using ISL_Service.Application.DTOs.VentasCobranzaOperaciones;
using ISL_Service.Application.DTOs.VentasEscrituras;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Infrastructure.Repositories;

/*
  COBRANZA, CONCENTRADO Y COBRO CONTRA LOS MISMOS PROCEDIMIENTOS DE MAC31

  Tres cosas que estaban en C# y aqui cambian, todas con motivo:

  1) EL PREFIJO "0" DE LOS FOLIOS. Legacy, en las dos consultas, hace
     `FolioInicialCobranza = "0" + FolioInicialCobranza` antes de llamar. El
     procedimiento declara ese parametro como INT, asi que "0"+"123" entra como
     123 y ""+"0" entra como 0: el prefijo es un parche para que una cadena vacia
     no reviente la conversion. Aqui el folio es un int y 0 significa "sin
     filtro", que es lo mismo sin el truco. (Efecto colateral curioso del parche:
     en la consulta de cobros los folios de venta ya venian en "0" por defecto, y
     el prefijo los dejaba en "00".)

  2) LA RAMA MUERTA. En las dos consultas legacy tiene
     `if (FolioInicial != "0") ... else if (FolioInicial != "0") ...`: la segunda
     condicion repite la primera, asi que la rama "buscar por folio de VENTA
     exclusivamente" nunca corre. No se copia el defecto, pero tampoco se
     resucita la rama: cada endpoint recibe los campos que su procedimiento usa
     y el servicio decide la prioridad con nombre.

  3) EL IDUSUARIOACTUAL. En legacy viaja en el cuerpo y el procedimiento lo usa
     para resolver la columna de acceso por almacen. Que viaje en el cuerpo
     significa que cualquiera puede pedir la consulta con el id de otro y ver lo
     que ese otro ve. Aqui sale del token.

  Las listas de ids van pegadas con VIRGULILLA, no con coma. Comprobado en el
  cuerpo de los procedimientos: todos declaran @Delimitador y le asignan '~'.
*/
public class VentasCobranzaOperacionesRepository : SpLegacyRepositoryBase, IVentasCobranzaOperacionesRepository
{
    private const string SpCrearCobranza = "sp_n_InsertarCobranzaDetalle";
    private const string SpActualizarCobranza = "sp_n_ActualizarCobranzaDetalle";
    private const string SpCancelarCobranza = "sp_n_CancelarCobranza";
    private const string SpConsultarCobranzas = "sp_n_ConsultarCobranzas";
    private const string SpCrearConcentrado = "sp_n_InsertarConcentradoDetalle";
    private const string SpActualizarConcentrado = "sp_n_ActualizarConcentradoDetalle";
    private const string SpCancelarConcentrado = "sp_n_CancelarConcentrado";
    private const string SpAbrirCobro = "sp_n_AbrirCobro";
    private const string SpCerrarCobro = "sp_n_CerrarCobro";
    private const string SpCancelarCobro = "sp_n_CancelarCobro";
    private const string SpActualizarCobro = "sp_n_ActualizarCobro";
    private const string SpConsultarCobros = "sp_n_ConsultarCobros";
    private const string SpConsultarCobroClientes = "sp_n_ConsultarCobroClientes";
    private const string SpPendientesVencidos = "sp_n_rptVentasSaldosPrincipal";

    private const int LargoLista = 4000;
    private const int LargoTexto = 4000;
    private const int LargoEquipoCorto = 400;
    private const int LargoEquipoLargo = 2000;
    private const int LargoFecha30 = 30;
    private const int LargoFecha100 = 100;

    public VentasCobranzaOperacionesRepository(IConfiguration configuration) : base(configuration) { }

    /* ---------------------------- Cobranza ---------------------------- */

    public async Task<ResultadoLegacyDto> CrearCobranzaAsync(
        CobranzaCrearRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpCrearCobranza, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDRepartidor", request.IdRepartidor));
            cmd.Parameters.Add(Varchar("@IDsVentaAgregar", LargoLista, FiltrosLegacy.Ids(request.IdsVentaAgregar)));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipoCorto, FiltrosLegacy.Texto(equipo, LargoEquipoCorto)));
            cmd.Parameters.Add(Varchar("@Observaciones", LargoTexto, FiltrosLegacy.Texto(request.Observaciones)));
            cmd.Parameters.Add(Entero("@IDCobro", request.IdCobro));
            cmd.Parameters.Add(Entero("@esCobro", request.EsCobro));
            cmd.Parameters.Add(Entero("@IDAlmacen", request.IdAlmacen));
        }, ct);

        /*
          EL ID QUE DEVUELVE SON TRES, Y LEGACY LOS PEGABA CON PIPE.

          El procedimiento contesta IDCobranza, IDCobro y FolioCobro en columnas
          separadas, y el repositorio de legacy los concatena en un solo campo
          `IDResult` con "|" para que quepan en su DTO generico. El cliente
          despues parte la cadena por posicion. Aqui no se pega nada: las tres
          columnas bajan como columnas en `filas`, y `id` apunta a la cobranza.
          Un campo "a|b|c" obliga a que las dos mitades esten de acuerdo sobre el
          orden, y nadie recuerda el orden de un pipe.
        */
        return Resultado(tablas, "IDCobranza");
    }

    public async Task<ResultadoLegacyDto> ActualizarCobranzaAsync(
        CobranzaActualizarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpActualizarCobranza, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDCobranza", request.IdCobranza));
            cmd.Parameters.Add(Varchar("@IDsCobranzaDetalleEliminar", LargoLista, FiltrosLegacy.Ids(request.IdsDetalleEliminar)));
            cmd.Parameters.Add(Varchar("@IDsCobranzaDetalleDevolver", LargoLista, FiltrosLegacy.Ids(request.IdsDetalleDevolver)));
            cmd.Parameters.Add(Varchar("@IDsVentaAgregar", LargoLista, FiltrosLegacy.Ids(request.IdsVentaAgregar)));
            cmd.Parameters.Add(Entero("@IDRepartidor", request.IdRepartidor));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipoCorto, FiltrosLegacy.Texto(equipo, LargoEquipoCorto)));
            cmd.Parameters.Add(Varchar("@Observaciones", LargoTexto, FiltrosLegacy.Texto(request.Observaciones)));
        }, ct);

        return Resultado(tablas, "IDCobranza");
    }

    public async Task<ResultadoLegacyDto> CancelarCobranzaAsync(
        CobranzaCancelarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        /* Los parametros se llaman @IDUsuarioCancelo y @EquipoCancelo: el mismo
           dato del token, con el nombre que le puso quien escribio el
           procedimiento. */
        var tablas = await EjecutarTablasAsync(SpCancelarCobranza, cmd =>
        {
            cmd.Parameters.Add(Varchar("@IDsCobranza", LargoLista, FiltrosLegacy.Ids(request.IdsCobranza)));
            cmd.Parameters.Add(Entero("@IDUsuarioCancelo", idUsuario));
            cmd.Parameters.Add(Varchar("@EquipoCancelo", LargoEquipoLargo, FiltrosLegacy.Texto(equipo, LargoEquipoLargo)));
            cmd.Parameters.Add(Varchar("@IDsCobro", LargoLista, FiltrosLegacy.Ids(request.IdsCobro)));
        }, ct);

        return Resultado(tablas, "IDCobranza");
    }

    public async Task<FilasLegacyDto> ConsultarCobranzasAsync(
        CobranzaConsultaRequest request, int idUsuarioActual, CancellationToken ct)
    {
        var filas = await EjecutarFilasAsync(SpConsultarCobranzas, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDCobranza", request.IdCobranza));
            cmd.Parameters.Add(Entero("@IDRepartidor", request.IdRepartidor));
            cmd.Parameters.Add(Entero("@IDUsuarioCobranza", request.IdUsuarioCobranza));
            cmd.Parameters.Add(Entero("@FolioInicialCobranza", request.FolioInicialCobranza));
            cmd.Parameters.Add(Entero("@FolioFinalCobranza", request.FolioFinalCobranza));
            cmd.Parameters.Add(Varchar("@FechaInicialCobranza", LargoFecha30, FiltrosLegacy.FechaDesde(request.FechaInicialCobranza)));
            cmd.Parameters.Add(Varchar("@FechaFinalCobranza", LargoFecha30, FiltrosLegacy.FechaHasta(request.FechaFinalCobranza)));
            cmd.Parameters.Add(Entero("@IDEmpresaVenta", request.IdEmpresaVenta));
            cmd.Parameters.Add(Entero("@FolioInicialVenta", request.FolioInicialVenta));
            cmd.Parameters.Add(Entero("@FolioFinalVenta", request.FolioFinalVenta));
            cmd.Parameters.Add(Entero("@IDUsuarioActual", idUsuarioActual));
        }, ct);

        return new FilasLegacyDto { Rows = filas };
    }

    /* --------------------------- Concentrado --------------------------- */

    public async Task<ResultadoLegacyDto> CrearConcentradoAsync(
        ConcentradoCrearRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpCrearConcentrado, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDRepartidor", request.IdRepartidor));
            cmd.Parameters.Add(Varchar("@IDsVentaAgregar", LargoLista, FiltrosLegacy.Ids(request.IdsVentaAgregar)));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipoCorto, FiltrosLegacy.Texto(equipo, LargoEquipoCorto)));
            cmd.Parameters.Add(Varchar("@Observaciones", LargoTexto, FiltrosLegacy.Texto(request.Observaciones)));
            cmd.Parameters.Add(Entero("@IDCobro", request.IdCobro));
            cmd.Parameters.Add(Entero("@esCobro", request.EsCobro));
            cmd.Parameters.Add(Entero("@IDAlmacen", request.IdAlmacen));
            cmd.Parameters.Add(Entero("@SoloDevueltas", request.SoloDevueltas));
        }, ct);

        return Resultado(tablas, "IDConcentrado");
    }

    public async Task<ResultadoLegacyDto> ActualizarConcentradoAsync(
        ConcentradoActualizarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpActualizarConcentrado, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDConcentrado", request.IdConcentrado));
            cmd.Parameters.Add(Varchar("@IDsConcentradoDetalleEliminar", LargoLista, FiltrosLegacy.Ids(request.IdsDetalleEliminar)));
            cmd.Parameters.Add(Varchar("@IDsVentaAgregar", LargoLista, FiltrosLegacy.Ids(request.IdsVentaAgregar)));
            cmd.Parameters.Add(Entero("@IDRepartidor", request.IdRepartidor));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipoCorto, FiltrosLegacy.Texto(equipo, LargoEquipoCorto)));
            cmd.Parameters.Add(Varchar("@Observaciones", LargoTexto, FiltrosLegacy.Texto(request.Observaciones)));
        }, ct);

        return Resultado(tablas, "IDConcentrado");
    }

    public async Task<ResultadoLegacyDto> CancelarConcentradoAsync(
        ConcentradoCancelarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpCancelarConcentrado, cmd =>
        {
            cmd.Parameters.Add(Varchar("@IDsConcentrado", LargoLista, FiltrosLegacy.Ids(request.IdsConcentrado)));
            cmd.Parameters.Add(Entero("@IDUsuarioCancelo", idUsuario));
            cmd.Parameters.Add(Varchar("@EquipoCancelo", LargoEquipoLargo, FiltrosLegacy.Texto(equipo, LargoEquipoLargo)));
            cmd.Parameters.Add(Varchar("@IDsCobro", LargoLista, FiltrosLegacy.Ids(request.IdsCobro)));
        }, ct);

        return Resultado(tablas, "IDConcentrado");
    }

    /* ------------------------------ Cobro ------------------------------ */

    public async Task<ResultadoLegacyDto> AbrirCobroAsync(
        CobroLoteRequest request, int idUsuario, string equipo, CancellationToken ct)
        => await CobroLoteAsync(SpAbrirCobro, request, conObservaciones: false, idUsuario, equipo, ct);

    public async Task<ResultadoLegacyDto> CancelarCobroAsync(
        CobroLoteRequest request, int idUsuario, string equipo, CancellationToken ct)
        => await CobroLoteAsync(SpCancelarCobro, request, conObservaciones: false, idUsuario, equipo, ct);

    public async Task<ResultadoLegacyDto> CerrarCobroAsync(
        CobroLoteRequest request, int idUsuario, string equipo, CancellationToken ct)
        => await CobroLoteAsync(SpCerrarCobro, request, conObservaciones: true, idUsuario, equipo, ct);

    /// <summary>
    /// Abrir, cerrar y cancelar comparten los cuatro primeros parametros y solo
    /// cerrar lleva @Observaciones, en TERCER lugar. Van por nombre, asi que la
    /// posicion no importa para SQL Server, pero el parametro de mas si: el de
    /// abrir y el de cancelar no lo declaran y mandarselo seria un error.
    /// </summary>
    private async Task<ResultadoLegacyDto> CobroLoteAsync(
        string procedimiento, CobroLoteRequest request, bool conObservaciones,
        int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(procedimiento, cmd =>
        {
            cmd.Parameters.Add(Varchar("@IDsCobro", LargoLista, FiltrosLegacy.Ids(request.IdsCobro)));
            cmd.Parameters.Add(Varchar("@FoliosCobro", LargoLista, FiltrosLegacy.Ids(request.FoliosCobro)));
            if (conObservaciones)
                cmd.Parameters.Add(Varchar("@Observaciones", LargoTexto, FiltrosLegacy.Texto(request.Observaciones)));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipoLargo, FiltrosLegacy.Texto(equipo, LargoEquipoLargo)));
        }, ct);

        return Resultado(tablas, "IDCobro");
    }

    public async Task<ResultadoLegacyDto> ActualizarCobroAsync(
        CobroActualizarRequest request, CancellationToken ct)
    {
        /*
          ESTE NO RECIBE NI USUARIO NI EQUIPO, Y NO ES UN OLVIDO NUESTRO.

          sp_n_ActualizarCobro declara cuatro parametros y ninguno es de firma.
          Legacy tampoco los manda: su controlador ni valida IDUsuario aqui. No
          se le inventan: un parametro que el procedimiento no declara hace
          fallar la llamada entera.
        */
        var tablas = await EjecutarTablasAsync(SpActualizarCobro, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDCobro", request.IdCobro));
            cmd.Parameters.Add(Entero("@IDConcentrado", request.IdConcentrado));
            cmd.Parameters.Add(Entero("@IDCobranza", request.IdCobranza));
            cmd.Parameters.Add(Varchar("@Observaciones", LargoTexto, FiltrosLegacy.Texto(request.Observaciones)));
        }, ct);

        return Resultado(tablas, "IDCobro");
    }

    public async Task<FilasLegacyDto> ConsultarCobrosAsync(
        CobroConsultaRequest request, int idUsuarioActual, CancellationToken ct)
    {
        var filas = await EjecutarFilasAsync(SpConsultarCobros, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDCobro", request.IdCobro));
            cmd.Parameters.Add(Entero("@IDCliente", request.IdCliente));
            cmd.Parameters.Add(Entero("@IDRepartidor", request.IdRepartidor));
            cmd.Parameters.Add(Entero("@IDUsuarioCobro", request.IdUsuarioCobro));
            cmd.Parameters.Add(Entero("@FolioInicialCobro", request.FolioInicialCobro));
            cmd.Parameters.Add(Entero("@FolioFinalCobro", request.FolioFinalCobro));
            cmd.Parameters.Add(Varchar("@FechaInicialCobro", LargoFecha30, FiltrosLegacy.FechaDesde(request.FechaInicialCobro)));
            cmd.Parameters.Add(Varchar("@FechaFinalCobro", LargoFecha30, FiltrosLegacy.FechaHasta(request.FechaFinalCobro)));
            cmd.Parameters.Add(Entero("@IDEmpresaVenta", request.IdEmpresaVenta));
            cmd.Parameters.Add(Entero("@FolioInicialVenta", request.FolioInicialVenta));
            cmd.Parameters.Add(Entero("@FolioFinalVenta", request.FolioFinalVenta));
            cmd.Parameters.Add(Entero("@IDUsuarioActual", idUsuarioActual));
        }, ct);

        return new FilasLegacyDto { Rows = filas };
    }

    public async Task<FilasLegacyDto> ConsultarCobroClientesAsync(int idCobro, CancellationToken ct)
    {
        var filas = await EjecutarFilasAsync(SpConsultarCobroClientes, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDCobro", idCobro));
        }, ct);

        return new FilasLegacyDto { Rows = filas };
    }

    /* --------------------- Pendientes y vencidos --------------------- */

    public async Task<FilasLegacyDto> ConsultarPendientesVencidosAsync(
        CobranzaPendientesVencidosRequest request, CancellationToken ct)
    {
        /*
          LAS SIETE CONSTANTES SON DE LEGACY, Y SE DEJAN EN EL MISMO VALOR.

          El servicio de legacy pisa siete de los catorce parametros justo antes
          de llamar, sin importar lo que mande el cliente:

            Status          = 4   pendientes Y vencidos
            Formato         = 1   detallado
            IDTipoDocumento = 0   todos
            IDAlmacen       = 0   todos
            Mostrar         = 0   contado y credito
            Criterio        = 0   todos, no solo los anteriores a la semana
            DiasVencimiento = 0   todos

          Son los valores que definen QUE es esta pantalla: "la cobranza
          pendiente y vencida, al detalle". Se escriben aqui, con su significado
          al lado, en vez de dejarlos como numeros sueltos en un request que el
          cliente cree que puede cambiar.

          La fecha de operacion lleva 23:59:59 —no 00:00:00— porque es un corte
          de saldo: el saldo "al dia 5" incluye todo lo del dia 5.
        */
        const int StatusPendientesYVencidos = 4;
        const int FormatoDetallado = 1;
        const int TodosLosTiposDeDocumento = 0;
        const int TodosLosAlmacenes = 0;
        const int ContadoYCredito = 0;
        const int SinCriterioDeSemana = 0;
        const int TodosLosDiasVencidos = 0;

        var filas = await EjecutarFilasAsync(SpPendientesVencidos, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDEmpresa", request.IdEmpresa));
            cmd.Parameters.Add(Varchar("@FechaOperacion", LargoFecha100, FiltrosLegacy.FechaHasta(request.FechaOperacion)));
            cmd.Parameters.Add(Varchar("@FechaInicial", LargoFecha100, FiltrosLegacy.FechaDesde(request.FechaInicial)));
            cmd.Parameters.Add(Varchar("@FechaFinal", LargoFecha100, FiltrosLegacy.FechaHasta(request.FechaFinal)));
            cmd.Parameters.Add(Entero("@DiasVencimiento", TodosLosDiasVencidos));
            cmd.Parameters.Add(Entero("@FormatoMostrar", request.FormatoMostrar));
            cmd.Parameters.Add(Entero("@Status", StatusPendientesYVencidos));
            cmd.Parameters.Add(Entero("@Mostrar", ContadoYCredito));
            cmd.Parameters.Add(Entero("@Formato", FormatoDetallado));
            cmd.Parameters.Add(Entero("@IDAgente", request.IdAgente));
            cmd.Parameters.Add(Entero("@IDCliente", request.IdCliente));
            cmd.Parameters.Add(Entero("@IDTipoDocumento", TodosLosTiposDeDocumento));
            cmd.Parameters.Add(Entero("@IDAlmacen", TodosLosAlmacenes));
            cmd.Parameters.Add(Entero("@Criterio", SinCriterioDeSemana));
        }, ct);

        return new FilasLegacyDto { Rows = filas };
    }

    /* ---------------------------------------------------------------- */

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
