using ISL_Service.Application.DTOs.VentasDescuentos;
using ISL_Service.Application.DTOs.VentasEscrituras;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Infrastructure.Repositories;

/*
  DESCUENTOS: CATALOGO, DETALLE Y AJUSTES DE VENTA

  CUATRO PROCEDIMIENTOS DEL CATALOGO NO EXISTEN EN Produccion_svr:
  sp_n_ActualizarDescuentos, sp_n_EliminarDescuentos y sp_n_ConsultaDescuentos no
  estan en la base (si hay sp_Descuentos y sp_n_ConsultaDescuentosDetalle, que
  son otros). Legacy los llama igual y falla igual. Se implementan por su nombre,
  se marcan como no verificables en el reporte, y el dia que aparezcan en una
  base funcionan sin tocar nada.

  sp_n_InsertarDescuentos SI existe, y recibe CERO parametros: crea el renglon en
  blanco. Lo del nombre del descuento lo hace otra pantalla.

  LOS AJUSTES DE VENTA SON LA PARTE SERIA. Un ajuste es un descuento sobre una
  remision ya emitida: alguien lo pide y alguien lo autoriza, y de la firma de
  quien autoriza depende despues la auditoria. En legacy ese id viaja en el
  cuerpo; aqui sale del token.
*/
public class VentasDescuentosRepository : SpLegacyRepositoryBase, IVentasDescuentosRepository
{
    private const string SpCrearDescuento = "sp_n_InsertarDescuentos";
    private const string SpActualizarDescuento = "sp_n_ActualizarDescuentos";
    private const string SpEliminarDescuento = "sp_n_EliminarDescuentos";
    private const string SpConsultarDescuentos = "sp_n_ConsultaDescuentos";
    private const string SpSimularDescuento = "sp_n_SimularDescuento";
    private const string SpCrearDetalle = "sp_n_InsertarDescuentosDetalle";
    private const string SpActualizarDetalle = "sp_n_ActualizarDescuentosDetalle";
    private const string SpEliminarDetalle = "sp_n_EliminarDescuentosDetalle";
    private const string SpCrearAjuste = "sp_n_InsertarVentasDescuentosAjustes";
    private const string SpStatusAjuste = "sp_n_ActualizaStatusVentasDescuentosAjustes";
    private const string SpSimularAjuste = "sp_n_SimulaVentasDescuentosAjustes";
    private const string SpConsultarAjustes = "sp_n_ConsultaVentasDescuentosAjustes";

    private const int LargoLista = 4000;
    private const int LargoListaCorta = 500;
    private const int LargoListaMedia = 1000;
    private const int LargoTexto = 4000;
    private const int LargoPorcentaje = 50;
    private const int LargoEquipoDetalle = 200;
    private const int LargoEquipoSimular = 200;
    private const int LargoDescuento = 1000;
    private const int LargoFecha = 50;
    private const int LargoObservaciones = 250;
    private const int LargoMotivo = 250;
    private const int LargoFiltroCorto = 500;

    public VentasDescuentosRepository(IConfiguration configuration) : base(configuration) { }

    /* ------------------------------ Catalogo ------------------------------ */

    public async Task<ResultadoLegacyDto> CrearDescuentoAsync(CancellationToken ct)
    {
        /* Cero parametros. No es un olvido: el procedimiento no declara ninguno. */
        var tablas = await EjecutarTablasAsync(SpCrearDescuento, _ => { }, ct);
        return Resultado(tablas, "IDDescuento");
    }

    public async Task<ResultadoLegacyDto> ActualizarDescuentoAsync(
        DescuentoActualizarRequest request, CancellationToken ct)
    {
        /*
          UN SOLO PARAMETRO, Y EL NOMBRE DEL DESCUENTO NO VIAJA.

          Legacy valida que venga el texto y despues no se lo manda al
          procedimiento, que solo recibe @IDDescuento. Se respeta: mandarle un
          parametro que no declara haria fallar la llamada entera, y adivinar como
          se llamaria seria inventar.
        */
        var tablas = await EjecutarTablasAsync(SpActualizarDescuento, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDDescuento", request.IdDescuento));
        }, ct);

        return Resultado(tablas, "IDDescuento");
    }

    public async Task<ResultadoLegacyDto> EliminarDescuentoAsync(
        DescuentoIdRequest request, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpEliminarDescuento, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDDescuento", request.IdDescuento));
        }, ct);

        return Resultado(tablas, "IDDescuento");
    }

    public async Task<FilasLegacyDto> ConsultarDescuentosAsync(
        DescuentoIdRequest request, CancellationToken ct)
    {
        var filas = await EjecutarFilasAsync(SpConsultarDescuentos, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDDescuento", request.IdDescuento));
        }, ct);

        return new FilasLegacyDto { Rows = filas };
    }

    public async Task<ResultadoLegacyDto> SimularDescuentoAsync(
        DescuentoSimularRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        /*
          SIMULAR ESCRIBE.

          El procedimiento hace INSERT INTO Descuentos cuando no encuentra el
          renglon de simulacion de ese usuario y ese equipo: es su manera de
          tener un esquema temporal por puesto de trabajo. Por eso el usuario y el
          equipo son la LLAVE de la operacion, no una firma — y por eso importa
          que salgan del token: con el equipo de otro, se le pisa su simulacion.
        */
        var tablas = await EjecutarTablasAsync(SpSimularDescuento, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Descuento", LargoDescuento, FiltrosLegacy.Texto(request.Descuento, LargoDescuento)));
            cmd.Parameters.Add(Varchar("@NombreEquipo", LargoEquipoSimular, FiltrosLegacy.Texto(equipo, LargoEquipoSimular)));
        }, ct);

        return Resultado(tablas, "IDDescuento");
    }

    /* ------------------------------- Detalle ------------------------------- */

    public async Task<ResultadoLegacyDto> CrearDetalleAsync(
        DescuentoDetalleCrearRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        /* El de insertar NO declara @IDGrupoCategoria; el de actualizar si. */
        var tablas = await EjecutarTablasAsync(SpCrearDetalle, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDDescuento", request.IdDescuento));
            cmd.Parameters.Add(Entero("@IDCategoria", request.IdCategoria));
            cmd.Parameters.Add(Entero("@IDMarca", request.IdMarca));
            cmd.Parameters.Add(Varchar("@Porcentaje", LargoPorcentaje, FiltrosLegacy.Texto(request.Porcentaje, LargoPorcentaje)));
            cmd.Parameters.Add(Entero("@AplicarTodos", request.AplicarTodos));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipoDetalle, FiltrosLegacy.Texto(equipo, LargoEquipoDetalle)));
        }, ct);

        return Resultado(tablas, "IDDescuentoDetalle");
    }

    public async Task<ResultadoLegacyDto> ActualizarDetalleAsync(
        DescuentoDetalleActualizarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpActualizarDetalle, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDDescuentoDetalle", request.IdDescuentoDetalle));
            cmd.Parameters.Add(Entero("@IDDescuento", request.IdDescuento));
            cmd.Parameters.Add(Entero("@IDGrupoCategoria", request.IdGrupoCategoria));
            cmd.Parameters.Add(Entero("@IDCategoria", request.IdCategoria));
            cmd.Parameters.Add(Entero("@IDMarca", request.IdMarca));
            cmd.Parameters.Add(Varchar("@Porcentaje", LargoPorcentaje, FiltrosLegacy.Texto(request.Porcentaje, LargoPorcentaje)));
            cmd.Parameters.Add(Entero("@AplicarTodos", request.AplicarTodos));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipoDetalle, FiltrosLegacy.Texto(equipo, LargoEquipoDetalle)));
        }, ct);

        return Resultado(tablas, "IDDescuentoDetalle");
    }

    public async Task<ResultadoLegacyDto> EliminarDetalleAsync(
        DescuentoDetalleEliminarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpEliminarDetalle, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDDescuentoDetalle", request.IdDescuentoDetalle));
            cmd.Parameters.Add(Entero("@IDDescuento", request.IdDescuento));
            cmd.Parameters.Add(Entero("@IDCategoria", request.IdCategoria));
            cmd.Parameters.Add(Entero("@IDMarca", request.IdMarca));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipoDetalle, FiltrosLegacy.Texto(equipo, LargoEquipoDetalle)));
        }, ct);

        return Resultado(tablas, "IDDescuentoDetalle");
    }

    /* -------------------------- Ajustes de venta -------------------------- */

    public async Task<ResultadoLegacyDto> CrearAjusteAsync(
        VentaDescuentoAjusteCrearRequest request, int idUsuario, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpCrearAjuste, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDTipoLiquidacion", request.IdTipoLiquidacion));
            cmd.Parameters.Add(Entero("@IDVenta", request.IdVenta));
            cmd.Parameters.Add(Entero("@IDEmpresa", request.IdEmpresa));
            cmd.Parameters.Add(Entero("@Folio", request.Folio));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@DescuentoGlobal", LargoListaCorta, FiltrosLegacy.ImportesUsados(request.DescuentoGlobal)));
            cmd.Parameters.Add(Varchar("@IDVentasDetalle", LargoListaCorta, FiltrosLegacy.Ids(request.IdsVentaDetalle)));
            cmd.Parameters.Add(Varchar("@VentaDetalleDescuento1", LargoListaCorta, FiltrosLegacy.ImportesUsados(request.Descuento1)));
            cmd.Parameters.Add(Varchar("@VentaDetalleDescuento2", LargoListaCorta, FiltrosLegacy.ImportesUsados(request.Descuento2)));
            cmd.Parameters.Add(Varchar("@VentaDetalleDescuento3", LargoListaCorta, FiltrosLegacy.ImportesUsados(request.Descuento3)));
            cmd.Parameters.Add(Varchar("@DescuentoAdicional", LargoListaCorta, FiltrosLegacy.ImportesUsados(request.DescuentoAdicional)));
            /*
              LA FECHA VA SIN HORA: EL PROCEDIMIENTO SE LA PEGA.

              sp_n_InsertarVentasDescuentosAjustes hace
              `@Fecha = @Fecha + ' ' + <la hora de ahora>` antes de insertar. Con la
              hora dentro queda "2026-10-06 00:00:00 23:23:32", que no es fecha, y
              como corre con ANSI_WARNINGS OFF guarda NULL sin avisar.
            */
            cmd.Parameters.Add(Varchar("@Fecha", LargoFecha, FiltrosLegacy.SoloFecha(request.Fecha)));
            cmd.Parameters.Add(Varchar("@Observaciones", LargoObservaciones, FiltrosLegacy.Texto(request.Observaciones, LargoObservaciones)));
            cmd.Parameters.Add(Decimal18("@NuevoTotalPagar", request.NuevoTotalPagar));
            /* La de liquidacion es una fecha de calendario y entra directa a la
               columna; se manda igual que la escribe Mac31, sin hora. */
            cmd.Parameters.Add(Varchar("@FechaLiquidacion", LargoFecha, FiltrosLegacy.SoloFecha(request.FechaLiquidacion)));
            cmd.Parameters.Add(Varchar("@TextoDescuentos", LargoTexto, FiltrosLegacy.Texto(request.TextoDescuentos)));
            cmd.Parameters.Add(Varchar("@TextoDescuentosPorLinea", LargoTexto, FiltrosLegacy.Texto(request.TextoDescuentosPorLinea)));
            cmd.Parameters.Add(Decimal18("@DescuentoAplicar", request.DescuentoAplicar));
            cmd.Parameters.Add(Decimal18("@Diferencia", request.Diferencia));
        }, ct);

        return Resultado(tablas, "IDVentasDescuentosAjustes");
    }

    public async Task<ResultadoLegacyDto> CambiarStatusAjusteAsync(
        VentaDescuentoAjusteStatusRequest request, int idUsuario, CancellationToken ct)
    {
        /*
          QUIEN AUTORIZA O RECHAZA ES EL DEL TOKEN.

          El procedimiento tiene DOS parametros de usuario, @IDUsuarioAutorizacion
          y @IDUsuarioRechazo, y lo que decide la operacion es cual de los dos
          viene lleno. En legacy los dos vienen del cuerpo, asi que un usuario
          podia mandar el id de su jefe en @IDUsuarioAutorizacion y quedarse con
          un descuento "autorizado por el jefe". Aqui el id es el del token y el
          cuerpo solo dice si se autoriza o se rechaza.
        */
        var tablas = await EjecutarTablasAsync(SpStatusAjuste, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDVentasDescuentosAjustes", request.IdVentasDescuentosAjustes));
            cmd.Parameters.Add(Entero("@IDUsuarioAutorizacion", request.Autorizar ? idUsuario : 0));
            cmd.Parameters.Add(Entero("@IDUsuarioRechazo", request.Autorizar ? 0 : idUsuario));
            /* Sin hora: sp_n_ActualizaStatusVentasDescuentosAjustes se la pega el,
               y con la hora dentro guarda NULL en la fecha de autorizacion sin dar
               ningun error (ANSI_WARNINGS OFF). */
            cmd.Parameters.Add(Varchar("@Fecha", LargoFecha, FiltrosLegacy.SoloFecha(request.Fecha)));
            cmd.Parameters.Add(Varchar("@MotivoRechazo", LargoMotivo, FiltrosLegacy.Texto(request.MotivoRechazo, LargoMotivo)));
        }, ct);

        return Resultado(tablas, "IDVentasDescuentosAjustes");
    }

    public async Task<FilasLegacyDto> SimularAjusteAsync(
        VentaDescuentoAjusteSimularRequest request, int idUsuarioActual, CancellationToken ct)
    {
        var filas = await EjecutarFilasAsync(SpSimularAjuste, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDVentasDescuentosAjustes", request.IdVentasDescuentosAjustes));
            cmd.Parameters.Add(Entero("@IDVenta", request.IdVenta));
            cmd.Parameters.Add(Entero("@IDEmpresa", request.IdEmpresa));
            cmd.Parameters.Add(Entero("@Folio", request.Folio));
            cmd.Parameters.Add(Varchar("@DescuentoGlobal", LargoTexto, FiltrosLegacy.ImportesUsados(request.DescuentoGlobal)));
            cmd.Parameters.Add(Varchar("@IDVentasDetalle", LargoFiltroCorto, FiltrosLegacy.Ids(request.IdsVentaDetalle)));
            cmd.Parameters.Add(Varchar("@VentaDetalleDescuento1", LargoTexto, FiltrosLegacy.ImportesUsados(request.Descuento1)));
            cmd.Parameters.Add(Varchar("@VentaDetalleDescuento2", LargoTexto, FiltrosLegacy.ImportesUsados(request.Descuento2)));
            cmd.Parameters.Add(Varchar("@VentaDetalleDescuento3", LargoTexto, FiltrosLegacy.ImportesUsados(request.Descuento3)));
            cmd.Parameters.Add(Varchar("@DescuentoAdicional", LargoTexto, FiltrosLegacy.ImportesUsados(request.DescuentoAdicional)));
            cmd.Parameters.Add(Entero("@Totalizado", request.Totalizado));
            cmd.Parameters.Add(Entero("@IDUsuarioActual", idUsuarioActual));
        }, ct);

        return new FilasLegacyDto { Rows = filas };
    }

    public async Task<FilasLegacyDto> ConsultarAjustesAsync(
        VentaDescuentoAjusteConsultaRequest request, int idUsuarioActual, CancellationToken ct)
    {
        /*
          LOS FILTROS VIENEN DEL REQUEST, NO DE UNA FILA DE Parametros.

          Esta es la diferencia mas grande de todo el modulo. Legacy ignora los
          filtros del cuerpo: recibe un `ID`, lee con sp_n_ConsultaParametros el
          renglon que la PANTALLA DE REPORTES dejo guardado, y saca los filtros de
          Param2 a Param12 por POSICION. Es el mecanismo de los reportes de
          legacy: la pantalla guarda veinte columnas de texto y el reporte las
          vuelve a leer.

          Para una consulta de pantalla eso tiene tres problemas: hay que haber
          guardado el renglon antes (si no, contesta "INTENTE GENERAR DE NUEVAMENTE
          EL REPORTE.", con el typo incluido); los filtros viajan como texto en
          posiciones sin nombre; y cualquiera que adivine un `ID` ve los filtros
          —y los datos— de otro.

          Aqui los filtros son campos con nombre y el candado de almacen sale del
          token. El renglon de Parametros se sigue usando donde de verdad hace
          falta: en los reportes, que se piden por URL.
        */
        var filas = await EjecutarFilasAsync(SpConsultarAjustes, cmd =>
        {
            cmd.Parameters.Add(Varchar("@IDVentasDescuentosAjustes", LargoTexto, FiltrosLegacy.Ids(request.IdsVentasDescuentosAjustes)));
            cmd.Parameters.Add(Varchar("@IDTiposLiquidaciones", LargoListaMedia, FiltrosLegacy.Ids(request.IdsTipoLiquidacion)));
            cmd.Parameters.Add(Varchar("@IDsVenta", LargoListaMedia, FiltrosLegacy.Ids(request.IdsVenta)));
            cmd.Parameters.Add(Varchar("@IDsCliente", LargoFiltroCorto, FiltrosLegacy.Ids(request.IdsCliente)));
            cmd.Parameters.Add(Varchar("@IDsAgente", LargoFiltroCorto, FiltrosLegacy.Ids(request.IdsAgente)));
            cmd.Parameters.Add(Entero("@FolioInicial", request.FolioInicial));
            cmd.Parameters.Add(Entero("@FolioFinal", request.FolioFinal));
            cmd.Parameters.Add(Varchar("@FechaInicial", 20, FiltrosLegacy.FechaDesde(request.FechaInicial)));
            cmd.Parameters.Add(Varchar("@FechaFinal", 20, FiltrosLegacy.FechaHasta(request.FechaFinal)));
            cmd.Parameters.Add(Entero("@Status", request.Status));
            cmd.Parameters.Add(Entero("@IDUsuarioActual", idUsuarioActual));
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
