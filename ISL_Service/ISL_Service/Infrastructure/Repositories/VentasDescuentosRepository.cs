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
    /// <summary>
    /// El mismo procedimiento con el que se cobra una remision. Aqui se llama con
    /// su `@IDVentasDescuentosAjustes` de verdad, que es lo que marca la
    /// liquidacion como aplicada; ver InsertarPagoDeDescuentoAsync.
    /// </summary>
    private const string SpInsertarVentasPagos = "sp_n_InsertarVentasPagos";

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

        /*
          LA TABLA QUE IMPORTA NO SIEMPRE ES LA PRIMERA, Y AQUI CASI NUNCA LO ES.

          `sp_n_SimularDescuento` tiene dos salidas segun si el renglon de
          prueba de ese usuario y ese equipo YA existe:

            no existia  ->  lo inserta y devuelve UNA tabla:
                            result / mensaje / IDDescuento
            ya existia  ->  devuelve DOS: primero un `SELECT Porcentaje = 0`
                            (el bloque ELSE del procedimiento) y DESPUES la de
                            result / mensaje / IDDescuento.

          Con `Resultado(tablas, ...)` a secas se leia la tabla 0 siempre, asi
          que en el segundo caso -o sea de la SEGUNDA simulacion en adelante,
          para siempre- devolvia `Porcentaje` y ni id ni result: la respuesta
          salia con `hecho` en true, sin IDDescuento y sin un solo error. La
          pantalla no tenia esquema con el que simular y no habia nada que
          mirar para saber por que.

          Asi que se elige la tabla POR SU CONTENIDO: la primera que traiga la
          columna del id. Si ninguna la trae, se cae en la de siempre y el
          resultado dice lo que diga.
        */
        return Resultado(TablasDesdeLaQueTraeElId(tablas, "IDDescuento"), "IDDescuento");
    }

    /*
      Reordena las tablas para que la que trae `columnaId` quede primera, sin
      perder las demas. No se cambia `Resultado` para todos porque las otras
      escrituras SI devuelven su resultado en la tabla 0 y ahi `Filas` es lo
      que el front ya lee.
    */
    private static List<List<Dictionary<string, object?>>> TablasDesdeLaQueTraeElId(
        List<List<Dictionary<string, object?>>> tablas, string columnaId)
    {
        for (var i = 0; i < tablas.Count; i++)
        {
            var fila = tablas[i].Count > 0 ? tablas[i][0] : null;
            if (fila is not null && fila.ContainsKey(columnaId))
            {
                if (i == 0) return tablas;

                var ordenadas = new List<List<Dictionary<string, object?>>> { tablas[i] };
                for (var j = 0; j < tablas.Count; j++)
                    if (j != i) ordenadas.Add(tablas[j]);

                return ordenadas;
            }
        }

        return tablas;
    }

    /*
      LA UNICA CONSULTA DE ESTE ARCHIVO QUE NO ES UN PROCEDIMIENTO.

      Es un SELECT y no un sp_n_ porque no hay ninguno que conteste esto: la
      pregunta ("¿este esquema es el borrador de simulacion de este puesto de
      trabajo?") no existe en legacy, porque alla el permiso se resolvia
      escondiendo botones. La condicion no se invento: son las TRES columnas
      con las que `sp_n_SimularDescuento` busca y crea ese renglon, leidas de
      su propio texto:

          SELECT @IDDescuento = IDDescuento FROM Descuentos
          WHERE NombreEquipo = @NombreEquipo
            AND IDUsuario = @IDUsuario
            AND EsSimular = @EsSimular   -- 1

      El equipo se compara tal cual, sin recortar ni normalizar, porque asi se
      guarda y asi se busca alla: cualquier arreglo por nuestra cuenta haria
      que el renglon que el procedimiento SI encuentra aqui no apareciera.

      Fallar cerrado es lo correcto si el renglon no existe o no es suyo: el
      llamador se queda con el permiso que de verdad tenga.
    */
    public async Task<bool> EsBorradorDeSimulacionAsync(
        int idDescuento, int idUsuario, string equipo, CancellationToken ct)
    {
        if (idDescuento <= 0 || idUsuario <= 0) return false;

        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT TOP 1 1 FROM dbo.Descuentos " +
            "WHERE IDDescuento = @IDDescuento AND IDUsuario = @IDUsuario " +
            "AND EsSimular = 1 AND NombreEquipo = @NombreEquipo";
        cmd.CommandTimeout = CommandTimeoutSeconds;
        cmd.Parameters.Add(Entero("@IDDescuento", idDescuento));
        cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
        cmd.Parameters.Add(Varchar("@NombreEquipo", 200, equipo ?? ""));

        var hay = await cmd.ExecuteScalarAsync(ct);
        return hay is not null && hay != DBNull.Value;
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

    /*
      EL ABONO DE UN DESCUENTO: sp_n_InsertarVentasPagos CON SU
      @IDVentasDescuentosAjustes DE VERDAD.

      POR QUE HACIA FALTA ESTE METODO
      -------------------------------
      sp_n_InsertarVentasPagos se llamaba en UN solo sitio del backend nuevo,
      VentasMultiPagoRepository, que es el de cobrar remisiones, y ahi manda
      `@IDVentasDescuentosAjustes = 0` FIJO (su linea 455). Con 0 el
      procedimiento se salta este bloque, leido en la base:

          IF @IDVentasDescuentosAjustes > 0
          BEGIN
            UPDATE VentasDescuentosAjustes
            SET IDPagoVenta = @IDPagoVenta,
                [IDUsuario Aplicacion] = @IDUsuario,
                [Fecha Aplicacion] = @FechaOperacionPagos
            WHERE IDVentasDescuentosAjustes = @IDVentasDescuentosAjustes
          END

      O sea: con 0 el abono entra pero la liquidacion NUNCA queda marcada como
      aplicada ni apuntada al pago que la aplico. Dos consecuencias, y las dos
      son dinero: la misma liquidacion se puede aplicar otra vez (nada dice que
      ya se aplico), y si hay que cancelar el abono no hay forma de saber cual
      pago vino de cual descuento.

      LOS TREINTA Y CINCO PARAMETROS VAN TODOS, en el mismo orden y con los
      mismos nombres que Mac3Servicios, VentasPagosRepository lineas 126-163. El
      procedimiento tiene valores por omision con datos de prueba, asi que los
      que no aplican van en 0 o cadena vacia, no se omiten.

      LO QUE MANDA EL BOTON APLICAR DE MAC31 Y LO QUE NO
      (LiquidacionesDescuento.cs:1050-1062): manda IDVentasDescuentosAjustes,
      IDVenta, IDTipoPago, IDUsuario, Abono y Observaciones. NO manda IDCliente
      ni MontoTotal -quedan en 0-, y el procedimiento resuelve el cliente desde
      la remision. Se copia tal cual: mandar un IDCliente desde aqui seria
      inventar un dato que legacy no manda.

      LA TRANSACCION ES DEL PROCEDIMIENTO. Abre y cierra su propia transaccion y
      hace COMMIT dentro; no se envuelve en otra desde aqui, igual que en
      VentasMultiPagoRepository.
    */
    public async Task<ResultadoLegacyDto> InsertarPagoDeDescuentoAsync(
        int idVentasDescuentosAjustes,
        int idVenta,
        int idTipoPago,
        decimal abono,
        string? observaciones,
        int idUsuario,
        string equipo,
        CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpInsertarVentasPagos, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDCobro", 0));
            cmd.Parameters.Add(Entero("@IDVentasDescuentosAjustes", idVentasDescuentosAjustes));
            cmd.Parameters.Add(Entero("@IDVenta", idVenta));
            cmd.Parameters.Add(Entero("@IDCliente", 0));
            cmd.Parameters.Add(Entero("@IDTipoPago", idTipoPago));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Decimal18("@Abono", abono));
            cmd.Parameters.Add(Decimal18("@MontoTotal", 0m));
            cmd.Parameters.Add(Varchar("@Observaciones", LargoTexto, FiltrosLegacy.Texto(observaciones)));
            cmd.Parameters.Add(Entero("@IDBancoCheque", 0));
            cmd.Parameters.Add(Entero("@IDBancoTransfer", 0));
            cmd.Parameters.Add(Entero("@IDBancoTarjeta", 0));
            cmd.Parameters.Add(Varchar("@Transferencia", LargoListaCorta, string.Empty));
            cmd.Parameters.Add(Varchar("@Cheque", LargoListaCorta, string.Empty));
            cmd.Parameters.Add(Varchar("@Tarjeta", LargoListaCorta, string.Empty));
            cmd.Parameters.Add(Entero("@DiffUsados", 0));
            cmd.Parameters.Add(Varchar("@DepositoEfectivoNumero", LargoListaCorta, string.Empty));
            cmd.Parameters.Add(Entero("@IDBancoDepositoEfe", 0));
            cmd.Parameters.Add(Varchar("@ExcedenteUsadosMotivo", LargoTexto, string.Empty));
            cmd.Parameters.Add(Decimal18("@Cargo", 0m));
            cmd.Parameters.Add(Varchar("@CargoMotivo", LargoTexto, string.Empty));
            cmd.Parameters.Add(Varchar("@IDsTipoUsado", LargoListaCorta, string.Empty));
            cmd.Parameters.Add(Varchar("@CantidadesUsado", LargoListaCorta, string.Empty));
            cmd.Parameters.Add(Varchar("@CostosConIvaUsado", LargoListaCorta, string.Empty));
            cmd.Parameters.Add(Entero("@IDAgenteLiquidacion", 0));
            cmd.Parameters.Add(Entero("@IDRepartidorLiquidacion", 0));
            cmd.Parameters.Add(Entero("@TipoTarjeta", 0));
            cmd.Parameters.Add(Entero("@IDPedido", 0));
            cmd.Parameters.Add(Entero("@IDGarantia", 0));
            cmd.Parameters.Add(Entero("@PedidoNumeroPago", 0));
            cmd.Parameters.Add(Entero("@IDBancoTransferenciaMovimiento", 0));
            cmd.Parameters.Add(Decimal18("@CascoKiloCantidad", 0m));
            cmd.Parameters.Add(Decimal18("@CascoKiloTotal", 0m));
            cmd.Parameters.Add(Decimal18("@CascoKiloPrecio", 0m));
            cmd.Parameters.Add(Varchar("@CascoKiloMotivo", LargoTexto, string.Empty));
            cmd.Parameters.Add(Varchar("@Equipo", LargoListaCorta, FiltrosLegacy.Texto(equipo, LargoListaCorta)));
        }, ct);

        return Resultado(tablas, "IDPagoVenta");
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
