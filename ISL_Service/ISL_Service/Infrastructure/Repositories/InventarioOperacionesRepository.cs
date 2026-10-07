using ISL_Service.Application.DTOs.InventarioOperaciones;
using ISL_Service.Application.DTOs.VentasEscrituras;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Infrastructure.Repositories;

/*
  INVENTARIO Y CATALOGO DE TIPOS DE CASCO, CONTRA LOS PROCEDIMIENTOS DE MAC31

  Dos notas de fondo:

  1) LAS SEIS LISTAS DEL INVENTARIO DE PEDIDOS se arman aqui, a partir de una
     lista de renglones. En legacy el cliente mandaba seis listas paralelas, el
     servicio las pegaba con virgulilla y el procedimiento las volvia a cruzar
     por posicion; nadie comprobaba que midieran lo mismo, y el sintoma de que no
     lo hicieran era un pedido con la cantidad de otro producto. Con un renglon
     por producto el desajuste no se puede ni expresar.

  2) EL CAMBIO DE ESTATUS DEL CATALOGO LEE PRIMERO. sp_n_ActualizarTipoUsado
     recibe los doce campos y sobrescribe el renglon entero, asi que para cambiar
     solo el estatus hay que mandarle los otros once como estan. En legacy eso lo
     resolvia el cliente (mandaba el DTO completo desde la pantalla) y cuando no
     lo hacia —el DELETE, que no valida nada— el renglon quedaba con los importes
     en cero. Aqui el renglon actual se lee de la base antes de escribir.
*/
public class InventarioOperacionesRepository : SpLegacyRepositoryBase, IInventarioOperacionesRepository
{
    private const string SpCrearAjuste = "sp_n_InsertarAjusteInventario";
    private const string SpCancelarAjuste = "sp_n_CancelarAjusteInventario";
    private const string SpConsultarAjustes = "sp_n_ConsultaAjusteInventario";
    private const string SpCrearInventarioPedido = "sp_n_InsertarInventarioPedidos";
    private const string SpCancelarInventarioPedido = "sp_n_CancelarInventarioPedidos";
    private const string SpConsultarInventarioPedidos = "sp_n_ConsultarInventarioPedidos";
    private const string SpCrearTipoUsado = "sp_n_InsertarTipoUsado";
    private const string SpActualizarTipoUsado = "sp_n_ActualizarTipoUsado";
    private const string SpConsultarTiposUsado = "sp_n_ConsultaTiposUsados";

    private const int LargoLista = 4000;
    private const int LargoTexto = 4000;
    private const int LargoEquipoCorto = 400;
    private const int LargoEquipoLargo = 1000;
    private const int LargoFecha = 20;
    private const int LargoListaFiltro = 2000;
    private const int LargoNombre = 250;

    public InventarioOperacionesRepository(IConfiguration configuration) : base(configuration) { }

    /* ------------------------ Ajustes de inventario ------------------------ */

    public async Task<ResultadoLegacyDto> CrearAjusteAsync(
        AjusteInventarioCrearRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpCrearAjuste, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDAlmacen", request.IdAlmacen));
            cmd.Parameters.Add(Entero("@IDProducto", request.IdProducto));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Entero("@Entrada", request.Entrada));
            cmd.Parameters.Add(Entero("@Salida", request.Salida));
            cmd.Parameters.Add(Varchar("@Motivo", LargoTexto, FiltrosLegacy.Texto(request.Motivo)));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipoLargo, FiltrosLegacy.Texto(equipo, LargoEquipoLargo)));
        }, ct);

        return Resultado(tablas, "IDAjusteInventario");
    }

    public async Task<ResultadoLegacyDto> CancelarAjusteAsync(
        AjusteInventarioCancelarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        /* Los parametros llevan el sufijo "Cancelacion" aunque el dato sea el
           mismo del token. Es el nombre que les puso el procedimiento. */
        var tablas = await EjecutarTablasAsync(SpCancelarAjuste, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDAjusteInventario", request.IdAjusteInventario));
            cmd.Parameters.Add(Entero("@IDUsuarioCancelacion", idUsuario));
            cmd.Parameters.Add(Varchar("@EquipoCancelacion", LargoEquipoLargo, FiltrosLegacy.Texto(equipo, LargoEquipoLargo)));
        }, ct);

        return Resultado(tablas, "IDAjusteInventario");
    }

    public async Task<FilasLegacyDto> ConsultarAjustesAsync(
        AjusteInventarioConsultaRequest request, int idUsuarioActual, CancellationToken ct)
    {
        /*
          LA LISTA VACIA ES "TODOS", Y ASI LO ENTIENDE EL PROCEDIMIENTO.

          En legacy la pantalla mandaba literalmente "0" para decir "todos los
          almacenes" y el servicio lo traducia a "" justo antes de llamar
          (`if (IDsAlmacen == "0") IDsAlmacen = "";`). Una lista vacia dice lo
          mismo sin el intermediario.
        */
        var filas = await EjecutarFilasAsync(SpConsultarAjustes, cmd =>
        {
            cmd.Parameters.Add(Varchar("@IDsGrupoCategoria", LargoListaFiltro, FiltrosLegacy.Ids(request.IdsGrupoCategoria)));
            cmd.Parameters.Add(Varchar("@IDsCategoria", LargoListaFiltro, FiltrosLegacy.Ids(request.IdsCategoria)));
            cmd.Parameters.Add(Varchar("@IDsMarca", LargoListaFiltro, FiltrosLegacy.Ids(request.IdsMarca)));
            cmd.Parameters.Add(Varchar("@IDsAlmacen", LargoListaFiltro, FiltrosLegacy.Ids(request.IdsAlmacen)));
            cmd.Parameters.Add(Varchar("@FechaInicial", LargoFecha, FiltrosLegacy.FechaDesde(request.FechaInicial)));
            cmd.Parameters.Add(Varchar("@FechaFinal", LargoFecha, FiltrosLegacy.FechaHasta(request.FechaFinal)));
            cmd.Parameters.Add(Entero("@IDUsuarioActual", idUsuarioActual));
        }, ct);

        return new FilasLegacyDto { Rows = filas };
    }

    /* ----------------------- Inventario de pedidos ----------------------- */

    public async Task<ResultadoLegacyDto> CrearInventarioPedidoAsync(
        InventarioPedidoCrearRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpCrearInventarioPedido, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDStatus", request.IdStatus));
            cmd.Parameters.Add(Varchar("@Observaciones", LargoTexto, FiltrosLegacy.Texto(request.Observaciones)));
            cmd.Parameters.Add(Varchar("@IDsProducto", LargoLista,
                FiltrosLegacy.Ids(request.Partidas.Select(p => p.IdProducto))));
            cmd.Parameters.Add(Varchar("@Codigos", LargoLista,
                FiltrosLegacy.TextosUsados(request.Partidas.Select(p => p.Codigo))));
            cmd.Parameters.Add(Varchar("@CantidadesInventario", LargoLista,
                FiltrosLegacy.Ids(request.Partidas.Select(p => p.CantidadInventario))));
            cmd.Parameters.Add(Varchar("@CantidadesVentas", LargoLista,
                FiltrosLegacy.Ids(request.Partidas.Select(p => p.CantidadVentas))));
            cmd.Parameters.Add(Varchar("@CantidadesFaltante", LargoLista,
                FiltrosLegacy.Ids(request.Partidas.Select(p => p.CantidadFaltante))));
            cmd.Parameters.Add(Varchar("@CantidadesPedido", LargoLista,
                FiltrosLegacy.Ids(request.Partidas.Select(p => p.CantidadPedido))));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipoCorto, FiltrosLegacy.Texto(equipo, LargoEquipoCorto)));
        }, ct);

        return Resultado(tablas, "IDResult");
    }

    public async Task<ResultadoLegacyDto> CancelarInventarioPedidoAsync(
        InventarioPedidoCancelarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpCancelarInventarioPedido, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDInventarioPedido", request.IdInventarioPedido));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipoCorto, FiltrosLegacy.Texto(equipo, LargoEquipoCorto)));
        }, ct);

        return Resultado(tablas, "IDInventarioPedido");
    }

    public async Task<FilasLegacyDto> ConsultarInventarioPedidosAsync(
        InventarioPedidoConsultaRequest request, CancellationToken ct)
    {
        var filas = await EjecutarFilasAsync(SpConsultarInventarioPedidos, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDInventarioPedido", request.IdInventarioPedido));
            cmd.Parameters.Add(Entero("@IDStatus", request.IdStatus));
            cmd.Parameters.Add(Varchar("@FechaInicial", LargoFecha, FiltrosLegacy.FechaDesde(request.FechaInicial)));
            cmd.Parameters.Add(Varchar("@FechaFinal", LargoFecha, FiltrosLegacy.FechaHasta(request.FechaFinal)));
        }, ct);

        return new FilasLegacyDto { Rows = filas };
    }

    /* --------------------- Catalogo de tipos de casco --------------------- */

    public async Task<ResultadoLegacyDto> CrearTipoUsadoAsync(
        TipoUsadoCrearRequest request, CancellationToken ct)
    {
        /*
          ESTE NO RECIBE NI USUARIO NI EQUIPO: el procedimiento declara diez
          parametros y ninguno es de firma. Es un catalogo, no un movimiento.
        */
        var tablas = await EjecutarTablasAsync(SpCrearTipoUsado, cmd =>
        {
            AgregarCamposTipoUsado(cmd, request);
        }, ct);

        return Resultado(tablas, "IDTipoUsado");
    }

    public async Task<ResultadoLegacyDto> ActualizarTipoUsadoAsync(
        TipoUsadoActualizarRequest request, int idStatus, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpActualizarTipoUsado, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDTipoUsado", request.IdTipoUsado));
            cmd.Parameters.Add(Entero("@IDStatus", idStatus));
            AgregarCamposTipoUsado(cmd, request);
        }, ct);

        return Resultado(tablas, "IDTipoUsado");
    }

    public async Task<TipoUsadoGuardado?> LeerTipoUsadoAsync(int idTipoUsado, CancellationToken ct)
    {
        /*
          SE PIDE SIN FILTRO DE ESTATUS A PROPOSITO (@IDStatus = 0).

          Para reactivar un tipo inactivo hay que poder leerlo primero, y si se
          pidiera solo los activos no aparecería: el endpoint de reactivar
          contestaria "no existe" sobre un renglon que si existe.
        */
        var filas = await EjecutarFilasAsync(SpConsultarTiposUsado, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDTipoUsado", idTipoUsado));
            cmd.Parameters.Add(Entero("@IDStatus", 0));
            cmd.Parameters.Add(Varchar("@TipoUsado", LargoNombre, string.Empty));
            cmd.Parameters.Add(Entero("@Formato", 0));
            cmd.Parameters.Add(Entero("@Identico", 0));
        }, ct);

        if (filas.Count == 0) return null;
        var fila = filas[0];

        return new TipoUsadoGuardado
        {
            IdStatus = Entero(fila, "IDStatus", 1),
            Datos = new TipoUsadoActualizarRequest
            {
                IdTipoUsado = Entero(fila, "IDTipoUsado", idTipoUsado),
                TipoUsado = Texto(fila, "TipoUsado"),
                TipoUsadoCorto = Texto(fila, "TipoUsadoCorto"),
                VentaCostoCargoSinIva = Importe(fila, "VentaCostoCargoSinIva"),
                VentaCostoCargoConIva = Importe(fila, "VentaCostoCargoConIva"),
                VentaCostoCreditoSinIva = Importe(fila, "VentaCostoCreditoSinIva"),
                VentaCostoCreditoConIva = Importe(fila, "VentaCostoCreditoConIva"),
                Bonificacion = Importe(fila, "Bonificacion"),
                CostoExcedentes = Importe(fila, "CostoExcedentes"),
                PesoKg = Importe(fila, "PesoKg"),
                Orden = Entero(fila, "Orden")
            }
        };
    }

    private static void AgregarCamposTipoUsado(
        Microsoft.Data.SqlClient.SqlCommand cmd, TipoUsadoCrearRequest request)
    {
        cmd.Parameters.Add(Varchar("@TipoUsado", LargoNombre, FiltrosLegacy.Texto(request.TipoUsado, LargoNombre)));
        cmd.Parameters.Add(Varchar("@TipoUsadoCorto", LargoNombre, FiltrosLegacy.Texto(request.TipoUsadoCorto, LargoNombre)));
        cmd.Parameters.Add(Decimal18("@VentaCostoCargoSinIva", request.VentaCostoCargoSinIva));
        cmd.Parameters.Add(Decimal18("@VentaCostoCargoConIva", request.VentaCostoCargoConIva));
        cmd.Parameters.Add(Decimal18("@VentaCostoCreditoSinIva", request.VentaCostoCreditoSinIva));
        cmd.Parameters.Add(Decimal18("@VentaCostoCreditoConIva", request.VentaCostoCreditoConIva));
        cmd.Parameters.Add(Decimal18("@Bonificacion", request.Bonificacion));
        cmd.Parameters.Add(Decimal18("@CostoExcedentes", request.CostoExcedentes));
        cmd.Parameters.Add(Decimal18("@PesoKg", request.PesoKg));
        cmd.Parameters.Add(Entero("@Orden", request.Orden));
    }

    private static decimal Importe(Dictionary<string, object?> fila, string columna)
    {
        if (!fila.TryGetValue(columna, out var valor) || valor is null) return 0m;
        return valor switch
        {
            decimal d => d,
            double db => (decimal)db,
            float f => (decimal)f,
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
