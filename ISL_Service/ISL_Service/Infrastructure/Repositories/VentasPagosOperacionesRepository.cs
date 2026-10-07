using System.Data;
using ISL_Service.Application.DTOs.VentasEscrituras;
using ISL_Service.Application.DTOs.VentasPagosOperaciones;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/*
  PAGOS DE VENTA Y OPERACIONES SUELTAS DE LA REMISION

  Dos hallazgos de legacy que valen la pena antes del codigo:

  1) sp_n_LimpiarCSVentasPagos SE LLAMA SIEMPRE CON CEROS.

     El repositorio de legacy construye la cadena de traza con los valores del
     DTO y despues manda los parametros HARDCODEADOS en 0:

         command.Parameters.AddWithValue("@IDPagoVenta", 0);
         command.Parameters.AddWithValue("@IDVenta", 0);

     O sea que es una limpieza GLOBAL de los pagos de centro de servicio, no la
     de un pago. El endpoint no recibe nada que importe, y su controlador no
     valida nada —ni que venga el cuerpo—. Aqui se respeta el comportamiento
     (ceros) y se dice en el nombre: el endpoint no acepta ids.

  2) sp_n_CancelarVentasPagosKilos NO EXISTE EN ESTA BASE.

     Legacy lo llama con cinco parametros (@IDPagoVenta, @IDPagoVentaUsado,
     @IDVenta, @IDUsuario, @Equipo) y, curiosamente, NO le manda
     IDVentaPagoKilo, que es el unico campo que su controlador valida. En
     Produccion_svr el procedimiento no esta: existe sp_n_ConsultarVentasPagosKilos
     (la consulta) pero no el de cancelar. Legacy falla igual que esto, asi que
     el endpoint queda implementado y no verificable, y el comparador lo recoge
     como tal.

  3) EsLuro SE LEE DE LA TABLA, NO DEL PROCEDIMIENTO.

     sp_n_VentasDetalleMultiple recibe @EsLuro, y legacy lo saca de
     sp_n_ConsultaConstantes — que es la rutina de CIERRE DEL DIA. Aqui se lee
     con un SELECT a dbo.Constantes, que da el mismo valor sin congelar el
     inventario del dia anterior ni adelantar la fecha de operacion. Es el mismo
     patron que ya usa VentasPagosRepository.EsquemaPagoAsync.
*/
public class VentasPagosOperacionesRepository : SpLegacyRepositoryBase, IVentasPagosOperacionesRepository
{
    private const string SpIncobrable = "sp_n_InsertarVentasPagosIncobrable";
    private const string SpAbonarKilos = "sp_n_InsertarVentasPagosKilos";
    private const string SpCancelarAbonoKilos = "sp_n_CancelarVentasPagosKilos";
    private const string SpLimpiarCs = "sp_n_LimpiarCSVentasPagos";
    private const string SpCrearNuevoPago = "sp_n_InsertarVentasNuevosPagos";
    private const string SpAplicarNuevoPago = "sp_n_AplicarVentasNuevosPagos";
    private const string SpCancelarNuevoPago = "sp_n_CancelarVentasNuevosPagos";
    private const string SpCancelarTransferencia = "sp_n_CancelarBancoTransferencia";
    private const string SpCancelarTransferenciaPago = "sp_n_CancelarBancoTransferenciaPago";
    private const string SpReasignar = "sp_n_VentasReasignacion";
    private const string SpActualizarVenta = "sp_n_ActualizarVenta";
    private const string SpPagoPorKilo = "sp_n_ActualizarVentasPagoPorKilo";
    private const string SpDetalle = "sp_n_VentasDetalle";
    private const string SpDetalleMultiple = "sp_n_VentasDetalleMultiple";

    private const int LargoLista = 4000;
    private const int LargoTexto = 4000;
    private const int LargoTextoCorto = 1000;
    private const int LargoEquipoCorto = 400;
    private const int LargoEquipoLargo = 1000;
    private const int LargoTarjeta = 100;

    public VentasPagosOperacionesRepository(IConfiguration configuration) : base(configuration) { }

    public async Task<ResultadoLegacyDto> DeclararIncobrableAsync(
        VentaIncobrableRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpIncobrable, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDVenta", request.IdVenta));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipoLargo, FiltrosLegacy.Texto(equipo, LargoEquipoLargo)));
        }, ct);

        return Resultado(tablas, "IDPagoVenta");
    }

    public async Task<ResultadoLegacyDto> AbonarKilosAsync(
        VentaPagoKilosRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpAbonarKilos, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDCobro", request.IdCobro));
            cmd.Parameters.Add(Entero("@IDVenta", request.IdVenta));
            cmd.Parameters.Add(Entero("@IDCliente", request.IdCliente));
            cmd.Parameters.Add(Decimal18("@AbonoKilos", request.AbonoKilos));
            cmd.Parameters.Add(Varchar("@Observaciones", LargoTextoCorto, FiltrosLegacy.Texto(request.Observaciones, LargoTextoCorto)));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipoCorto, FiltrosLegacy.Texto(equipo, LargoEquipoCorto)));
        }, ct);

        return Resultado(tablas, "IDVentaPagoKilo");
    }

    public async Task<ResultadoLegacyDto> CancelarAbonoKilosAsync(
        VentaPagoKilosCancelarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        /*
          LOS PARAMETROS SON LOS QUE MANDA LEGACY, NO LOS QUE PARECERIAN.

          sp_n_CancelarVentasPagosKilos no esta en esta base, asi que la firma
          que se respeta es la que usa el repositorio de MacServicios2 — que es
          la unica evidencia que hay de como se llama. Y ahi hay una rareza que
          se conserva a proposito: NO se manda el IDVentaPagoKilo, que es el
          unico campo que su controlador valida. Cambiarlo seria adivinar la
          firma de un procedimiento que no se puede leer.
        */
        var tablas = await EjecutarTablasAsync(SpCancelarAbonoKilos, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDPagoVenta", request.IdPagoVenta));
            cmd.Parameters.Add(Entero("@IDPagoVentaUsado", request.IdPagoVentaUsado));
            cmd.Parameters.Add(Entero("@IDVenta", request.IdVenta));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipoLargo, FiltrosLegacy.Texto(equipo, LargoEquipoLargo)));
        }, ct);

        return Resultado(tablas, "IDVentaPagoKilo");
    }

    public async Task<ResultadoLegacyDto> LimpiarPagosCentroServicioAsync(
        int idPagoVenta, int idVenta, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpLimpiarCs, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDPagoVenta", idPagoVenta));
            cmd.Parameters.Add(Entero("@IDVenta", idVenta));
        }, ct);

        return Resultado(tablas, "IDPagoVenta");
    }

    public async Task<ResultadoLegacyDto> CrearNuevoPagoAsync(
        NuevoPagoCrearRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpCrearNuevoPago, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDCliente", request.IdCliente));
            cmd.Parameters.Add(Entero("@IDTipoPago", request.IdTipoPago));
            cmd.Parameters.Add(Decimal18("@Cargo", request.Cargo));
            cmd.Parameters.Add(Decimal18("@Abono", request.Abono));
            cmd.Parameters.Add(Decimal18("@MontoTotal", request.MontoTotal));
            cmd.Parameters.Add(Entero("@IDBancoTarjeta", request.IdBancoTarjeta));
            cmd.Parameters.Add(Varchar("@NumeroTarjeta", LargoTarjeta, FiltrosLegacy.Texto(request.NumeroTarjeta, LargoTarjeta)));
            cmd.Parameters.Add(Entero("@TipoTarjeta", request.TipoTarjeta));
            cmd.Parameters.Add(Varchar("@Motivo", LargoTextoCorto, FiltrosLegacy.Texto(request.Motivo, LargoTextoCorto)));
            cmd.Parameters.Add(Varchar("@Observaciones", LargoTexto, FiltrosLegacy.Texto(request.Observaciones)));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipoLargo, FiltrosLegacy.Texto(equipo, LargoEquipoLargo)));
        }, ct);

        return Resultado(tablas, "IDNuevoPagoVenta");
    }

    public async Task<ResultadoLegacyDto> AplicarNuevoPagoAsync(
        NuevoPagoRequest request, int idUsuario, string equipo, CancellationToken ct)
        => await NuevoPagoAsync(SpAplicarNuevoPago, request, idUsuario, equipo, ct);

    public async Task<ResultadoLegacyDto> CancelarNuevoPagoAsync(
        NuevoPagoRequest request, int idUsuario, string equipo, CancellationToken ct)
        => await NuevoPagoAsync(SpCancelarNuevoPago, request, idUsuario, equipo, ct);

    private async Task<ResultadoLegacyDto> NuevoPagoAsync(
        string procedimiento, NuevoPagoRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(procedimiento, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDNuevoPagoVenta", request.IdNuevoPagoVenta));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipoCorto, FiltrosLegacy.Texto(equipo, LargoEquipoCorto)));
        }, ct);

        return Resultado(tablas, "IDNuevoPagoVenta");
    }

    public async Task<ResultadoLegacyDto> CancelarTransferenciaAsync(
        TransferenciaCancelarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        /*
          ESTE ES EL MAS LARGO DE TODA LA OLA EN EFECTO DE BASE.

          sp_n_CancelarBancoTransferencia actualiza BancoTranferencias y sus
          movimientos, Ventas, [Ventas Pagos], [Ventas Pagos Usados] y Clientes,
          e inserta en CobrosDineroUsadoClientesLog. El clasificador de
          escrituras se lo habia perdido porque el DML va dentro de SQL dinamico.
          Todo pasa dentro del procedimiento, en su propia transaccion.
        */
        var tablas = await EjecutarTablasAsync(SpCancelarTransferencia, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDBancoTransferenciaMovimiento", request.IdBancoTransferenciaMovimiento));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipoCorto, FiltrosLegacy.Texto(equipo, LargoEquipoCorto)));
        }, ct);

        return Resultado(tablas, "IDBancoTransferenciaMovimiento");
    }

    public async Task<ResultadoLegacyDto> CancelarTransferenciaPagoAsync(
        TransferenciaPagoCancelarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpCancelarTransferenciaPago, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDPagoVenta", request.IdPagoVenta));
            cmd.Parameters.Add(Entero("@IDPagoVentaUsado", request.IdPagoVentaUsado));
            cmd.Parameters.Add(Entero("@IDEmpresa", request.IdEmpresa));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipoCorto, FiltrosLegacy.Texto(equipo, LargoEquipoCorto)));
        }, ct);

        /*
          LOS CUATRO SALDOS BAJAN COMO COLUMNAS, NO PEGADOS CON PIPE.

          El procedimiento devuelve SaldoFavorDineroCliente,
          SaldoFavorCascosCliente, SaldoFavorDineroVenta y SaldoFavorCascosVenta.
          Legacy los concatena en un solo campo con "|" y el cliente los parte
          por posicion: cuatro numeros de dinero cuya lectura depende de que las
          dos mitades recuerden el orden. Aqui van como vinieron, en `filas`.
        */
        return Resultado(tablas, "IDPagoVenta");
    }

    public async Task<ResultadoLegacyDto> ReasignarAsync(
        VentasReasignarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpReasignar, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDCliente", request.IdCliente));
            cmd.Parameters.Add(Entero("@IDAgente", request.IdAgente));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Entero("@CambiarCliente", request.CambiarCliente));
            cmd.Parameters.Add(Varchar("@IDsVenta", LargoLista, FiltrosLegacy.Ids(request.IdsVenta)));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipoCorto, FiltrosLegacy.Texto(equipo, LargoEquipoCorto)));
            cmd.Parameters.Add(Varchar("@Motivo", LargoTexto, FiltrosLegacy.Texto(request.Motivo)));
        }, ct);

        return Resultado(tablas, "IDVenta");
    }

    public async Task<ResultadoLegacyDto> ActualizarVentaAsync(
        VentaActualizarRequest request, CancellationToken ct)
    {
        /* Dos parametros y ninguno de firma: el procedimiento no los declara.
           No se le inventan. */
        var tablas = await EjecutarTablasAsync(SpActualizarVenta, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDVenta", request.IdVenta));
            cmd.Parameters.Add(Varchar("@Observaciones", LargoTexto, FiltrosLegacy.Texto(request.Observaciones)));
        }, ct);

        return Resultado(tablas, "IDVenta");
    }

    public async Task<ResultadoLegacyDto> MarcarPagoPorKiloAsync(
        VentasPagoPorKiloRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpPagoPorKilo, cmd =>
        {
            cmd.Parameters.Add(Varchar("@IDsVenta", LargoLista, FiltrosLegacy.Ids(request.IdsVenta)));
            cmd.Parameters.Add(Entero("@PagoPorKilo", request.PagoPorKilo));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipoCorto, FiltrosLegacy.Texto(equipo, LargoEquipoCorto)));
        }, ct);

        return Resultado(tablas, "IDVenta");
    }

    public async Task<FilasLegacyDto> ConsultarDetalleAsync(
        VentasDetalleConsultaRequest request, CancellationToken ct)
    {
        if (request.IdsVenta.Count > 0)
        {
            var esLuro = await EsLuroAsync(ct);
            var filasMultiple = await EjecutarFilasAsync(SpDetalleMultiple, cmd =>
            {
                cmd.Parameters.Add(Varchar("@IDsVenta", LargoLista, FiltrosLegacy.Ids(request.IdsVenta)));
                cmd.Parameters.Add(Entero("@EsLuro", esLuro));
            }, ct);

            return new FilasLegacyDto { Rows = filasMultiple };
        }

        var filas = await EjecutarFilasAsync(SpDetalle, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDVenta", request.IdVenta));
            cmd.Parameters.Add(Entero("@IDDescuento", request.IdDescuento));
        }, ct);

        return new FilasLegacyDto { Rows = filas };
    }

    /// <summary>
    /// `EsLuro` de dbo.Constantes, leido con un SELECT.
    ///
    /// Legacy lo saca de sp_n_ConsultaConstantes, que ADEMAS de leer hace el
    /// cierre del dia cuando la fecha de operacion esta atrasada: borra y rehace
    /// AlmacenProductoHist y adelanta Constantes.FechaOperacion. Una consulta de
    /// partidas no tiene por que hacer eso. El valor es el mismo; la diferencia
    /// es que esto no escribe.
    /// </summary>
    private async Task<int> EsLuroAsync(CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        await using var cmd = new SqlCommand(
            "SELECT TOP 1 ISNULL(EsLuro, 0) FROM dbo.Constantes;", conn)
        { CommandTimeout = CommandTimeoutSeconds };

        var valor = await cmd.ExecuteScalarAsync(ct);
        if (valor is null || valor == DBNull.Value) return 0;
        return Convert.ToInt32(valor, System.Globalization.CultureInfo.InvariantCulture);
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
