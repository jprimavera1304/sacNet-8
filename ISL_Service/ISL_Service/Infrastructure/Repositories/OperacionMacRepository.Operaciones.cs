using System.Globalization;
using ISL_Service.Application.DTOs.Empleados;
using ISL_Service.Application.DTOs.OperacionMac;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

public partial class OperacionMacRepository
{
    // =======================================================================
    // COMPRAS
    // =======================================================================

    /// <summary>
    /// Consulta de compras.
    ///
    /// La precedencia del filtro por factura es EXPLICITA: si hay texto, se manda
    /// solo la factura y los demas filtros van vacios. Legacy hacia lo mismo pero
    /// a escondidas (reemplazaba el DTO completo) y con un defecto: el campo no
    /// tenia valor por omision, asi que una peticion sin factura entraba igual a
    /// esa rama porque null != "", y consultaba con @Factura nulo tirando todos
    /// los filtros que el usuario si habia puesto. Aqui la rama necesita texto.
    ///
    /// @IDUsuario es un FILTRO (quien capturo la compra) y @IDUsuarioActual es la
    /// firma de quien consulta. Son dos cosas distintas que legacy mezclaba en el
    /// mismo campo del cuerpo; aqui la segunda sale del token.
    /// </summary>
    public Task<List<Dictionary<string, object?>>> ConsultarComprasAsync(
        ComprasFiltro filtro, int idUsuarioActual, CancellationToken ct)
    {
        var factura = Texto(filtro.Factura);
        var porFactura = factura.Length > 0;

        return ConsultarAsync("dbo.sp_n_ConsultaCompras", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCompra", porFactura ? 0 : filtro.IdCompra);
            cmd.Parameters.AddWithValue("@IDEmpresa", porFactura ? 0 : filtro.IdEmpresa);
            cmd.Parameters.AddWithValue("@IDAlmacen", porFactura ? 0 : filtro.IdAlmacen);
            cmd.Parameters.AddWithValue("@IDProveedor", porFactura ? 0 : filtro.IdProveedor);
            cmd.Parameters.AddWithValue("@IDProducto", porFactura ? 0 : filtro.IdProducto);
            cmd.Parameters.AddWithValue("@IDUsuario", porFactura ? 0 : filtro.IdUsuarioCaptura);
            cmd.Parameters.AddWithValue("@Factura", factura);
            cmd.Parameters.AddWithValue("@FechaCapturaInicial", porFactura ? string.Empty : ADesde(filtro.CapturaDesde));
            cmd.Parameters.AddWithValue("@FechaCapturaFinal", porFactura ? string.Empty : AHasta(filtro.CapturaHasta));
            cmd.Parameters.AddWithValue("@FechaEmisionInicial", porFactura ? string.Empty : ADesde(filtro.EmisionDesde));
            cmd.Parameters.AddWithValue("@FechaEmisionFinal", porFactura ? string.Empty : AHasta(filtro.EmisionHasta));
            cmd.Parameters.AddWithValue("@FechaCancelInicial", porFactura ? string.Empty : ADesde(filtro.CancelacionDesde));
            cmd.Parameters.AddWithValue("@FechaCancelFinal", porFactura ? string.Empty : AHasta(filtro.CancelacionHasta));
            cmd.Parameters.AddWithValue("@FechaPagoInicial", porFactura ? string.Empty : ADesde(filtro.PagoDesde));
            cmd.Parameters.AddWithValue("@FechaPagoFinal", porFactura ? string.Empty : AHasta(filtro.PagoHasta));
            // @Formato era el interruptor "0 todos los campos / 1 Excel" de
            // WinForms. Siempre 0: el Excel lo arma el front con los mismos datos.
            cmd.Parameters.AddWithValue("@Formato", 0);
            cmd.Parameters.AddWithValue("@IDUsuarioActual", idUsuarioActual);
        }, ct);
    }

    public Task<ResultadoLegacy> MarcarCompraFacturaAsync(
        int idCompra, bool esFactura, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarCompra", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCompra", idCompra);
            cmd.Parameters.AddWithValue("@EsFactura", esFactura ? 1 : 0);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    /// <summary>
    /// Cancelacion de compra.
    ///
    /// Devuelve tambien los renglones del procedimiento porque ahi viene el folio
    /// de la factura cancelada, que la pantalla muestra. Y porque legacy tenia un
    /// defecto que se arregla aqui: comprobaba que la lista no fuera nula —cosa
    /// que nunca es, se inicializa vacia— asi que una respuesta SIN renglones se
    /// reportaba como exito, sin result ni mensaje. Aqui una lista vacia se trata
    /// como lo que es: el procedimiento no contesto.
    /// </summary>
    public async Task<(ResultadoLegacy, List<Dictionary<string, object?>>)> CancelarCompraAsync(
        int idCompra, string motivo, int idUsuario, string equipo, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = Sp("dbo.sp_n_CancelarCompra", conn);
        cmd.Parameters.AddWithValue("@IDCompra", idCompra);
        cmd.Parameters.AddWithValue("@IDUsuarioCancelacion", idUsuario);
        cmd.Parameters.AddWithValue("@EquipoCancelacion", equipo);
        cmd.Parameters.AddWithValue("@MotivoCancelacion", Texto(motivo));

        var dt = await FillAsync(cmd, ct);
        var resultado = BajaLogicaLegacy.LeerResultado(dt);
        return (resultado, Funciones.DataTableToRows(dt));
    }

    // =======================================================================
    // INVENTARIO EN TRANSITO
    // =======================================================================

    public Task<(ResultadoLegacy, int)> ProcesarTransitoAsync(
        ProcesarTransitoRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarConIdAsync("dbo.sp_n_ProcesarInventarioTransito", "IDResult", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDVentaDetalle", r.IdVentaDetalle);
            cmd.Parameters.AddWithValue("@IDCliente", r.IdCliente);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    // =======================================================================
    // CORTES DE CAJA
    // =======================================================================

    /// <summary>
    /// Consulta de cortes. El id manda sobre las fechas: con id se ignora el
    /// rango, igual que en legacy, pero dicho en vez de hecho a escondidas.
    /// </summary>
    public Task<List<Dictionary<string, object?>>> ConsultarCortesAsync(
        int idCorte, DateTime? desde, DateTime? hasta, CancellationToken ct)
        => ConsultarAsync("dbo.sp_n_ConsultarCortes", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCorte", idCorte);
            cmd.Parameters.AddWithValue("@FechaInicial", idCorte > 0 ? string.Empty : ADesde(desde));
            cmd.Parameters.AddWithValue("@FechaFinal", idCorte > 0 ? string.Empty : AHasta(hasta));
        }, ct);

    /// <summary>
    /// Los gastos del corte por tipo. Las dos fechas son obligatorias: el
    /// procedimiento las usa sin ramas y legacy las convertia sin comprobar que
    /// vinieran, asi que una consulta sin fechas reventaba con un
    /// ArgumentOutOfRange y devolvia 400 con el stack completo.
    /// </summary>
    public Task<List<Dictionary<string, object?>>> ConsultarCorteTipoGastosAsync(
        string corteInfo, DateTime desde, DateTime hasta, CancellationToken ct)
        => ConsultarAsync("dbo.sp_n_ConsultarCortesTipoGastos", cmd =>
        {
            cmd.Parameters.AddWithValue("@CorteInfo", Texto(corteInfo));
            cmd.Parameters.AddWithValue("@FechaInicial", ADesde(desde));
            cmd.Parameters.AddWithValue("@FechaFinal", AHasta(hasta));
        }, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarCortesCentroServicioAsync(
        int idCorte, int folio, DateTime? desde, DateTime? hasta, int idTipoRetiro, CancellationToken ct)
        => ConsultarAsync("dbo.sp_n_ConsultarCortesCS", cmd =>
        {
            // El folio manda: con folio se ignoran id, tipo de retiro y fechas.
            var porFolio = folio > 0;
            cmd.Parameters.AddWithValue("@IDCorte", porFolio ? 0 : idCorte);
            cmd.Parameters.AddWithValue("@Folio", folio);
            cmd.Parameters.AddWithValue("@FechaInicial", porFolio ? string.Empty : ADesde(desde));
            cmd.Parameters.AddWithValue("@FechaFinal", porFolio ? string.Empty : AHasta(hasta));
            cmd.Parameters.AddWithValue("@IDTipoRetiro", porFolio ? 0 : idTipoRetiro);
        }, ct);

    /// <summary>
    /// Alta del corte de caja.
    ///
    /// Aqui se arma el CorteInfo (20 importes pegados con '@@@@') y las cuatro
    /// listas de gastos unidas con '~'. Las cuatro salen de la MISMA lista de
    /// renglones, asi que no pueden quedar desalineadas: en legacy el front
    /// armaba las cuatro cadenas por separado y nadie comprobaba que tuvieran la
    /// misma longitud.
    ///
    /// Y SE LEE EL RESULTADO. El insert de corte era el unico de legacy que NO
    /// leia result/mensaje/IDResult del procedimiento: la respuesta siempre salia
    /// con result 0 y mensaje vacio, asi que la pantalla no podia distinguir un
    /// corte guardado de uno rechazado.
    ///
    /// @IDsCorteDetalle no se manda: legacy lo armaba y nunca lo enviaba, y el
    /// procedimiento no lo recibe.
    /// </summary>
    public Task<(ResultadoLegacy, int)> InsertarCorteAsync(
        CrearCorteRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarConIdAsync("dbo.sp_n_InsertarCorte", "IDResult", cmd =>
        {
            cmd.Parameters.AddWithValue("@FechaCorte", AFechaLegacy(r.FechaCorte));
            cmd.Parameters.AddWithValue("@CorteInfo", ArmarCorteInfo(r.Desglose));
            cmd.Parameters.AddWithValue("@IDsTipoGasto", Unir(r.Gastos.Select(x => Entero(x.IdTipoGasto))));
            cmd.Parameters.AddWithValue("@IDsEmpleadoPrestamo", Unir(r.Gastos.Select(x => Entero(x.IdEmpleadoPrestamo))));
            cmd.Parameters.AddWithValue("@Conceptos", Unir(r.Gastos.Select(x => Texto(x.Concepto))));
            cmd.Parameters.AddWithValue("@Montos", Unir(r.Gastos.Select(x => Numero(x.Monto))));
            cmd.Parameters.AddWithValue("@Observaciones", Texto(r.Observaciones));
            cmd.Parameters.AddWithValue("@ObservacionesCheque", Texto(r.ObservacionesCheque));
            cmd.Parameters.AddWithValue("@ObservacionesDepositosCheque", Texto(r.ObservacionesDepositosCheque));
            cmd.Parameters.AddWithValue("@ObservacionesDepositoEfectivo", Texto(r.ObservacionesDepositoEfectivo));
            cmd.Parameters.AddWithValue("@ObservacionesSobranteLiquidaciones", Texto(r.ObservacionesSobranteLiquidaciones));
            cmd.Parameters.AddWithValue("@LiquidacionTiendas", r.LiquidacionTiendas);
            cmd.Parameters.AddWithValue("@NoAplicado", r.NoAplicado);
            cmd.Parameters.AddWithValue("@SobranteLiquidaciones", r.SobranteLiquidaciones);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    /// <summary>
    /// Corte de retiro de centro de servicio. Solo viaja la fecha final: legacy
    /// convertia tambien una fecha inicial y la descartaba sin mandarla.
    /// </summary>
    public Task<(ResultadoLegacy, int)> InsertarCorteCentroServicioAsync(
        CrearCorteCentroServicioRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarConIdAsync("dbo.sp_n_InsertarCorteCS", "IDResult", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDTipoRetiro", r.IdTipoRetiro);
            cmd.Parameters.AddWithValue("@MontoRetiro", r.MontoRetiro);
            cmd.Parameters.AddWithValue("@MontoRetiroTarjetaCredito", r.MontoRetiroTarjetaCredito);
            cmd.Parameters.AddWithValue("@MontoRetiroTarjetaDebito", r.MontoRetiroTarjetaDebito);
            cmd.Parameters.AddWithValue("@FechaFinal", AHasta(r.FechaFinal));
            cmd.Parameters.AddWithValue("@Observaciones", Texto(r.Observaciones));
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    /// <summary>
    /// Cancela un corte, sea de caja o de centro de servicio.
    ///
    /// ES EL MISMO PROCEDIMIENTO PARA LOS DOS: legacy tenia dos endpoints
    /// (/cortes/cancelar y /cortescs/cancelar) que llamaban a
    /// sp_n_CancelarCorte con los mismos parametros. Aqui hay uno, porque dos
    /// rutas para la misma operacion solo sirven para que alguien cancele por la
    /// equivocada y crea que hizo algo distinto.
    /// </summary>
    public Task<ResultadoLegacy> CancelarCorteAsync(
        int idCorte, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_CancelarCorte", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCorte", idCorte);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    // =======================================================================
    // TRANSFERENCIAS BANCARIAS
    // =======================================================================

    /// <summary>
    /// Estados de cuenta. La precedencia es: id, luego archivo, luego fechas.
    /// Es la cascada de legacy, dicha en vez de escondida en tres
    /// reinicializaciones del DTO.
    /// </summary>
    public Task<List<Dictionary<string, object?>>> ConsultarTransferenciasAsync(
        TransferenciasFiltro filtro, CancellationToken ct)
    {
        var archivo = Texto(filtro.Archivo);
        var porId = filtro.IdBancoTransferencia > 0;
        var porArchivo = !porId && archivo.Length > 0;
        var otrosFiltros = !porId && !porArchivo;

        return ConsultarAsync("dbo.sp_n_ConsultaBancoTransferencias", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDBancoTransferencia", filtro.IdBancoTransferencia);
            cmd.Parameters.AddWithValue("@FechaInicial", otrosFiltros ? ADesde(filtro.Desde) : string.Empty);
            cmd.Parameters.AddWithValue("@FechaFinal", otrosFiltros ? AHasta(filtro.Hasta) : string.Empty);
            cmd.Parameters.AddWithValue("@IDCliente", otrosFiltros ? filtro.IdCliente : 0);
            cmd.Parameters.AddWithValue("@Referencia", otrosFiltros ? Texto(filtro.Referencia) : string.Empty);
            cmd.Parameters.AddWithValue("@Archivo", porArchivo ? archivo : string.Empty);
            cmd.Parameters.AddWithValue("@Aplicado", otrosFiltros ? filtro.Aplicado : 0);
            cmd.Parameters.AddWithValue("@Rechazado", otrosFiltros ? filtro.Rechazado : 0);
        }, ct);
    }

    /// <summary>
    /// Movimientos de un estado de cuenta. Aqui todos los filtros conviven: el
    /// procedimiento los combina y legacy no reinicializaba nada.
    ///
    /// El nombre del procedimiento tiene un error de escritura de origen
    /// ("Tranferencias", sin la s) y se deja tal cual: es el nombre que tiene en
    /// la base.
    /// </summary>
    public Task<List<Dictionary<string, object?>>> ConsultarMovimientosTransferenciaAsync(
        MovimientosTransferenciaFiltro filtro, CancellationToken ct)
        => ConsultarAsync("dbo.sp_n_ConsultaBancoTranferenciasMovimientos", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDBancoTransferencia", filtro.IdBancoTransferencia);
            cmd.Parameters.AddWithValue("@IDStatusTransferencia", filtro.IdStatusTransferencia);
            cmd.Parameters.AddWithValue("@IDsClientes", Unir(filtro.IdsClientes.Select(Entero)));
            cmd.Parameters.AddWithValue("@IDCliente", filtro.IdCliente);
            cmd.Parameters.AddWithValue("@Referencia", Texto(filtro.Referencia));
            cmd.Parameters.AddWithValue("@Aplicado", filtro.Aplicado);
            cmd.Parameters.AddWithValue("@Rechazado", filtro.Rechazado);
            cmd.Parameters.AddWithValue("@Identificado", filtro.Identificado);
            cmd.Parameters.AddWithValue("@NoIdentificado", filtro.NoIdentificado);
            cmd.Parameters.AddWithValue("@FechaInicial", ADesde(filtro.Desde));
            cmd.Parameters.AddWithValue("@FechaFinal", AHasta(filtro.Hasta));
        }, ct);

    /// <summary>
    /// Carga un estado de cuenta completo: la cabecera y todos sus movimientos,
    /// EN UNA TRANSACCION.
    ///
    /// ESTE ES EL CAMBIO MAS IMPORTANTE DEL MODULO. sp_n_InsertarBancoTransferencia
    /// inserta UN movimiento por llamada y crea la cabecera en la primera (cuando
    /// recibe @IDBancoTransferencia = 0), devolviendo su id para que las
    /// siguientes lo reusen. En legacy ese bucle vivia en el cliente: N peticiones
    /// HTTP sueltas, sin transaccion. Un archivo de 400 renglones que fallara en
    /// el 250 dejaba 249 movimientos cargados y el estado de cuenta a medias, sin
    /// forma de saber desde donde seguir ni de deshacerlo — y un estado de cuenta
    /// incompleto se concilia mal contra los pagos.
    ///
    /// Aqui el lote entero va en una transaccion: o entra completo o no entra
    /// nada. Si el procedimiento rechaza un renglon, se deshace todo y se
    /// devuelve SU mensaje, diciendo en que numero de movimiento fue.
    /// </summary>
    public async Task<(ResultadoLegacy, int, int)> CargarTransferenciasAsync(
        CargarTransferenciasRequest r, int idUsuario, string equipo, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await conn.BeginTransactionAsync(ct);

        var idCabecera = r.IdBancoTransferencia;
        var cargados = 0;

        foreach (var m in r.Movimientos)
        {
            await using var cmd = Sp("dbo.sp_n_InsertarBancoTransferencia", conn, tx);
            cmd.Parameters.AddWithValue("@IDBancoTransferencia", idCabecera);
            cmd.Parameters.AddWithValue("@RutaArchivo", Texto(r.RutaArchivo));
            cmd.Parameters.AddWithValue("@Archivo", Texto(r.Archivo));
            cmd.Parameters.AddWithValue("@SaldoInicial", r.SaldoInicial);
            cmd.Parameters.AddWithValue("@SaldoFinal", r.SaldoFinal);
            cmd.Parameters.AddWithValue("@TotalDepositos", r.TotalDepositos);
            cmd.Parameters.AddWithValue("@TotalRetiros", r.TotalRetiros);
            cmd.Parameters.AddWithValue("@NumMovimientos", r.NumMovimientos);
            cmd.Parameters.AddWithValue("@Num", m.Num);
            cmd.Parameters.AddWithValue("@Fecha", AFechaLegacy(m.Fecha));
            cmd.Parameters.AddWithValue("@Descripcion", Texto(m.Descripcion));
            cmd.Parameters.AddWithValue("@Suc", m.Suc);
            // Estos dos los CONVIERTE A decimal(22,0) el procedimiento, para
            // comparar y para insertar. Vacio se manda como "0": la cadena vacia
            // tumba la carga entera con "Error converting data type varchar to
            // numeric", y la columna destino es numerica, asi que 0 es lo que
            // significa "no hay".
            cmd.Parameters.AddWithValue("@RefNumerica", Numerico(m.RefNumerica));
            cmd.Parameters.AddWithValue("@RefAlfanumerica", Texto(m.RefAlfanumerica));
            cmd.Parameters.AddWithValue("@Autorizacion", Numerico(m.Autorizacion));
            cmd.Parameters.AddWithValue("@Ordenante", Texto(m.Ordenante));
            cmd.Parameters.AddWithValue("@BancoEmisor", Texto(m.BancoEmisor));
            cmd.Parameters.AddWithValue("@Deposito", m.Deposito);
            cmd.Parameters.AddWithValue("@Retiro", m.Retiro);
            cmd.Parameters.AddWithValue("@Saldo", m.Saldo);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);

            var dt = await FillAsync(cmd, ct);
            var resultado = BajaLogicaLegacy.LeerResultado(dt);

            if (!resultado.Ok)
            {
                /*
                  EL ROLLBACK VA PROTEGIDO, Y NO ES PARANOIA.

                  `sp_n_InsertarBancoTransferencia` hace `BEGIN TRAN` y, en su
                  camino de error, un `ROLLBACK TRAN` DESNUDO: sin @@TRANCOUNT,
                  sin SAVE TRAN. Dentro de una transaccion abierta desde C#, ese
                  rollback NO deshace "lo suyo": deshace la transaccion EXTERNA,
                  la nuestra. Y despues el procedimiento regresa normal.

                  Asi que al llegar aqui la transaccion puede estar YA deshecha, y
                  entonces `tx.RollbackAsync` lanzaba "This SqlTransaction has
                  completed". Esa excepcion se llevaba por delante el mensaje que
                  este metodo acaba de construir con tanto cuidado ("Movimiento
                  250: ...") y el usuario recibia un 500 pelado al rechazar UN
                  movimiento de su archivo.

                  El efecto en la base es el mismo -nada queda escrito, que es lo
                  que se buscaba-, asi que tragarse este fallo concreto no esconde
                  nada: solo evita que el aviso bueno se pierda.
                */
                try
                {
                    await tx.RollbackAsync(ct);
                }
                catch (InvalidOperationException)
                {
                    // El propio procedimiento ya deshizo la transaccion. El lote
                    // no quedo escrito, que es lo que importa.
                }

                var mensaje = string.IsNullOrWhiteSpace(resultado.Mensaje)
                    ? $"La base rechazo el movimiento {m.Num}."
                    : $"Movimiento {m.Num}: {resultado.Mensaje}";
                return (new ResultadoLegacy { Result = resultado.Result, Mensaje = mensaje }, 0, 0);
            }

            // La primera llamada crea la cabecera y devuelve su id; de ahi en
            // adelante se reusa para que todos los movimientos queden en el mismo
            // estado de cuenta.
            if (idCabecera == 0 && dt.Rows.Count > 0)
                idCabecera = BajaLogicaLegacy.LeerInt(dt.Rows[0], "IDResult");

            cargados++;
        }

        await tx.CommitAsync(ct);
        return (new ResultadoLegacy { Result = 1, Mensaje = string.Empty }, idCabecera, cargados);
    }

    public Task<ResultadoLegacy> IdentificarTransferenciasAsync(
        IdentificarTransferenciasRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_IdentificarBancoTransferencia", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDsBancoTransferenciaMovimiento", Unir(r.IdsMovimiento.Select(Entero)));
            cmd.Parameters.AddWithValue("@IDBancoTransferencia", r.IdBancoTransferencia);
            cmd.Parameters.AddWithValue("@IDEmpresa", r.IdEmpresa);
            cmd.Parameters.AddWithValue("@IDCliente", r.IdCliente);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    /// <summary>
    /// Aplica el movimiento a las ventas. AQUI SE MUEVE DINERO.
    ///
    /// El procedimiento (dinero o cascos) lo decide el controlador a partir de un
    /// valor obligatorio, no de dos banderas sueltas: en legacy, con las dos en
    /// false se armaba un SqlCommand con nombre VACIO y el usuario leia
    /// "[No se pudo procesar ] - ..." sin saber que le faltaba elegir.
    ///
    /// Las dos listas '~' (ventas e importes) salen de la misma lista de pares.
    /// </summary>
    public Task<ResultadoLegacy> AplicarTransferenciaAsync(
        AplicarTransferenciaRequest r, string sp, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync(sp, cmd =>
        {
            cmd.Parameters.AddWithValue("@IDBancoTransferenciaMovimiento", r.IdMovimiento);
            cmd.Parameters.AddWithValue("@IDBancoTransferencia", r.IdBancoTransferencia);
            cmd.Parameters.AddWithValue("@IDEmpresa", r.IdEmpresa);
            cmd.Parameters.AddWithValue("@IDsVentas", Unir(r.Pagos.Select(x => Entero(x.IdVenta))));
            cmd.Parameters.AddWithValue("@MontoPagos", Unir(r.Pagos.Select(x => Numero(x.Monto))));
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    /// <summary>
    /// Cancela un movimiento bancario ya aplicado: revierte el pago en Ventas,
    /// [Ventas Pagos], [Ventas Pagos Usados] y el saldo del cliente.
    ///
    /// SE MANDA EL ID DEL MOVIMIENTO, NO EL DEL ESTADO DE CUENTA, y eso corrige un
    /// defecto que hoy tiene esta operacion rota en esta base: el procedimiento
    /// declara @IDBancoTransferenciaMovimiento (leido de sys.parameters en
    /// Produccion_svr) y el repositorio de legacy le manda un
    /// @IDBancoTransferencia, parametro que no existe. SQL Server rechaza la
    /// llamada antes de ejecutar nada, asi que cancelar una transferencia desde
    /// MacServicios2 no funciona. El nombre del procedimiento confunde, pero lo
    /// que cancela es un movimiento.
    /// </summary>
    public Task<ResultadoLegacy> CancelarMovimientoTransferenciaAsync(
        int idMovimiento, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_CancelarBancoTransferencia", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDBancoTransferenciaMovimiento", idMovimiento);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    // =======================================================================
    // CAMBIOS DE VIGENCIA
    // =======================================================================

    public Task<List<Dictionary<string, object?>>> ConsultarCambiosVigenciaAsync(
        CambiosVigenciaFiltro filtro, CancellationToken ct)
    {
        var porId = filtro.IdCambioVigencia > 0;

        return ConsultarAsync("dbo.sp_n_ConsultarCambiosVigencia", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCambioVigencia", filtro.IdCambioVigencia);
            cmd.Parameters.AddWithValue("@IDStatusCambioVigencia", porId ? 0 : filtro.IdStatusCambioVigencia);
            cmd.Parameters.AddWithValue("@IDCliente", porId ? 0 : filtro.IdCliente);
            cmd.Parameters.AddWithValue("@IDUsuarioRecibe", porId ? 0 : filtro.IdUsuarioRecibe);
            cmd.Parameters.AddWithValue("@FolioInicial", porId ? 0 : filtro.FolioInicial);
            cmd.Parameters.AddWithValue("@FolioFinal", porId ? 0 : filtro.FolioFinal);
            cmd.Parameters.AddWithValue("@FechaInicial", porId ? string.Empty : ADesde(filtro.Desde));
            cmd.Parameters.AddWithValue("@FechaFinal", porId ? string.Empty : AHasta(filtro.Hasta));
        }, ct);
    }

    /*
      EL DETALLE: UN RENGLON POR PRODUCTO.

      ES LECTURA PURA, Y SE COMPROBO LEYENDO EL PROCEDIMIENTO EN LA BASE
      -----------------------------------------------------------------
      sp_n_ConsultarCambiosVigenciaDetalle, leido de sys.sql_modules en MacZ el
      07-10-2026: lo unico que escribe es la tabla temporal
      #ConsultarCambiosVigenciaTemp (un CREATE TABLE y un INSERT ... EXEC(@Sql)),
      y despues un SELECT de esa temporal. Cero INSERT/UPDATE/DELETE contra
      tablas reales. El nombre dice "Consultar" y esta vez es verdad -- que es
      exactamente lo que hay que comprobar antes de exponer cualquier sp_n_, y no
      dar por bueno (sp_n_ConsultaConstantes se llama "Consulta" y hace el cierre
      del dia).

      LO QUE DEVUELVE QUE LA CABECERA NO TIENE: IDProducto, Marca, Clave (el
      Codigo del producto), Ticket (el VALE), Vigencia y Garantia por renglon, mas
      el bloque de entregada POR RENGLON (UsuEnt sale de
      cvDetalle.IDUsuarioEntregada, no de la cabecera).

      NO SE FILTRA NADA MAS QUE EL ID porque el procedimiento solo declara
      @IDCambioVigencia. La cabecera se repite en cada renglon: es un JOIN plano,
      no dos tablas, y asi lo manda la base.

      Mac31 llama a este mismo procedimiento y NO pinta sus renglones (carga la
      cabecera de la primera fila y nunca llena la rejilla de detalle). Aqui si se
      publican: el dato existe y el documento tiene ocho productos.
    */
    public Task<List<Dictionary<string, object?>>> ConsultarCambiosVigenciaDetalleAsync(
        int idCambioVigencia, CancellationToken ct)
        => ConsultarAsync("dbo.sp_n_ConsultarCambiosVigenciaDetalle", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCambioVigencia", idCambioVigencia);
        }, ct);

    /// <summary>
    /// Alta de un cambio de vigencia.
    ///
    /// Las tres listas '~' (productos, vigencias, garantias) salen de la misma
    /// lista de renglones, y @NumProductos lo cuenta el backend: en legacy ese
    /// numero venia del cuerpo y podia contradecir a las listas sin que nada lo
    /// comprobara.
    /// </summary>
    public Task<(ResultadoLegacy, int)> InsertarCambioVigenciaAsync(
        CrearCambioVigenciaRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarConIdAsync("dbo.sp_n_InsertarCambioVigencia", "IDResult", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCliente", r.IdCliente);
            cmd.Parameters.AddWithValue("@IDUsuarioRecibe", r.IdUsuarioRecibe);
            cmd.Parameters.AddWithValue("@NumProductos", r.Productos.Count);
            cmd.Parameters.AddWithValue("@IDsProducto", Unir(r.Productos.Select(x => Entero(x.IdProducto))));
            cmd.Parameters.AddWithValue("@Vigencias", Unir(r.Productos.Select(x => Texto(x.Vigencia))));
            cmd.Parameters.AddWithValue("@Garantias", Unir(r.Productos.Select(x => Texto(x.Garantia))));
            cmd.Parameters.AddWithValue("@Observaciones", Texto(r.Observaciones));
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    public Task<ResultadoLegacy> ActualizarCambioVigenciaAsync(
        int id, ActualizarCambioVigenciaRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarCambioVigencia", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCambioVigencia", id);
            cmd.Parameters.AddWithValue("@IDStatusCambioVigencia", r.IdStatusCambioVigencia);
            cmd.Parameters.AddWithValue("@IDCliente", r.IdCliente);
            cmd.Parameters.AddWithValue("@IDRepartidor", r.IdUsuarioRecibe);
            cmd.Parameters.AddWithValue("@IDProducto", r.IdProducto);
            cmd.Parameters.AddWithValue("@Fecha", AFechaLegacy(r.Fecha));
            cmd.Parameters.AddWithValue("@Ticket", Texto(r.Ticket));
            cmd.Parameters.AddWithValue("@Vigencia", Texto(r.Vigencia));
            cmd.Parameters.AddWithValue("@Garantia", Texto(r.Garantia));
            cmd.Parameters.AddWithValue("@Observaciones", Texto(r.Observaciones));
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    /// <summary>
    /// Cancela el cambio de vigencia.
    ///
    /// EL MOTIVO NO SE PIDE porque el procedimiento no lo guarda: recibe solo el
    /// id, el usuario y el equipo. Legacy aceptaba un campo Observaciones en el
    /// cuerpo, lo saneaba y lo tiraba sin mandarlo — el capturista escribia el
    /// motivo de la cancelacion y no quedaba en ninguna parte. Pedirlo aqui seria
    /// repetir esa mentira.
    /// </summary>
    public Task<ResultadoLegacy> CancelarCambioVigenciaAsync(
        int id, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_CancelarCambioVigencia", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCambioVigencia", id);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    /// <summary>Devuelve el cambio de vigencia al estado anterior. Mismo caso que cancelar.</summary>
    public Task<ResultadoLegacy> RegresarCambioVigenciaAsync(
        int id, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_RegresarCambioVigencia", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCambioVigencia", id);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);
}
