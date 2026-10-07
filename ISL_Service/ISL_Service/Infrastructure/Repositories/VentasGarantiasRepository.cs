using ISL_Service.Application.DTOs.VentasEscrituras;
using ISL_Service.Application.DTOs.VentasGarantias;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Infrastructure.Repositories;

/*
  GARANTIAS CONTRA LOS MISMOS PROCEDIMIENTOS DE MAC31

  Lo que estaba en C# y aqui cambia de sitio:

  1) @GarantiasInfo lo armaba el CLIENTE de WinForms. Ahora lo arma el servidor,
     con el mismo formato exacto (ver TextoDeBitacora mas abajo). Un texto de
     bitacora que depende de que cada cliente lo pegue igual no es un contrato:
     es una esperanza.

  2) LA FECHA DE INGRESO ES UN datetime DE VERDAD. Los dos procedimientos que la
     reciben la declaran `datetime`, no varchar — son los unicos de esta ola. Asi
     que toda la gimnasia de legacy (leer formatoFechaDB de Constantes y voltear
     la cadena con Substring) era innecesaria incluso para el: SqlClient manda un
     datetime sin pasar por texto. Y, de paso, ya no se llama a
     sp_n_ConsultaConstantes, que es el que hace el cierre del dia.

  3) EL RESULTADO SI SE LEE. En legacy, insertar/actualizar/registrar responden
     siempre `{IDResult:"", result:0, mensaje:""}`: el repositorio nunca llena
     esos campos (solo lo hace el de aceptar) y las filas que devolvio el
     procedimiento se tiran. O sea que la pantalla no se enteraba de un fallo con
     su explicacion. Aqui bajan las filas y, si traen result/mensaje, se leen.
*/
public class VentasGarantiasRepository : SpLegacyRepositoryBase, IVentasGarantiasRepository
{
    private const string SpCrear = "sp_n_InsertarGarantia";
    private const string SpActualizar = "sp_n_ActualizarGarantia";
    private const string SpCancelar = "sp_n_CancelarGarantia";
    private const string SpConsultar = "sp_n_ConsultarGarantias";
    private const string SpAceptar = "sp_n_GarantiasAceptar";
    private const string SpConsultarAceptar = "sp_n_ConsultarGarantiasAceptar";
    private const string SpCrearRegistro = "sp_n_InsertarGarantiaRegistro";
    private const string SpCancelarRegistro = "sp_n_CancelarGarantiaRegistro";
    private const string SpConsultarRegistro = "sp_n_ConsultarGarantiasRegistro";

    private const int LargoLista = 4000;
    private const int LargoTexto = 4000;
    private const int LargoCampo = 400;
    private const int LargoEquipo = 1000;
    private const int LargoFecha = 30;

    public VentasGarantiasRepository(IConfiguration configuration) : base(configuration) { }

    public async Task<ResultadoLegacyDto> CrearAsync(
        GarantiaCrearRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpCrear, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDEmpresa", request.IdEmpresa));
            cmd.Parameters.Add(Entero("@IDCliente", request.IdCliente));
            cmd.Parameters.Add(Entero("@IDRepartidor", request.IdRepartidor));
            cmd.Parameters.Add(Entero("@IDAlmacen", request.IdAlmacen));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Entero("@IDProducto", request.IdProducto));
            cmd.Parameters.Add(Fecha("@FechaIngreso", request.FechaIngreso));
            cmd.Parameters.Add(Varchar("@Ticket", LargoCampo, FiltrosLegacy.Texto(request.Ticket, LargoCampo)));
            cmd.Parameters.Add(Varchar("@Vigencia", LargoCampo, FiltrosLegacy.Texto(request.Vigencia, LargoCampo)));
            cmd.Parameters.Add(Varchar("@Garantia", LargoCampo, FiltrosLegacy.Texto(request.Garantia, LargoCampo)));
            cmd.Parameters.Add(Varchar("@Motivo", LargoTexto, FiltrosLegacy.Texto(request.Motivo)));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipo, FiltrosLegacy.Texto(equipo, LargoEquipo)));
            cmd.Parameters.Add(Entero("@GarantiaMayorista", request.GarantiaMayorista));
            cmd.Parameters.Add(Entero("@IDEmpresaMayorista", request.IdEmpresaMayorista));
            cmd.Parameters.Add(Entero("@IDProductoCambio", request.IdProductoCambio));
        }, ct);

        return Resultado(tablas, "IDGarantia");
    }

    public async Task<ResultadoLegacyDto> ActualizarAsync(
        GarantiaActualizarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpActualizar, cmd =>
        {
            cmd.Parameters.Add(Varchar("@IDsGarantia", LargoLista, FiltrosLegacy.Ids(request.IdsGarantia)));
            cmd.Parameters.Add(Varchar("@IDsVenta", LargoLista, FiltrosLegacy.Ids(request.IdsVenta)));
            cmd.Parameters.Add(Varchar("@GarantiasInfo", LargoTexto, TextoDeBitacora(request.Partidas)));
            cmd.Parameters.Add(Entero("@IDStatusGarantia", request.IdStatusGarantia));
            cmd.Parameters.Add(Entero("@IDCliente", request.IdCliente));
            cmd.Parameters.Add(Entero("@IDProducto", request.IdProducto));
            cmd.Parameters.Add(Entero("@IDRepartidor", request.IdRepartidor));
            cmd.Parameters.Add(Fecha("@FechaIngreso", request.FechaIngreso));
            cmd.Parameters.Add(Varchar("@Ticket", LargoCampo, FiltrosLegacy.Texto(request.Ticket, LargoCampo)));
            cmd.Parameters.Add(Varchar("@Vigencia", LargoCampo, FiltrosLegacy.Texto(request.Vigencia, LargoCampo)));
            cmd.Parameters.Add(Varchar("@Garantia", LargoCampo, FiltrosLegacy.Texto(request.Garantia, LargoCampo)));
            cmd.Parameters.Add(Varchar("@Motivo", LargoTexto, FiltrosLegacy.Texto(request.Motivo)));
            cmd.Parameters.Add(Decimal18("@Porcentaje", request.Porcentaje, 4));
            cmd.Parameters.Add(Decimal18("@Monto", request.Monto));
            cmd.Parameters.Add(Varchar("@Diagnostico", LargoTexto, FiltrosLegacy.Texto(request.Diagnostico)));
            cmd.Parameters.Add(Varchar("@FolioEnviadaProveedor", LargoTexto, FiltrosLegacy.Texto(request.FolioEnviadaProveedor)));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipo, FiltrosLegacy.Texto(equipo, LargoEquipo)));
            cmd.Parameters.Add(Entero("@IDProductoCambio", request.IdProductoCambio));
            cmd.Parameters.Add(Entero("@IDProductoCarga", request.IdProductoCarga));
            cmd.Parameters.Add(Entero("@IDPedidoPagos", request.IdPedidoPagos));
        }, ct);

        return Resultado(tablas, "IDGarantia");
    }

    public async Task<ResultadoLegacyDto> CancelarAsync(
        GarantiaCancelarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpCancelar, cmd =>
        {
            cmd.Parameters.Add(Varchar("@IDsGarantia", LargoLista, FiltrosLegacy.Ids(request.IdsGarantia)));
            cmd.Parameters.Add(Varchar("@GarantiasInfo", LargoTexto, TextoDeBitacora(request.Partidas)));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipo, FiltrosLegacy.Texto(equipo, LargoEquipo)));
        }, ct);

        return Resultado(tablas, "IDGarantia");
    }

    public async Task<FilasLegacyDto> ConsultarAsync(
        GarantiaConsultaRequest request, int idUsuarioActual, CancellationToken ct)
    {
        /*
          ESTE NO RECIBE @IDStatusGarantiaAceptada, Y NO ES UN OLVIDO.

          El procedimiento NO lo declara: en el repositorio de legacy esa linea
          esta comentada. El campo si influye, pero antes de llamar: decide que
          filtros se mandan (con 1 se piden las pendientes y se tira el resto).
          Esa decision la toma el servicio.
        */
        var filas = await EjecutarFilasAsync(SpConsultar, cmd =>
        {
            AgregarFiltrosComunes(cmd, request, idUsuarioActual);
        }, ct);

        return new FilasLegacyDto { Rows = filas };
    }

    public async Task<ResultadoLegacyDto> AceptarAsync(
        GarantiaAceptarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        /*
          ESTE PROCEDIMIENTO ESCRIBE EN OTRA BASE.

          Arma con SQL dinamico un UPDATE contra la tabla Garantias de la base
          del centro de servicio, y el nombre de esa base lo resuelve el
          procedimiento a partir de @IDCentroServicio. Por eso el centro es
          obligatorio aqui (el servicio lo exige): con 0 el procedimiento no
          sabe a que base ir.

          No se usa BaseDelCentro para esto. BaseDelCentro reescribe la cadena de
          conexion de ESTE proceso, y lo que hace falta es que UNA instruccion
          dentro de una transaccion del procedimiento apunte a otro catalogo.
          Hacerlo desde aqui obligaria a abrir una segunda conexion y a dos
          transacciones distintas: el procedimiento ya lo resuelve en una.
        */
        var tablas = await EjecutarTablasAsync(SpAceptar, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDGarantia", request.IdGarantia));
            cmd.Parameters.Add(Entero("@IDCentroServicio", request.IdCentroServicio));
            cmd.Parameters.Add(Varchar("@Observaciones", LargoTexto, FiltrosLegacy.Texto(request.Observaciones)));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipo, FiltrosLegacy.Texto(equipo, LargoEquipo)));
        }, ct);

        return Resultado(tablas, "IDResult");
    }

    public async Task<FilasLegacyDto> ConsultarAceptarAsync(
        GarantiaConsultaRequest request, int idUsuarioActual, CancellationToken ct)
    {
        var filas = await EjecutarFilasAsync(SpConsultarAceptar, cmd =>
        {
            AgregarFiltrosComunes(cmd, request, idUsuarioActual);
            /* El unico parametro de mas respecto a ConsultarGarantias, y el que
               hace que esta pantalla exista: 1 pendientes, 2 aceptadas. */
            cmd.Parameters.Add(Entero("@IDStatusGarantiaAceptada", request.IdStatusGarantiaAceptada));
        }, ct);

        return new FilasLegacyDto { Rows = filas };
    }

    public async Task<ResultadoLegacyDto> CrearRegistroAsync(
        GarantiaRegistroCrearRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpCrearRegistro, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDEmpresa", request.IdEmpresa));
            cmd.Parameters.Add(Entero("@IDProducto", request.IdProducto));
            cmd.Parameters.Add(Varchar("@Ticket", LargoCampo, FiltrosLegacy.Texto(request.Ticket, LargoCampo)));
            cmd.Parameters.Add(Varchar("@Vigencia", LargoCampo, FiltrosLegacy.Texto(request.Vigencia, LargoCampo)));
            cmd.Parameters.Add(Varchar("@Garantia", LargoCampo, FiltrosLegacy.Texto(request.Garantia, LargoCampo)));
            cmd.Parameters.Add(Varchar("@Observaciones", LargoTexto, FiltrosLegacy.Texto(request.Observaciones)));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipo, FiltrosLegacy.Texto(equipo, LargoEquipo)));
        }, ct);

        return Resultado(tablas, "IDGarantiaRegistro");
    }

    public async Task<ResultadoLegacyDto> CancelarRegistroAsync(
        GarantiaRegistroCancelarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpCancelarRegistro, cmd =>
        {
            /* El parametro se llama @IDsGarantia y lo que lleva son ids de
               REGISTRO. Es su nombre; cambiarlo seria tocar un sp_n_. */
            cmd.Parameters.Add(Varchar("@IDsGarantia", LargoLista, FiltrosLegacy.Ids(request.IdsRegistro)));
            cmd.Parameters.Add(Varchar("@GarantiasInfo", LargoTexto, TextoDeBitacora(request.Partidas)));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipo, FiltrosLegacy.Texto(equipo, LargoEquipo)));
        }, ct);

        return Resultado(tablas, "IDGarantiaRegistro");
    }

    public async Task<FilasLegacyDto> ConsultarRegistroAsync(
        GarantiaRegistroConsultaRequest request, CancellationToken ct)
    {
        var filas = await EjecutarFilasAsync(SpConsultarRegistro, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDGarantiaRegistro", request.IdGarantiaRegistro));
            cmd.Parameters.Add(Entero("@IDProducto", request.IdProducto));
            cmd.Parameters.Add(Entero("@IDStatus", request.IdStatus));
            cmd.Parameters.Add(Varchar("@FechaInicial", LargoFecha, FiltrosLegacy.FechaDesde(request.FechaInicial)));
            cmd.Parameters.Add(Varchar("@FechaFinal", LargoFecha, FiltrosLegacy.FechaHasta(request.FechaFinal)));
            cmd.Parameters.Add(Varchar("@Ticket", LargoCampo, FiltrosLegacy.Texto(request.Ticket, LargoCampo)));
            cmd.Parameters.Add(Varchar("@Vigencia", LargoCampo, FiltrosLegacy.Texto(request.Vigencia, LargoCampo)));
            cmd.Parameters.Add(Varchar("@Garantia", LargoCampo, FiltrosLegacy.Texto(request.Garantia, LargoCampo)));
            cmd.Parameters.Add(Varchar("@Folio", LargoCampo, FiltrosLegacy.Texto(request.Folio, LargoCampo)));
        }, ct);

        return new FilasLegacyDto { Rows = filas };
    }

    /* ---------------------------------------------------------------- */

    /// <summary>
    /// Los catorce filtros que comparten las dos consultas de garantias, en el
    /// mismo orden en que los registra legacy.
    /// </summary>
    private static void AgregarFiltrosComunes(
        Microsoft.Data.SqlClient.SqlCommand cmd, GarantiaConsultaRequest request, int idUsuarioActual)
    {
        cmd.Parameters.Add(Entero("@IDGarantia", request.IdGarantia));
        cmd.Parameters.Add(Entero("@IDCliente", request.IdCliente));
        cmd.Parameters.Add(Entero("@IDRepartidor", request.IdRepartidor));
        cmd.Parameters.Add(Entero("@IDProducto", request.IdProducto));
        cmd.Parameters.Add(Entero("@IDStatusGarantia", request.IdStatusGarantia));
        cmd.Parameters.Add(Varchar("@FechaInicial", LargoFecha, FiltrosLegacy.FechaDesde(request.FechaInicial)));
        cmd.Parameters.Add(Varchar("@FechaFinal", LargoFecha, FiltrosLegacy.FechaHasta(request.FechaFinal)));
        cmd.Parameters.Add(Varchar("@FechaIngresoInicial", LargoFecha, FiltrosLegacy.FechaDesde(request.FechaIngresoInicial)));
        cmd.Parameters.Add(Varchar("@FechaIngresoFinal", LargoFecha, FiltrosLegacy.FechaHasta(request.FechaIngresoFinal)));
        cmd.Parameters.Add(Varchar("@Ticket", LargoCampo, FiltrosLegacy.Texto(request.Ticket, LargoCampo)));
        cmd.Parameters.Add(Varchar("@Vigencia", LargoCampo, FiltrosLegacy.Texto(request.Vigencia, LargoCampo)));
        cmd.Parameters.Add(Varchar("@Garantia", LargoCampo, FiltrosLegacy.Texto(request.Garantia, LargoCampo)));
        cmd.Parameters.Add(Varchar("@Diagnostico", LargoCampo, FiltrosLegacy.Texto(request.Diagnostico, LargoCampo)));
        cmd.Parameters.Add(Varchar("@Folio", LargoCampo, FiltrosLegacy.Texto(request.Folio, LargoCampo)));
        cmd.Parameters.Add(Entero("@IDUsuarioActual", idUsuarioActual));
    }

    /// <summary>
    /// El texto que los procedimientos escriben en la bitacora, con el formato
    /// EXACTO que armaba Mac31:
    ///
    ///     CLAVE: ABC123  -  VALE: 9981~CLAVE: XYZ777  -  VALE: 9982
    ///
    /// Dos espacios antes y despues del guion, "VALE: " con uno, elementos
    /// pegados con virgulilla y sin virgulilla final. Se respeta al byte porque
    /// la bitacora vieja ya esta escrita asi y alguien la lee con los ojos:
    /// cambiar el formato partiria en dos la historia de la misma tabla.
    ///
    /// Es publico (internal al ensamblado via el repositorio) y esta probado sin
    /// base en GarantiasBitacoraTests.
    /// </summary>
    public static string TextoDeBitacora(IEnumerable<GarantiaPartida>? partidas)
    {
        if (partidas is null) return string.Empty;

        var piezas = partidas
            .Where(p => p is not null)
            .Select(p => $"CLAVE: {FiltrosLegacy.Texto(p.Codigo)}  -  VALE: {FiltrosLegacy.Texto(p.Ticket)}")
            .ToList();

        return string.Join(FiltrosLegacy.SeparadorIds, piezas);
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
