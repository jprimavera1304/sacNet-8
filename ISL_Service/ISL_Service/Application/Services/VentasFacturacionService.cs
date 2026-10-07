using System.Net;
using System.Net.Mail;
using ISL_Service.Application.DTOs.VentasEscrituras;
using ISL_Service.Application.DTOs.VentasFacturacion;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Facturacion;

namespace ISL_Service.Application.Services;

/*
  FACTURACION: EL ORDEN DE LAS COSAS

  El orden importa mas que cualquier otra cosa de este archivo, y es el de legacy
  porque es el unico que no deja basura:

      1. leer constantes y comprobar el candado del key
      2. resolver serie y folio
      3. TIMBRAR en Facturapi   <- el unico paso irreversible
      4. guardar el renglon de parametros del CFDI
      5. insertar la factura en Mac31 (una por remision)

  Timbrar ANTES de escribir significa que un fallo en Facturapi no deja una
  factura fantasma en la base. Y significa tambien lo contrario, que es el riesgo
  real de este modulo: si el paso 3 funciona y el 5 falla, hay un CFDI emitido
  ante el SAT que Mac31 no conoce. No hay forma de evitarlo —no se puede meter una
  llamada HTTP dentro de una transaccion de SQL Server—, asi que lo que se hace es
  DECIRLO: el mensaje de ese caso nombra el UUID y el id de Facturapi para que
  alguien pueda arreglarlo a mano. Legacy no lo dice.

  EL CANDADO DEL KEY NO ES OPCIONAL. Es lo unico que impide que una instalacion de
  pruebas con una copia de la base de produccion timbre comprobantes reales.
*/
public class VentasFacturacionService : IVentasFacturacionService
{
    private readonly IVentasFacturacionRepository _repository;
    private readonly FacturapiCliente _facturapi;
    private readonly ILogger<VentasFacturacionService> _logger;

    public VentasFacturacionService(
        IVentasFacturacionRepository repository,
        FacturapiCliente facturapi,
        ILogger<VentasFacturacionService> logger)
    {
        _repository = repository;
        _facturapi = facturapi;
        _logger = logger;
    }

    public async Task<FacturaTimbradaDto> TimbrarAsync(
        FacturaTimbrarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        Validar(request);

        var constantes = await _repository.LeerConstantesAsync(ct);
        var secreto = ReglasFacturacion.SecretoReal(constantes.FacturapiSecretKey, constantes.AppMac)
            ?? throw new ConflictException(ReglasFacturacion.MensajeKeyNoCoincide);

        var esNotaCredito = request.Tipo is 1 or 2 or 3;
        var sinRemision = request.IdsVenta.Count == 0;

        var tipoDocumento = esNotaCredito
            ? ReglasFacturacion.TipoDocumento.NotaCredito
            : sinRemision
                ? ReglasFacturacion.TipoDocumento.FacturaSinRemision
                : ReglasFacturacion.TipoDocumento.Factura;

        var serieBase = ReglasFacturacion.Serie(constantes.FacturapiSecretKey, constantes.AppMac, tipoDocumento);

        /*
          EL FOLIO SALE DE LA PRIMERA REMISION, COMO EN LEGACY.

          Cuando se facturan varias remisiones en un solo CFDI, el folio fiscal es
          el de la primera y las demas quedan ligadas por su propio renglon de
          VentaFactura. Sin remision no hay folio: lo pone Facturapi.
        */
        var (serie, folio) = sinRemision
            ? (serieBase, 0)
            : ReglasFacturacion.SerieYFolio(serieBase, request.FoliosVenta.Count > 0 ? request.FoliosVenta[0] : null);

        var tipoCfdi = esNotaCredito ? "E" : null;

        var respuesta = await _facturapi.TimbrarAsync(secreto, tipoCfdi, serie, folio, request, ct);
        if (!respuesta.Ok || respuesta.Comprobante is null)
            throw new ConflictException(respuesta.Error ?? "NO SE PUDO GENERAR LA FACTURA.");

        var timbrada = respuesta.Comprobante;
        timbrada.CadenaQr = ReglasFacturacion.CadenaQr(
            constantes.UrlVerificaSat, timbrada.Uuid, constantes.RfcEmisor, request.Receptor.Rfc,
            timbrada.Total, selloCfdi: null);

        await GuardarAsync(request, timbrada, serie, folio, request.Tipo, sinRemision,
            fechaPago: null, fechaPagoSat: string.Empty, numComplementos: 0,
            idUsuario, equipo, ct);

        return timbrada;
    }

    public async Task<FacturaTimbradaDto> TimbrarComplementoAsync(
        ComplementoPagoTimbrarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (request.IdEmpresa <= 0) throw new ArgumentException("La empresa es obligatoria.");
        if (string.IsNullOrWhiteSpace(request.FormaPago)) throw new ArgumentException("La forma de pago es obligatoria.");
        if (request.FechaPago is null) throw new ArgumentException("La fecha y hora del pago son obligatorias.");
        if (request.Documentos.Count == 0) throw new ArgumentException("Hay que indicar al menos un documento a pagar.");
        if (request.Documentos.Any(d => d.Importe <= 0))
            throw new ArgumentException("El importe de cada documento tiene que ser mayor que cero.");
        if (string.IsNullOrWhiteSpace(request.Receptor.Rfc))
            throw new ArgumentException("El RFC del receptor es obligatorio.");

        var constantes = await _repository.LeerConstantesAsync(ct);
        var secreto = ReglasFacturacion.SecretoReal(constantes.FacturapiSecretKey, constantes.AppMac)
            ?? throw new ConflictException(ReglasFacturacion.MensajeKeyNoCoincide);

        /*
          CADA DOCUMENTO APORTA SU UUID, SU PARCIALIDAD Y SU SALDO ANTERIOR.

          Los tres son obligatorios en el CFDI de pago y los tres salen de la
          factura guardada: la parcialidad es el numero de pagos que ya lleva mas
          uno, y el saldo anterior es el que tenia ANTES de este pago. Si falta uno,
          el SAT rechaza el complemento, asi que si no esta la factura se para aqui
          y no se timbra nada.
        */
        var relacionados = new List<(string Uuid, int Parcialidad, decimal SaldoAnterior)>();
        foreach (var documento in request.Documentos)
        {
            if (string.IsNullOrWhiteSpace(documento.IdFacturapi))
                throw new ArgumentException("Cada documento necesita su id de Facturapi.");

            var guardada = await _repository.LeerFacturaAsync(documento.IdFacturapi!, ct);
            if (guardada is null || string.IsNullOrWhiteSpace(guardada.Uuid))
                throw new NotFoundException($"No se encontro la factura {documento.IdFacturapi} o no tiene UUID.");

            relacionados.Add((guardada.Uuid!, guardada.NumeroPagos + 1, guardada.Saldo));
        }

        var respuesta = await _facturapi.TimbrarComplementoAsync(
            secreto, request, relacionados, constantes.PorcentajeIva, ct);
        if (!respuesta.Ok || respuesta.Comprobante is null)
            throw new ConflictException(respuesta.Error ?? "NO SE PUDO GENERAR EL COMPLEMENTO DE PAGO.");

        var timbrada = respuesta.Comprobante;
        timbrada.CadenaQr = ReglasFacturacion.CadenaQr(
            constantes.UrlVerificaSat, timbrada.Uuid, constantes.RfcEmisor, request.Receptor.Rfc,
            timbrada.Total, selloCfdi: null);

        /*
          UN RENGLON DE FACTURA POR DOCUMENTO PAGADO, igual que legacy: el
          complemento es uno ante el SAT y N renglones en Mac31, uno por cada
          factura a la que se le abono.
        */
        const int TipoComplementoPago = 4;
        var fechaPagoSat = ReglasFacturacion.FechaPagoSat(request.FechaPago);
        var idParametros = await GuardarParametrosAsync(timbrada, request.Observaciones, idUsuario, equipo, ct);
        timbrada.IdParametros = idParametros;

        for (var i = 0; i < request.Documentos.Count; i++)
        {
            var documento = request.Documentos[i];
            var comoFactura = new FacturaTimbrarRequest
            {
                IdEmpresa = request.IdEmpresa,
                IdClienteFacturacion = request.IdClienteFacturacion,
                IdCliente = request.IdCliente,
                FormaPago = request.FormaPago,
                Receptor = request.Receptor,
                IdEmpresaCs = request.IdEmpresaCs,
                IncluirImportes = 1
            };

            var deEsteDocumento = new FacturaTimbradaDto
            {
                IdFacturapi = timbrada.IdFacturapi,
                Uuid = timbrada.Uuid,
                Serie = timbrada.Serie,
                Folio = timbrada.Folio,
                /* El total del renglon es lo que se abono a ESA factura, no el del
                   complemento completo. Asi lo hace legacy y es lo correcto: el
                   saldo de cada factura se mueve por su importe. */
                Total = documento.Importe,
                Status = timbrada.Status,
                CancellationStatus = timbrada.CancellationStatus,
                FechaCreacion = timbrada.FechaCreacion,
                FechaComprobante = timbrada.FechaComprobante
            };

            var insertado = await _repository.InsertarFacturaAsync(
                comoFactura, deEsteDocumento, idParametros,
                idVenta: 0, folio: timbrada.Folio, serie: timbrada.Serie ?? string.Empty,
                totalFacturadoVenta: documento.Importe, tipo: TipoComplementoPago,
                fechaPago: request.FechaPago, fechaPagoSat: fechaPagoSat,
                numComplementosPago: request.Documentos.Count,
                idVentaFacturaRelacionada: documento.IdVentaFactura,
                idUsuario, equipo, ct);

            RegistrarSiQuedoHuerfano(insertado, timbrada, i);
            if (insertado.Id is > 0) timbrada.IdsVentaFactura.Add(insertado.Id.Value);
            timbrada.Resultado = insertado.Resultado;
            timbrada.Mensaje = insertado.Mensaje;
        }

        return timbrada;
    }

    public async Task<ResultadoLegacyDto> CancelarAsync(
        FacturaCancelarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.IdFacturapi))
            throw new ArgumentException("El comprobante es obligatorio.");
        if (string.IsNullOrWhiteSpace(request.IdFeMotivoCancelacion))
            throw new ArgumentException("El motivo del SAT es obligatorio.");
        if (string.IsNullOrWhiteSpace(request.MotivoCancelacion))
            throw new ArgumentException("El motivo de la cancelacion es obligatorio.");

        /*
          EL MOTIVO 01 DEL SAT EXIGE EL FOLIO SUSTITUTO.

          "Comprobante emitido con errores con relacion" significa que hay otro
          comprobante que lo sustituye, y el SAT rechaza la cancelacion sin ese
          UUID. Legacy no lo pide y el rechazo llega despues, con un mensaje del
          SAT que nadie relaciona con este campo.
        */
        if (ReglasFacturacion.ClaveSat(request.IdFeMotivoCancelacion) == "01"
            && string.IsNullOrWhiteSpace(request.UuidSustituto))
            throw new ArgumentException("El motivo 01 necesita el folio fiscal del comprobante que lo sustituye.");

        var constantes = await _repository.LeerConstantesAsync(ct);
        var secreto = ReglasFacturacion.SecretoReal(constantes.FacturapiSecretKey, constantes.AppMac)
            ?? throw new ConflictException(ReglasFacturacion.MensajeKeyNoCoincide);

        var respuesta = await _facturapi.CancelarAsync(
            secreto, request.IdFacturapi!, request.IdFeMotivoCancelacion, request.UuidSustituto, ct);

        /*
          UNA CANCELACION YA PEDIDA NO ES UN ERROR.

          Facturapi contesta "INVOICE HAS A PENDING CANCELLATION REQUEST" cuando ya
          hay una solicitud en curso. Legacy lo ignora y cancela en Mac31 igual; eso
          se conserva, porque el estado real —pendiente de que el cliente acepte— se
          consulta despues y aqui lo que importa es no dejar la base diciendo que
          nunca se pidio.
        */
        var yaPedida = respuesta.Error is not null
            && respuesta.Error.Contains("PENDING CANCELLATION REQUEST", StringComparison.OrdinalIgnoreCase);

        if (!respuesta.Ok && !yaPedida)
            throw new ConflictException(respuesta.Error ?? "No se pudo cancelar el comprobante.");

        /*
          SE GUARDA LO QUE DIJO EL SAT, NO UN 2 FIJO.

          Legacy mapea bien la respuesta (pending, rejected, expired, accepted) y
          acto seguido la sobreescribe con Accepted(2) "porque el servicio no
          informa bien". El resultado es que una cancelacion RECHAZADA por el
          cliente queda en Mac31 como aceptada: la factura sigue valida ante el SAT
          y el sistema la da por cancelada. Eso es lo peor que puede pasar en
          facturacion, asi que aqui se guarda el estado real.
        */
        var cancellationStatus = respuesta.Comprobante?.CancellationStatus
            ?? (yaPedida ? "pending" : string.Empty);
        var status = respuesta.Comprobante?.Status ?? string.Empty;

        var statusCancelacion = cancellationStatus.ToLowerInvariant() switch
        {
            "accepted" => 2,
            "pending" => 3,
            "rejected" => 4,
            "expired" => 5,
            _ => 3
        };

        /* El documento solo se da por cancelado cuando el SAT lo acepto. */
        var idStatus = statusCancelacion == 2 ? 2 : 1;

        var resultado = await _repository.CancelarFacturaAsync(
            request.IdFacturapi!, idStatus, request.IdFeMotivoCancelacion, request.MotivoCancelacion,
            statusCancelacion, status, cancellationStatus,
            respuesta.Comprobante?.FechaCreacion, idUsuario, equipo, ct);

        if (string.IsNullOrEmpty(resultado.Mensaje))
            resultado.Mensaje = MensajeDeCancelacion(statusCancelacion, request.Tipo);

        return resultado;
    }

    /// <summary>
    /// El texto que ve el usuario segun lo que dijo el SAT. Son los mismos
    /// literales de legacy, que es lo que la gente reconoce — pero aqui el que se
    /// manda es el que corresponde al estado REAL, no siempre el de "cancelada".
    /// </summary>
    public static string MensajeDeCancelacion(int statusCancelacion, int tipo)
    {
        if (statusCancelacion == 3)
            return "LA CANCELACION ESTA EN ESPERA DE QUE SEA ACEPTADA POR EL CLIENTE.";
        if (statusCancelacion == 4)
            return "EL CLIENTE RECHAZO LA CANCELACION, EL COMPROBANTE NO FUE CANCELADO.";
        if (statusCancelacion == 5)
            return "LA SOLICITUD EXPIRO, EL COMPROBANTE NO FUE CANCELADO.";

        return tipo switch
        {
            1 or 2 or 3 => "LA NOTA DE CREDITO HA SIDO CANCELADA.",
            4 => "EL COMPLEMENTO DE PAGO HA SIDO CANCELADO.",
            _ => "LA FACTURA HA SIDO CANCELADA."
        };
    }

    public async Task<FilasLegacyDto> ConsultarAsync(FacturaConsultaRequest request, CancellationToken ct)
    {
        /*
          ESTA CONSULTA NO ESCRIBE, Y LA DE LEGACY SI.

          En legacy, cuando se pide por IDVenta y la factura esta vigente, se
          pregunta a Facturapi y si alla aparece cancelada se llama a
          sp_n_CancelarVentaFactura desde el endpoint de CONSULTA. Es exactamente lo
          que advierte CUIDADO-consultas-que-escriben.md: una pantalla que dice
          consultar y cancela. Aqui no; sincronizar el estado con el SAT es una
          accion propia, con su permiso, y no un efecto secundario de abrir una
          lista.
        */
        request.IdsVentaFactura = Infrastructure.Data.FiltrosLegacy.LimpiarIds(request.IdsVentaFactura);
        request.IdsVenta = Infrastructure.Data.FiltrosLegacy.LimpiarIds(request.IdsVenta);
        request.IdsUsuario = Infrastructure.Data.FiltrosLegacy.LimpiarIds(request.IdsUsuario);
        request.IdsCliente = Infrastructure.Data.FiltrosLegacy.LimpiarIds(request.IdsCliente);

        var hayFiltro = request.IdVentaFactura > 0 || request.IdsVentaFactura.Count > 0
            || !string.IsNullOrWhiteSpace(request.IdFacturapi) || request.IdsVenta.Count > 0
            || request.IdCliente > 0 || request.IdsCliente.Count > 0 || request.IdAgente > 0
            || request.FolioInicial > 0 || request.FechaInicial is not null;

        if (!hayFiltro)
            throw new ArgumentException("Hay que indicar al menos un filtro: factura, remision, cliente, agente, folio o rango de fechas.");

        return await _repository.ConsultarAsync(request, ct);
    }

    public async Task<string> EnviarCorreoAsync(FacturaEnviarCorreoRequest request, CancellationToken ct)
    {
        if (request.IdVentaFactura <= 0)
            throw new ArgumentException("La factura es obligatoria.");
        if (string.IsNullOrWhiteSpace(request.Correos))
            throw new ArgumentException("Hay que indicar al menos un correo.");

        var datos = await _repository.LeerDatosCorreoAsync(request.IdVentaFactura, ct)
            ?? throw new NotFoundException("No se encontro la factura.");

        if (string.IsNullOrWhiteSpace(datos.Smtp) || datos.Puerto <= 0 || string.IsNullOrWhiteSpace(datos.Remitente))
            throw new ConflictException("La empresa no tiene configurado el correo de facturas.");

        var pdf = datos.Archivo;
        var xml = pdf.Replace(".pdf", ".xml", StringComparison.OrdinalIgnoreCase);

        /*
          NO SE ESPERA CUATRO SEGUNDOS A QUE APAREZCA EL ARCHIVO.

          Legacy, si no encuentra el PDF, hace Thread.Sleep(4000) y vuelve a mirar:
          bloquea un hilo del servidor apostando a que el proceso que genera el
          documento acabe. Aqui, si el archivo no esta, se dice que no esta — con su
          ruta, que es lo que hace falta para arreglarlo.
        */
        if (!File.Exists(pdf))
            throw new ConflictException($"NO SE ENCONTRO EL ARCHIVO PDF.{Environment.NewLine}({pdf})");
        if (!File.Exists(xml))
            throw new ConflictException($"NO SE ENCONTRO EL ARCHIVO XML.{Environment.NewLine}({xml})");

        /*
          EL CANDADO DE MAQUINA NO SE HEREDA.

          Legacy no manda el correo si Environment.MachineName es "DESKTOP-0RTIOR5",
          y contesta OK igual. Es un parche de alguien que probaba en su maquina,
          escrito dentro del codigo de produccion: en esa maquina la pantalla dice
          "enviado" y no se envio nada. Un servidor web no es la maquina de nadie,
          asi que ese candado aqui no significa nada y seria solo una trampa.
        */
        using var mensaje = new MailMessage
        {
            From = new MailAddress(datos.Remitente, string.IsNullOrWhiteSpace(datos.RemitenteNombre) ? datos.Remitente : datos.RemitenteNombre),
            Subject = datos.Asunto,
            Body = datos.Cuerpo,
            IsBodyHtml = true
        };

        foreach (var correo in Destinatarios(request.Correos))
            mensaje.To.Add(correo);

        if (mensaje.To.Count == 0)
            throw new ArgumentException("Ningun correo de la lista es valido.");

        if (!string.IsNullOrWhiteSpace(datos.ConCopia))
        {
            foreach (var copia in Destinatarios(datos.ConCopia))
                mensaje.Bcc.Add(copia);
        }

        await using var flujoPdf = File.OpenRead(pdf);
        await using var flujoXml = File.OpenRead(xml);
        mensaje.Attachments.Add(new Attachment(flujoPdf, Path.GetFileName(pdf)));
        mensaje.Attachments.Add(new Attachment(flujoXml, Path.GetFileName(xml)));

        using var smtp = new SmtpClient(datos.Smtp, datos.Puerto)
        {
            EnableSsl = true,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(datos.Usuario, datos.Contrasena)
        };

        await smtp.SendMailAsync(mensaje, ct);
        return string.Join(", ", mensaje.To.Select(x => x.Address));
    }

    /* ---------------------------------------------------------------- */

    private async Task GuardarAsync(
        FacturaTimbrarRequest request, FacturaTimbradaDto timbrada, string serie, int folio,
        int tipo, bool sinRemision, DateTime? fechaPago, string fechaPagoSat, int numComplementos,
        int idUsuario, string equipo, CancellationToken ct)
    {
        var idParametros = await GuardarParametrosAsync(timbrada, request.Observaciones, idUsuario, equipo, ct);
        timbrada.IdParametros = idParametros;

        if (sinRemision || request.Tipo is 1 or 2 or 3)
        {
            var unico = await _repository.InsertarFacturaAsync(
                request, timbrada, idParametros, idVenta: 0, folio, serie,
                totalFacturadoVenta: timbrada.Total, tipo, fechaPago, fechaPagoSat, numComplementos,
                request.IdVentaFacturaRelacionada, idUsuario, equipo, ct);

            RegistrarSiQuedoHuerfano(unico, timbrada, 0);
            if (unico.Id is > 0) timbrada.IdsVentaFactura.Add(unico.Id.Value);
            timbrada.Resultado = unico.Resultado;
            timbrada.Mensaje = unico.Mensaje;
            return;
        }

        /*
          UN RENGLON POR REMISION, Y SOLO SI SE LE FACTURO ALGO.

          Legacy inserta solo cuando TotalFacturadoVenta > 0: una remision que
          entro al CFDI con importe cero no genera renglon. Se conserva, porque un
          renglon de factura con total cero descuadra el "cuanto se ha facturado de
          esta remision" que despues decide si se puede volver a facturar.
        */
        for (var i = 0; i < request.IdsVenta.Count; i++)
        {
            var idVenta = request.IdsVenta[i];
            var folioRemision = i < request.FoliosVenta.Count ? request.FoliosVenta[i] : null;
            var totalDeEsta = i < request.TotalesFacturar.Count ? request.TotalesFacturar[i] : 0m;

            if (totalDeEsta <= 0m) continue;

            var (serieDeEsta, folioDeEsta) = ReglasFacturacion.SerieYFolio(serie, folioRemision);

            var insertado = await _repository.InsertarFacturaAsync(
                request, timbrada, idParametros, idVenta, folioDeEsta, serieDeEsta,
                totalDeEsta, tipo, fechaPago, fechaPagoSat, numComplementos,
                request.IdVentaFacturaRelacionada, idUsuario, equipo, ct);

            RegistrarSiQuedoHuerfano(insertado, timbrada, i);
            if (insertado.Id is > 0) timbrada.IdsVentaFactura.Add(insertado.Id.Value);
            timbrada.Resultado = insertado.Resultado;
            timbrada.Mensaje = insertado.Mensaje;
        }
    }

    private async Task<int> GuardarParametrosAsync(
        FacturaTimbradaDto timbrada, string? observaciones, int idUsuario, string equipo, CancellationToken ct)
    {
        /*
          LAS VEINTE COLUMNAS, EN SU POSICION.

          Son las que el reporte de la factura vuelve a leer por numero, asi que la
          posicion es el contrato. Se respeta el reparto de legacy; lo que cambia es
          que las que no se usan van vacias en vez de quedar sin mandar, porque una
          columna sin mandar desplaza la lectura de todas las de despues.

          Param1 queda vacio: legacy nunca lo asigna en este flujo (su comentario
          dice "Equipo ~ IDUsuario" y el codigo no lo escribe). Se deja igual para
          no mover las diecinueve de abajo.
        */
        var parametros = new string[20];
        for (var i = 0; i < parametros.Length; i++) parametros[i] = string.Empty;

        parametros[2] = ReglasFacturacion.Param(timbrada.IdFacturapi, timbrada.Uuid, string.Empty);
        parametros[6] = timbrada.UrlVerificacion ?? string.Empty;
        parametros[7] = observaciones ?? string.Empty;

        return await _repository.GuardarParametrosAsync(idUsuario, equipo, parametros, ct);
    }

    /// <summary>
    /// Si el CFDI se emitio y el insert en Mac31 fallo, queda un comprobante fiscal
    /// que la base no conoce. No se puede deshacer —el SAT ya lo tiene— asi que se
    /// deja constancia con el UUID y el id de Facturapi, que es lo unico con lo que
    /// alguien puede arreglarlo a mano. Legacy no registra nada.
    /// </summary>
    private void RegistrarSiQuedoHuerfano(ResultadoLegacyDto insertado, FacturaTimbradaDto timbrada, int indice)
    {
        if (insertado.Hecho) return;

        _logger.LogError(
            "CFDI TIMBRADO SIN REGISTRAR EN MAC31. idFacturapi={IdFacturapi} uuid={Uuid} renglon={Indice} mensaje={Mensaje}",
            timbrada.IdFacturapi, timbrada.Uuid, indice, insertado.Mensaje);

        timbrada.Mensaje =
            $"EL COMPROBANTE SE TIMBRO ANTE EL SAT PERO NO SE PUDO GUARDAR EN MAC31. "
            + $"UUID {timbrada.Uuid}, Facturapi {timbrada.IdFacturapi}. "
            + $"Detalle: {insertado.Mensaje}";
    }

    private static void Validar(FacturaTimbrarRequest request)
    {
        if (request.IdEmpresa <= 0) throw new ArgumentException("La empresa es obligatoria.");
        if (request.IdClienteFacturacion <= 0) throw new ArgumentException("El cliente de facturacion es obligatorio.");
        if (string.IsNullOrWhiteSpace(request.FormaPago)) throw new ArgumentException("La forma de pago es obligatoria.");
        if (string.IsNullOrWhiteSpace(request.MetodoPago)) throw new ArgumentException("El metodo de pago es obligatorio.");
        if (string.IsNullOrWhiteSpace(request.RegimenFiscal)) throw new ArgumentException("El regimen fiscal es obligatorio.");

        /*
          EL USO DEL CFDI ES OBLIGATORIO, Y LEGACY LO VALIDABA MAL.

          Su condicion dice `IDFERegimenFiscal == null || IDFEUsoCFDI == ""`: valida
          el regimen contra null y el uso contra cadena vacia, asi que un regimen
          vacio (no nulo) pasaba y un uso nulo tambien.
        */
        if (string.IsNullOrWhiteSpace(request.UsoCfdi)) throw new ArgumentException("El uso del CFDI es obligatorio.");

        if (string.IsNullOrWhiteSpace(request.Receptor.Rfc)) throw new ArgumentException("El RFC del receptor es obligatorio.");
        if (string.IsNullOrWhiteSpace(request.Receptor.RazonSocial)) throw new ArgumentException("La razon social del receptor es obligatoria.");
        if (string.IsNullOrWhiteSpace(request.Receptor.CodigoPostal)) throw new ArgumentException("El codigo postal del receptor es obligatorio.");

        if (request.Conceptos.Count == 0) throw new ArgumentException("Hay que indicar al menos un concepto.");
        foreach (var concepto in request.Conceptos)
        {
            if (string.IsNullOrWhiteSpace(concepto.ClaveProdServ))
                throw new ArgumentException("Cada concepto necesita su clave del SAT.");
            if (concepto.Cantidad <= 0)
                throw new ArgumentException("La cantidad de cada concepto tiene que ser mayor que cero.");
            if (concepto.PrecioUnitarioConIva < 0)
                throw new ArgumentException("El precio de un concepto no puede ser negativo.");
        }

        var sinRemision = request.IdsVenta.Count == 0;
        if (sinRemision && request.IdPedido <= 0 && request.Tipo == 0)
            throw new ArgumentException("Una factura sin remision necesita el pedido.");

        if (!sinRemision)
        {
            if (request.FoliosVenta.Count != request.IdsVenta.Count)
                throw new ArgumentException("Hay que mandar un folio por cada remision.");
            if (request.TotalesFacturar.Count != request.IdsVenta.Count)
                throw new ArgumentException("Hay que mandar un total a facturar por cada remision.");
        }

        if (request.Tipo is 1 or 2 or 3 && string.IsNullOrWhiteSpace(request.IdFacturapiRelacionada))
            throw new ArgumentException("Una nota de credito necesita la factura a la que se relaciona.");
    }

    private static IEnumerable<string> Destinatarios(string? lista)
        => (lista ?? string.Empty)
            .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Contains('@'));
}
