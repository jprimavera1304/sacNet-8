using ISL_Service.Application.DTOs.VentasCancelacion;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/*
  LAS REGLAS DE AQUI NO SON NUESTRAS

  Cada una esta copiada de ValidaCancelarVenta y btnCancelarVenta_Click
  (Legacy/Mac31/Forms/Ventas/ConsultarVentas.cs, alrededor de la linea 2727),
  con el mensaje que enseña Mac31 tal cual, en mayusculas incluidas. Esos
  mensajes son los que la gente repite por telefono cuando llama a preguntar por
  que no pudo cancelar; traducirlos "a algo mas claro" rompe esa conversacion.

  QUE IMPIDE CANCELAR, EN EL ORDEN EN QUE MAC31 PREGUNTA

    1. El folio empieza con "CV-"  -> es un cambio de vigencia.
    2. Ya esta cancelada.
    3. Esta pagada y debe algo, salvo en centro de servicio.
    4. Tiene abonos, salvo en centro de servicio.
    5. Ya esta facturada (solo en TAU).

  Y LO QUE NO IMPIDE PERO PIDE ALGO: si el folio sigue dentro de un concentrado
  abierto, Mac31 no se niega —ofrece sacarlo del concentrado, y para eso pide la
  contrasena de supervisor—. Es la unica cosa que "te pide otra cosa" cuando si
  se puede cancelar.

  LO QUE NO SE VALIDA AQUI ES A PROPOSITO. El saldo a favor, la falta de usados
  en inventario y los errores de la propia cancelacion los decide
  sp_n_CancelarVenta, y su mensaje se devuelve sin tocar. Adelantarnos seria
  escribir una segunda version de la regla que se despegaria de la primera.
*/
public class VentasCancelacionService : IVentasCancelacionService
{
    /// El nombre del formulario de Mac31, que es lo que se guarda en la bitacora
    /// de intentos de contrasena. Se conserva para que los dos canales se lean
    /// en el mismo reporte.
    private const string FormaBitacora = "ConsultarVentas";

    private readonly IVentasCancelacionRepository _repo;

    /// Para dejar constancia del caso "fuera del concentrado y sin cancelar", que
    /// es el unico de este servicio que deja dinero suelto. Ver CancelarAsync.
    private readonly ILogger<VentasCancelacionService> _logger;

    public VentasCancelacionService(IVentasCancelacionRepository repo, ILogger<VentasCancelacionService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task<VentasCancelacionVerificarResponse> VerificarAsync(IReadOnlyCollection<int> idsVenta, CancellationToken ct)
    {
        var candidatos = await EvaluarAsync(idsVenta, ct);

        return new VentasCancelacionVerificarResponse
        {
            Candidatos = candidatos.Select(c => c.Candidato).ToList(),
            PuedeContinuar = candidatos.Count > 0 && candidatos.All(c => c.Candidato.PuedeCancelar),
            RequiereContrasena = candidatos.Any(c => c.Candidato.RequiereContrasena)
        };
    }

    public async Task<VentasCancelacionResponse> CancelarAsync(VentasCancelacionRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var ids = (request.IdsVenta ?? new List<int>()).Where(x => x > 0).Distinct().ToList();
        if (ids.Count == 0)
            throw new ArgumentException("Selecciona una remisión a cancelar.");

        /*
          SE VUELVE A VALIDAR, ENTERO

          No es desconfianza del front: entre que se pinto el dialogo y se
          apreto "Sí, cancelar", alguien pudo cobrar o facturar esa misma
          remision desde Mac31. Validar aqui es lo unico que ve el estado real.
        */
        var evaluados = await EvaluarAsync(ids, ct);

        var bloqueada = evaluados.FirstOrDefault(e => !e.Candidato.PuedeCancelar);
        if (bloqueada is not null)
            throw new ConflictException(bloqueada.Candidato.Motivo);

        /*
          MAC31 NO CANCELA NADA SI ALGO DE LO MARCADO NO SE PUEDE

          ValidaCancelarVenta corre sobre todo lo seleccionado ANTES de
          preguntar, y a la primera que falla se sale sin cancelar ni las que si
          podian. Se respeta: cancelar la mitad de lo marcado y avisar despues
          es peor que no cancelar nada.
        */

        var conConcentrado = evaluados.Where(e => e.Concentrado is not null).ToList();
        if (conConcentrado.Count > 0)
            await ValidarContrasenaAsync(request.Contrasena, idUsuario, equipo, ct);

        /*
          SON DOS ESCRITURAS Y NO SE PUEDEN ENVOLVER EN UNA TRANSACCION. SE DICE
          POR QUE, Y SE DEJA DE FALLAR EN SILENCIO.

          LO QUE PASABA
          -------------
          Por cada venta se hacian dos cosas en este orden: quitarla del
          concentrado (sp_n_ActualizarConcentradoDetalle) y luego cancelarla
          (sp_n_CancelarVenta). Dos procedimientos, dos conexiones, sin
          transaccion. Si el segundo fallaba, la remision salia del concentrado
          pero seguia VIVA y POR COBRAR: fuera de la lista del cobrador, o sea
          invisible. Dinero que nadie va a ir a buscar. Y el bucle seguia con las
          demas ventas, acumulando casos.

          POR QUE NO SE ENVUELVE
          ----------------------
          Se leyeron los tres procedimientos en la base:

            sp_n_ActualizarConcentradoDetalle  BEGIN TRAN + ROLLBACK TRAN desnudo
            sp_n_CancelarVenta                 BEGIN TRAN + ROLLBACK TRAN desnudo
            sp_n_ConsultarConcentradoFolio     no abre nada (solo lee)

          Ninguno de los dos que escriben consulta @@TRANCOUNT ni usa SAVE TRAN.
          Un `ROLLBACK TRAN` desnudo dentro de una transaccion abierta desde C# no
          deshace lo del procedimiento: deshace LA NUESTRA, y despues el
          procedimiento regresa normal. El resultado seria peor que lo que hay: al
          intentar el commit, C# revienta y el usuario recibe un 500 en vez del
          mensaje de negocio. Es el mismo defecto que ya estaba documentado en la
          carga de estado de cuenta bancario (OperacionMacRepository).

          Para hacerlo atomico de verdad haria falta un `sp_w_CancelarVentaDelConcentrado`
          que hiciera los dos pasos con una sola transaccion bien hecha. Eso es
          cambio en la base y no se hace desde aqui: los sp_n_ no se editan.

          LO QUE SI SE HACE
          -----------------
          Dejar de fallar callado. Si una venta ya salio del concentrado y su
          cancelacion NO prospero, se PARA -no se siguen ensuciando las demas- y se
          contesta 409 nombrando el folio y diciendo exactamente en que estado
          quedo, para que alguien lo arregle hoy y no dentro de un mes. El caso se
          registra ademas como error del servidor.
        */
        var resultados = new List<VentasCancelacionResultado>();

        foreach (var e in evaluados)
        {
            var saleDelConcentrado = e.Concentrado is not null;
            if (saleDelConcentrado)
                await _repo.QuitarDelConcentradoAsync(e.Venta, e.Concentrado!, idUsuario, equipo, ct);

            var resultado = await _repo.CancelarAsync(e.Venta, idUsuario, equipo, ct);
            resultados.Add(resultado);

            if (saleDelConcentrado && !resultado.Cancelada)
            {
                _logger.LogError(
                    "REMISION FUERA DEL CONCENTRADO Y SIN CANCELAR. idVenta={IdVenta} folio={Folio} concentrado={IdConcentrado} usuario={IdUsuario} mensaje={Mensaje}",
                    e.Venta.IdVenta, resultado.Folio, e.Concentrado!.IdConcentrado, idUsuario, resultado.Mensaje);

                var yaCanceladas = resultados.Where(x => x.Cancelada).Select(x => x.Folio).ToList();

                throw new ConflictException(
                    $"La remisión {resultado.Folio} SALIÓ del concentrado pero NO se pudo cancelar, "
                    + "así que sigue viva y por cobrar fuera de la lista del cobrador. "
                    + "Hay que volver a agregarla al concentrado o cancelarla desde Mac31 antes de seguir. "
                    + $"Detalle de la base: {resultado.Mensaje}",
                    $"Concentrado {e.Concentrado!.IdConcentrado}, IDVenta {e.Venta.IdVenta}. "
                    + $"Canceladas antes de parar: {(yaCanceladas.Count == 0 ? "ninguna" : string.Join(", ", yaCanceladas))}. "
                    + "Las remisiones restantes de la selección NO se tocaron.");
            }
        }

        return new VentasCancelacionResponse
        {
            Resultados = resultados,
            Canceladas = resultados.Count(r => r.Cancelada),
            Fallidas = resultados.Count(r => !r.Cancelada)
        };
    }

    /* ------------------------------------------------------------------ */

    private sealed class Evaluada
    {
        public required VentaParaCancelar Venta { get; init; }
        public required VentasCancelacionCandidato Candidato { get; init; }
        public ConcentradoDeVenta? Concentrado { get; init; }
    }

    private async Task<List<Evaluada>> EvaluarAsync(IReadOnlyCollection<int> idsVenta, CancellationToken ct)
    {
        var ids = idsVenta.Where(x => x > 0).Distinct().ToList();
        if (ids.Count == 0)
            throw new ArgumentException("Selecciona una remisión a cancelar.");

        var constantes = await _repo.ConsultarConstantesAsync(ct);
        var ventas = await _repo.ConsultarVentasAsync(ids, ct);

        // TAU es la funcionalidad del tenant. Mac31 pregunta con "contiene" y no
        // con "igual" (Variables.funcionalidad.ToUpper().Contains("TAU")); aqui
        // igual, para no separarnos de el por un detalle de configuracion.
        var esTau = constantes.Funcionalidad.Contains("TAU", StringComparison.OrdinalIgnoreCase);
        var esCentroServicio = constantes.EsCentroServicio == 1;

        var lista = new List<Evaluada>();

        foreach (var id in ids)
        {
            var venta = ventas.FirstOrDefault(v => v.IdVenta == id);

            if (venta is null)
            {
                lista.Add(new Evaluada
                {
                    Venta = new VentaParaCancelar { IdVenta = id },
                    Candidato = new VentasCancelacionCandidato
                    {
                        IdVenta = id,
                        PuedeCancelar = false,
                        Motivo = "LA REMISIÓN YA NO EXISTE. VUELVE A CONSULTAR."
                    }
                });
                continue;
            }

            var motivo = MotivoQueImpide(venta, esTau, esCentroServicio);

            var candidato = new VentasCancelacionCandidato
            {
                IdVenta = venta.IdVenta,
                FolioFtm = venta.FolioFtm,
                NombreCliente = venta.NombreCliente,
                PuedeCancelar = motivo.Length == 0,
                Motivo = motivo
            };

            /*
              Lo del concentrado solo lo mira Mac31 en TAU, y solo despues de
              pasar las validaciones: preguntarlo para una remision que de todas
              formas no se puede cancelar es una consulta de mas y un dialogo que
              confunde.
            */
            ConcentradoDeVenta? concentrado = null;
            if (candidato.PuedeCancelar && esTau)
            {
                concentrado = await _repo.ConsultarConcentradoAbiertoAsync(venta.Folio, ct);
                if (concentrado is not null)
                {
                    candidato.RequiereContrasena = true;
                    candidato.FolioConcentrado = concentrado.FolioConcentrado;
                }
            }

            lista.Add(new Evaluada { Venta = venta, Candidato = candidato, Concentrado = concentrado });
        }

        return lista;
    }

    /// Vacio = si se puede cancelar. Si no, el texto de Mac31, sin retocar.
    private static string MotivoQueImpide(VentaParaCancelar venta, bool esTau, bool esCentroServicio)
    {
        // El prefijo lo pone fn_DevuelvePrefijoFolio; Mac31 lo lee del folio
        // formateado de la rejilla, que sale de la misma funcion.
        if (venta.FolioFtm.Contains("CV-", StringComparison.OrdinalIgnoreCase))
            return "NO SE PUEDE CANCELAR EL CAMBIO DE VIGENCIA.";

        if (venta.Cancelada)
            return $"LA REMISIÓN  {venta.FolioFtm}  ESTÁ CANCELADA.";

        /*
          "Pagada" y "tiene abonos" son dos casos distintos con dos textos
          distintos, y en Mac31 van con else-if: una remision pagada nunca
          enseña el de abonos aunque los tenga.

          En centro de servicio NINGUNO de los dos aplica: alla se cancela y se
          vuelve a capturar todo el dia, y el cobro se arregla aparte.
        */
        if (venta.Pagada && venta.TotalPagar > 0 && !esCentroServicio)
            return $"LA REMISIÓN  {venta.FolioFtm}  YA ESTÁ PAGADA.\n\nNO SE PUEDE CANCELAR.";

        if (venta.Abonos > 0 && !esCentroServicio)
            return $"LA REMISIÓN  {venta.FolioFtm}  TIENE PAGOS.\n\nNO SE PUEDE CANCELAR.";

        // Facturada: solo en TAU. Una factura timbrada no se deshace cancelando
        // la remision; se cancela la factura primero.
        if (venta.TotalFacturado > 0 && esTau)
            return $"LA REMISIÓN  {venta.FolioFtm}  YA ESTÁ FACTURADA.\n\nNO SE PUEDE CANCELAR.";

        return "";
    }

    /*
      LA CONTRASENA DE SUPERVISOR

      Es la de Mac31 (ConfirmarContrasena.GenerarContrasena), letra por letra:

        HH + mm de AHORA  +  el DIA de la fecha de operacion  +  dos letras del MES

      Cambia cada minuto a proposito: no es un secreto que se reparte, es un
      codigo que solo puede decirte por telefono quien esta viendo el sistema en
      ese momento. Y si el usuario tiene contrasena de autorizacion propia dada
      de alta, esa manda y el codigo del minuto no aplica.

      SE VALIDA AQUI Y NO EN EL NAVEGADOR. En Mac31 la comparacion es del lado
      del cliente porque el cliente es la maquina de la oficina; una pagina web
      no lo es, y cualquiera puede abrir las herramientas del navegador.

      LOS DOS ATAJOS DE MAC31 NO SE COPIAN: alla cualquier contrasena pasa si la
      maquina se llama DESKTOP-0RTIOR5 o si el usuario es el 3. Son puertas de
      desarrollo; en una direccion publica son un agujero.
    */
    private async Task ValidarContrasenaAsync(string? capturada, int idUsuario, string equipo, CancellationToken ct)
    {
        var texto = (capturada ?? "").Trim();

        if (texto.Length == 0)
            throw new ConflictException("INGRESE LA CONTRASEÑA.");

        var propia = await _repo.ConsultarContrasenaAutorizacionAsync(idUsuario, ct);
        var constantes = await _repo.ConsultarConstantesAsync(ct);

        var esperada = propia is { Length: > 0 }
            ? propia
            : ContrasenaDelMinuto(constantes.FechaOperacion, DateTime.Now);

        var correcta = string.Equals(texto, esperada, StringComparison.OrdinalIgnoreCase);

        // La bitacora se escribe acierte o no: el intento fallido es justo el que
        // interesa mirar despues.
        await _repo.RegistrarIntentoContrasenaAsync(FormaBitacora, correcta, texto, idUsuario, equipo, ct);

        if (!correcta)
            throw new ConflictException("LA CONTRASEÑA ES INCORRECTA.");
    }

    private static readonly string[] MesEnDosLetras =
        { "EN", "FE", "MA", "AB", "MA", "JU", "JU", "AG", "SE", "OC", "NO", "DI" };

    public static string ContrasenaDelMinuto(DateTime fechaOperacion, DateTime ahora)
        => $"{ahora:HH}{ahora:mm}{fechaOperacion.Day:00}{MesEnDosLetras[fechaOperacion.Month - 1]}";
}
