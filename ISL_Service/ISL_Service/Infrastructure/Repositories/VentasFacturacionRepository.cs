using System.Data;
using ISL_Service.Application.DTOs.VentasEscrituras;
using ISL_Service.Application.DTOs.VentasFacturacion;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Infrastructure.Facturacion;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/*
  FACTURACION: LOS CINCO PROCEDIMIENTOS DE LEGACY

  Tres decisiones que importan:

  1) LAS CONSTANTES SE LEEN CON UN SELECT. Legacy las saca de
     sp_n_ConsultaConstantes, que cuando la fecha de operacion esta atrasada hace
     el CIERRE DEL DIA. Timbrar una factura no tiene por que congelar el inventario
     del dia anterior ni adelantar la fecha del negocio.

  2) EL @Params DE sp_n_rptFacturaElectronica ES UN SOLO PARAMETRO con el formato
     "@<IDVentaFactura>". Legacy llega ahi por un camino raro: un repositorio
     generico que descubre la firma del procedimiento con DeriveParameters, parte
     la cadena por "|", y hace reemplazos de "~" por "|" y de "[" por "~" segun lo
     que contenga el valor. Ese mecanismo existe porque el mismo repositorio sirve
     a decenas de reportes con firmas distintas. Aqui el procedimiento se llama por
     su nombre y con su parametro: no hay nada que descubrir.

  3) EL ESTATUS DE CANCELACION NO SE FUERZA. Legacy mapea bien la respuesta del SAT
     (pending, rejected, expired, accepted) y acto seguido LO SOBREESCRIBE con
     Accepted(2) "porque el servicio no informa bien". Resultado: una cancelacion
     que el cliente rechazo queda en Mac31 como aceptada, y la factura sigue
     valida ante el SAT mientras el sistema la da por cancelada. Eso no se hereda:
     aqui se guarda lo que contesto el SAT.
*/
public class VentasFacturacionRepository : SpLegacyRepositoryBase, IVentasFacturacionRepository
{
    private const string SpActualizarParametro = "sp_n_ActualizarParametro";
    private const string SpInsertarFactura = "sp_n_InsertarVentaFactura";
    private const string SpCancelarFactura = "sp_n_CancelarVentaFactura";
    private const string SpConsultarFacturas = "sp_n_ConsultaVentasFacturas";
    private const string SpReporteFactura = "sp_n_rptFacturaElectronica";

    private const int LargoParam = 4000;
    private const int LargoLista = 4000;
    private const int LargoCien = 100;
    private const int LargoDiez = 10;
    private const int LargoCinco = 5;
    private const int LargoNombreEquipo = 1000;
    private const int LargoFecha = 20;

    public VentasFacturacionRepository(IConfiguration configuration) : base(configuration) { }

    /// <summary>
    /// Solo lo que hace falta para hablar con Facturapi: la llave y el nombre de
    /// la aplicacion (de los dos sale el secreto real, ver ReglasFacturacion).
    /// </summary>
    /// <remarks>
    /// NO SE REUSA <see cref="LeerConstantesAsync"/> A PROPOSITO. Esa trae seis
    /// columnas porque timbrar las necesita, y pedir de mas es pedir que falle
    /// por algo que no te importa: "Ver el PDF" moria con
    /// "Invalid column name 'PorcentajeIva'" por una columna que no iba a usar.
    ///
    /// LO QUE DECIA ESTE COMENTARIO Y ERA FALSO, PORQUE COSTO CARO. Aqui estaba
    /// escrito que `PorcentajeIva` "no existe en TODAS las bases". No existe en
    /// NINGUNA: la columna de `dbo.Constantes` se llama `IVA` (comprobado el
    /// 2026-10-15 en MacZ y en Produccion_svr: `PorcentajeIva` no esta, `IVA`
    /// esta y vale 16.00). Se arreglo ESTE metodo quitando la columna mala y se
    /// dejo la mala en el otro, asi que preparar Y TIMBRAR contestaban 500
    /// siempre, para cualquier folio — por eso por el camino del web y del
    /// telefono no se habia timbrado nunca en seis meses. El aviso estaba
    /// escrito y el defecto siguio ahi: un comentario a medias es peor que
    /// ninguno, porque se cree.
    /// </remarks>
    public async Task<(string? SecretKey, string? AppMac)> LeerLlaveFacturapiAsync(CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(@"
SELECT TOP 1
    AppMac             = ISNULL(AppMac, ''),
    FacturapiSecretKey = ISNULL(FacturapiSecretKey, '')
FROM dbo.Constantes;", conn)
        { CommandTimeout = CommandTimeoutSeconds };

        await using var rd = await cmd.ExecuteReaderAsync(ct);
        if (!await rd.ReadAsync(ct)) return (null, null);

        return (rd["FacturapiSecretKey"] as string, rd["AppMac"] as string);
    }

    /// <summary>
    /// Las seis constantes que necesitan preparar y timbrar.
    /// </summary>
    /// <remarks>
    /// <para>
    /// EL IVA: LA COLUMNA SE LLAMA `IVA` Y VIENE EN PORCENTAJE, NO EN TANTO POR
    /// UNO. Son dos trampas, y la segunda es mucho peor que la primera.
    /// </para>
    /// <para>
    /// La primera es el nombre. No hay ninguna columna `PorcentajeIva` en
    /// `dbo.Constantes`: ni en MacZ ni en Produccion_svr. Nombrarla hacia que
    /// `POST /api/ventas/facturacion/preparar` y el timbrado contestaran
    /// "Invalid column name 'PorcentajeIva'" para TODA remision. Falla ruidoso
    /// y se ve.
    /// </para>
    /// <para>
    /// La segunda es el factor, y esa falla CALLADA. `IVA` esta guardado como
    /// 16.00, no como 0.16, y <c>PorcentajeIva</c> se lee con
    /// <c>rd.GetDecimal</c> sin tocar: aliasear `IVA` tal cual quita el 500 y
    /// deja un IVA CIEN VECES MAYOR, que ademas timbra. Seria cambiar un error
    /// visible por un error fiscal silencioso. De ahi el <c>/ 100.0</c>.
    /// </para>
    /// <para>
    /// EL TANTO POR UNO ES LA CONVENCION DE TODO EL CODIGO, no una decision de
    /// aqui: <c>IVentasUsadosRepository</c> ("En tanto por uno: Constantes.IVA
    /// / 100"), <c>VentasUsadosRepository</c> (<c>LeerDecimal(rd,"IVA") /
    /// 100m</c>), <c>MapaDeCapacidades</c> (<c>c.Iva / 100m</c>), y el propio
    /// procedimiento de legacy, que arranca con
    /// <c>SELECT @PorcentajeIva = IVA/100</c>.
    /// </para>
    /// <para>
    /// `100.0` y NO `100.0e0`: con el literal flotante el resultado seria
    /// `float` y <c>GetDecimal</c> reventaria.
    /// </para>
    /// <para>
    /// Lo vigila <c>ConstantesDeFacturacionContraLaBaseTests</c>, que ejecuta
    /// esta consulta de verdad en las dos empresas y exige 0.16. Caza las dos
    /// trampas a la vez: el nombre y el factor.
    /// </para>
    /// </remarks>
    public async Task<ConstantesFacturacion> LeerConstantesAsync(CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(@"
SELECT TOP 1
    AppMac              = ISNULL(AppMac, ''),
    FacturapiSecretKey  = ISNULL(FacturapiSecretKey, ''),
    Funcionalidad       = ISNULL(Funcionalidad, ''),
    URLVerificaSat      = ISNULL(URLVerificaSat, ''),
    RFCEmisor           = ISNULL(RFCEmisor, ''),
    -- LA COLUMNA SE LLAMA `IVA`, Y VA DIVIDIDA ENTRE 100. Las dos mitades de
    -- esto hacen falta; ver el bloque de arriba del metodo.
    PorcentajeIva       = ISNULL(IVA, 0) / 100.0
FROM dbo.Constantes;", conn)
        { CommandTimeout = CommandTimeoutSeconds };

        await using var rd = await cmd.ExecuteReaderAsync(ct);
        if (!await rd.ReadAsync(ct)) return new ConstantesFacturacion();

        return new ConstantesFacturacion
        {
            AppMac = rd.GetString(0),
            FacturapiSecretKey = rd.GetString(1),
            Funcionalidad = rd.GetString(2),
            UrlVerificaSat = rd.GetString(3),
            RfcEmisor = rd.GetString(4),
            PorcentajeIva = rd.GetDecimal(5)
        };
    }

    public async Task<int> GuardarParametrosAsync(
        int idUsuario, string equipo, IReadOnlyList<string> parametros, CancellationToken ct)
    {
        /*
          VEINTE COLUMNAS, SIEMPRE LAS VEINTE.

          El procedimiento declara @Param1 a @Param20 y el reporte las lee por
          posicion. Mandar menos deja las que falten con su valor por defecto, y
          entonces el reporte lee en la posicion equivocada: el sello donde deberia
          ir la direccion. Se mandan todas, vacias las que no se usan.
        */
        /*
          EL NOMBRE DE EQUIPO VA UNICO POR LLAMADA.

          sp_n_ActualizarParametro recupera el id del renglon que acaba de
          insertar con `MAX(IDParametros) WHERE NombreEquipo=@NombreEquipo AND
          IDUsuario=@IDUsuario`. Con el nombre repetido, dos llamadas de la misma
          pareja (equipo, usuario) se cruzan y una se lleva los parametros de la
          otra: el papel sale con los filtros ajenos. Aqui el equipo era el
          nombre de usuario del token, asi que bastaban dos pestanas de la misma
          persona -riesgo que ya estaba escrito en VentasConsultaService.

          Con el sufijo, el MAX() se calcula sobre un conjunto de un solo
          renglon. Nadie lee Parametros por NombreEquipo (los reportes van por
          IDParametros), y esto NO es el equipo que se escribe en Ventas como
          "desde donde se imprimio": ese sigue siendo el del token y va por otro
          parametro, en otro procedimiento.
        */
        var equipoUnico = $"{FiltrosLegacy.Texto(equipo, 60)}-{Guid.NewGuid():N}";

        var filas = await EjecutarFilasAsync(SpActualizarParametro, cmd =>
        {
            cmd.Parameters.Add(Varchar("@NombreEquipo", LargoNombreEquipo, equipoUnico));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Entero("@IDReporte", 0));

            for (var i = 0; i < 20; i++)
            {
                var valor = i < parametros.Count ? parametros[i] ?? string.Empty : string.Empty;
                cmd.Parameters.Add(Varchar($"@Param{i + 1}", LargoParam, valor));
            }
        }, ct);

        return filas.Count > 0 ? Entero(filas[0], "ID") : 0;
    }

    public async Task<ResultadoLegacyDto> InsertarFacturaAsync(
        FacturaTimbrarRequest request,
        FacturaTimbradaDto timbrada,
        int idParametros,
        int idVenta,
        int folio,
        string serie,
        decimal totalFacturadoVenta,
        int tipo,
        DateTime? fechaPago,
        string fechaPagoSat,
        int numComplementosPago,
        int idVentaFacturaRelacionada,
        int idUsuario,
        string equipo,
        CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpInsertarFactura, cmd =>
        {
            /* @Params es el ID del renglon de parametros, no una cadena. */
            cmd.Parameters.Add(Varchar("@Params", LargoParam, idParametros.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            cmd.Parameters.Add(Entero("@IDVenta", idVenta));
            cmd.Parameters.Add(Entero("@IDEmpresa", request.IdEmpresaCs > 0 ? request.IdEmpresaCs : request.IdEmpresa));
            cmd.Parameters.Add(Entero("@IDClienteFacturacion", request.IdClienteFacturacion));
            cmd.Parameters.Add(Entero("@IDCliente", request.IdCliente));
            cmd.Parameters.Add(Entero("@Folio", folio));
            cmd.Parameters.Add(Varchar("@Serie", LargoCinco, Recortar(serie, LargoCinco)));
            cmd.Parameters.Add(Varchar("@CodigoPostal", LargoCinco, Recortar(request.Receptor.CodigoPostal, LargoCinco)));
            cmd.Parameters.Add(Varchar("@IDFERelacionFactura", LargoDiez, Recortar(ReglasFacturacion.ClaveSat(request.RelacionFactura), LargoDiez)));
            cmd.Parameters.Add(Varchar("@IDFEMetodoPago", LargoCien, ReglasFacturacion.ClaveSat(request.MetodoPago)));
            cmd.Parameters.Add(Varchar("@IDFEFormaPago", LargoCien, ReglasFacturacion.FormaPagoSat(request.FormaPago)));
            cmd.Parameters.Add(Varchar("@IDFERegimenFiscal", LargoCien, ReglasFacturacion.ClaveSat(request.RegimenFiscal)));
            cmd.Parameters.Add(Varchar("@IDFEUsoCFDI", LargoCien, ReglasFacturacion.ClaveSat(request.UsoCfdi)));
            cmd.Parameters.Add(Varchar("@CreatedAt", LargoCien, ReglasFacturacion.FechaHoraCfdi(timbrada.FechaCreacion)));
            cmd.Parameters.Add(Varchar("@FacturaDate", LargoCien, ReglasFacturacion.FechaHoraCfdi(timbrada.FechaComprobante)));
            cmd.Parameters.Add(Varchar("@IssueDate", LargoCien, ReglasFacturacion.FechaHoraCfdi(timbrada.FechaComprobante)));
            cmd.Parameters.Add(Varchar("@Status", 1000, timbrada.Status ?? string.Empty));
            cmd.Parameters.Add(Varchar("@CancellationStatus", 1000, timbrada.CancellationStatus ?? string.Empty));
            cmd.Parameters.Add(Decimal18("@TotalPagar", timbrada.Total));
            /* @TotalPagarFactura recibe el MISMO valor que @TotalPagar, igual que
               en legacy: es el total del CFDI visto desde los dos lados. */
            cmd.Parameters.Add(Decimal18("@TotalPagarFactura", timbrada.Total));
            cmd.Parameters.Add(Decimal18("@TotalFacturarDinero", request.TotalFacturarDinero));
            cmd.Parameters.Add(Decimal18("@TotalFacturarCascos", request.TotalFacturarCascos));
            cmd.Parameters.Add(Decimal18("@TotalFacturarDineroCascos", request.TotalFacturarDineroCascos));
            cmd.Parameters.Add(Decimal18("@TotalFacturadoVenta", totalFacturadoVenta));
            cmd.Parameters.Add(Entero("@TotalProductos", request.TotalProductos));
            cmd.Parameters.Add(Entero("@TotalUsados", request.TotalUsados));
            cmd.Parameters.Add(Decimal18("@Descuento", request.Descuento));
            cmd.Parameters.Add(Decimal18("@Tipo", tipo));
            cmd.Parameters.Add(Entero("@IDTipoNotaCredito", request.IdTipoNotaCredito));
            cmd.Parameters.Add(Entero("@IDPedido", request.IdPedido));
            cmd.Parameters.Add(Entero("@IDVentaFacturaRelacionada", idVentaFacturaRelacionada));
            cmd.Parameters.Add(Varchar("@FechaPago", LargoCien, ReglasFacturacion.FechaHoraCfdi(fechaPago)));
            cmd.Parameters.Add(Varchar("@FechaPagoSat", LargoCien, fechaPagoSat));
            cmd.Parameters.Add(Entero("@IncluirImportes", request.IncluirImportes));
            cmd.Parameters.Add(Entero("@NumComplementosPago", numComplementosPago));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoCien, FiltrosLegacy.Texto(equipo, LargoCien)));
        }, ct);

        return Resultado(tablas, "IDResult");
    }

    public async Task<ResultadoLegacyDto> CancelarFacturaAsync(
        string idFacturapi, int idStatus, string? motivoSat, string? motivo,
        int statusCancelacion, string? status, string? cancellationStatus,
        DateTime? fechaCancelacionSat, int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpCancelarFactura, cmd =>
        {
            cmd.Parameters.Add(Varchar("@IDFacturapi", LargoCien, idFacturapi));
            cmd.Parameters.Add(Entero("@IDStatus", idStatus));
            cmd.Parameters.Add(Varchar("@IDFEMotivoCancelacion", LargoCien, ReglasFacturacion.ClaveSat(motivoSat)));
            cmd.Parameters.Add(Varchar("@MotivoCancelacion", LargoParam, FiltrosLegacy.Texto(motivo)));
            cmd.Parameters.Add(Entero("@StatusCancelacion", statusCancelacion));
            cmd.Parameters.Add(Varchar("@Status", LargoCien, status ?? string.Empty));
            cmd.Parameters.Add(Varchar("@CancellationStatus", LargoCien, cancellationStatus ?? string.Empty));
            cmd.Parameters.Add(Varchar("@FechaCancelacionSat", LargoCien, ReglasFacturacion.FechaHoraCfdi(fechaCancelacionSat)));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoCien, FiltrosLegacy.Texto(equipo, LargoCien)));
        }, ct);

        return Resultado(tablas, "IDResult");
    }

    public async Task<FilasLegacyDto> ConsultarAsync(FacturaConsultaRequest request, CancellationToken ct)
    {
        var filas = await EjecutarFilasAsync(SpConsultarFacturas, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDVentaFactura", request.IdVentaFactura));
            cmd.Parameters.Add(Varchar("@IDsVentaFactura", LargoLista, FiltrosLegacy.Ids(request.IdsVentaFactura)));
            cmd.Parameters.Add(Entero("@IDVentaFacturaRelacionada", request.IdVentaFacturaRelacionada));
            cmd.Parameters.Add(Varchar("@IDFacturapi", 1000, FiltrosLegacy.Texto(request.IdFacturapi, 1000)));
            cmd.Parameters.Add(Varchar("@IDsVenta", LargoLista, FiltrosLegacy.Ids(request.IdsVenta)));
            cmd.Parameters.Add(Entero("@IDCliente", request.IdCliente));
            cmd.Parameters.Add(Entero("@IDAgente", request.IdAgente));
            cmd.Parameters.Add(Varchar("@IDsUsuario", LargoLista, FiltrosLegacy.Ids(request.IdsUsuario)));
            cmd.Parameters.Add(Entero("@IDStatus", request.IdStatus));
            cmd.Parameters.Add(Entero("@FolioInicial", request.FolioInicial));
            cmd.Parameters.Add(Entero("@FolioFinal", request.FolioFinal));
            cmd.Parameters.Add(Varchar("@FechaInicial", LargoFecha, FiltrosLegacy.FechaDesde(request.FechaInicial)));
            cmd.Parameters.Add(Varchar("@FechaFinal", LargoFecha, FiltrosLegacy.FechaHasta(request.FechaFinal)));
            cmd.Parameters.Add(Entero("@Tipo", request.Tipo));
            cmd.Parameters.Add(Entero("@Formato", request.Formato));
            cmd.Parameters.Add(Entero("@Identico", request.Identico));
            cmd.Parameters.Add(Entero("@IDEmpresaCS", request.IdEmpresaCs));
            cmd.Parameters.Add(Varchar("@IDsClientes", LargoLista, FiltrosLegacy.Ids(request.IdsCliente)));
        }, ct);

        return new FilasLegacyDto { Rows = filas };
    }

    public async Task<FacturaGuardada?> LeerFacturaAsync(string idFacturapi, CancellationToken ct)
    {
        var datos = await ConsultarAsync(new FacturaConsultaRequest
        {
            IdFacturapi = idFacturapi,
            /* Tipo 0 = factura: el complemento se aplica a facturas, no a notas. */
            Tipo = 0
        }, ct);

        if (datos.Rows.Count == 0) return null;
        var fila = datos.Rows[0];

        return new FacturaGuardada
        {
            IdVentaFactura = Entero(fila, "IDVentaFactura"),
            IdFacturapi = Texto(fila, "IDFacturapi"),
            Uuid = Texto(fila, "uuid"),
            NumeroPagos = Entero(fila, "NumeroPagos"),
            Saldo = Importe(fila, "Saldo")
        };
    }

    public async Task<CorreoFactura?> LeerDatosCorreoAsync(int idVentaFactura, CancellationToken ct)
    {
        /*
          UN SOLO PARAMETRO CON EL FORMATO "@<id>".

          El procedimiento declara @Params varchar(4000) y espera literalmente el
          caracter arroba seguido del id. No es un error de legacy: es la convencion
          de sus reportes, donde la arroba marca el inicio de cada valor.
        */
        var filas = await EjecutarFilasAsync(SpReporteFactura, cmd =>
        {
            cmd.Parameters.Add(Varchar("@Params", LargoParam,
                "@" + idVentaFactura.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }, ct);

        if (filas.Count == 0) return null;
        var fila = filas[0];

        var archivo = Texto(fila, "Archivo");
        return new CorreoFactura
        {
            /* El reporte guarda el .zip; el PDF es el mismo nombre con otra extension. */
            Archivo = archivo.Replace(".zip", ".pdf", StringComparison.OrdinalIgnoreCase),
            IdFacturapi = Texto(fila, "IDFacturapi"),
            Remitente = Texto(fila, "FacturasCorreo"),
            RemitenteNombre = Texto(fila, "FacturasCorreoNombre"),
            Smtp = Texto(fila, "FacturasCorreoSmtp"),
            Puerto = Entero(fila, "FacturasCorreoPuerto"),
            Usuario = Texto(fila, "FacturasCorreoUsuario"),
            Contrasena = Texto(fila, "FacturasCorreoContrasena"),
            Asunto = Texto(fila, "FacturasCorreoAsunto"),
            Cuerpo = Texto(fila, "FacturasCorreoCuerpo"),
            ConCopia = Texto(fila, "FacturasCorreoConCopia")
        };
    }

    private static string Recortar(string? valor, int largo)
    {
        var texto = valor ?? string.Empty;
        return texto.Length <= largo ? texto : texto[..largo];
    }

    private static decimal Importe(Dictionary<string, object?> fila, string columna)
    {
        if (!fila.TryGetValue(columna, out var valor) || valor is null) return 0m;
        return valor switch
        {
            decimal d => d,
            double db => (decimal)db,
            int i => i,
            long l => l,
            string t => decimal.TryParse(t, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var n) ? n : 0m,
            _ => 0m
        };
    }

    private static ResultadoLegacyDto Resultado(
        List<List<Dictionary<string, object?>>> tablas, string columnaId)
    {
        var dto = new ResultadoLegacyDto();
        if (tablas.Count == 0) return dto;

        dto.Filas = tablas[0];
        for (var i = 1; i < tablas.Count; i++) dto.TablasExtra.Add(tablas[i]);

        var primera = dto.Filas.Count > 0 ? dto.Filas[0] : null;
        if (primera is null) return dto;

        if (primera.ContainsKey("result")) dto.Resultado = Entero(primera, "result");
        if (primera.ContainsKey("mensaje")) dto.Mensaje = Texto(primera, "mensaje");
        if (primera.ContainsKey(columnaId)) dto.Id = Entero(primera, columnaId);

        return dto;
    }
}
