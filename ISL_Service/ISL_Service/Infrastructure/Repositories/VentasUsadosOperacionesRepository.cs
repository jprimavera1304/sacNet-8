using ISL_Service.Application.DTOs.VentasEscrituras;
using ISL_Service.Application.DTOs.VentasUsadosOperaciones;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Infrastructure.Repositories;

/*
  LOS CASCOS USADOS, CONTRA LOS MISMOS PROCEDIMIENTOS QUE MAC31

  Veinte procedimientos de legacy, ninguno nuevo y ninguno tocado. El orden y el
  nombre de cada parametro se comprobo dos veces: contra el repositorio de
  MacServicios2 (quien los llama hoy) y contra sys.parameters de la base (quien
  los declara). Donde legacy manda un parametro que el procedimiento no declara
  —o al reves— va anotado en el metodo.

  LAS TRES COSAS QUE ESTABAN EN C# Y AQUI SE HACEN DISTINTO
  --------------------------------------------------------
  1) LA VIRGULILLA. Legacy pega las listas con string.Join("~", lista) justo
     antes de llamar. Se mantiene el separador porque es lo que los sp_n_ parten,
     pero el pegado vive aqui y pasa por FiltrosLegacy, que ademas formatea los
     decimales con punto y cultura invariante. Legacy no lo hace: con la cultura
     del servidor en espanol, un 12,50 con coma PARTE la cadena y el
     procedimiento lee dos renglones donde habia uno. Es un error latente que no
     se hereda.

  2) LA FECHA. Legacy leia `formatoFechaDB` de Constantes —con
     sp_n_ConsultaConstantes, que es la rutina de CIERRE DEL DIA— y volteaba la
     cadena con Substring de posiciones fijas. Aqui la fecha se manda en
     "yyyy-MM-dd HH:mm:ss", que SQL Server entiende con cualquier DATEFORMAT.
     Consecuencia practica: una consulta de cascos usados ya no puede adelantar
     la fecha de operacion del negocio ni rehacer el corte de inventario.

  3) EL RESET DEL DTO. En las cuatro consultas, legacy construye un DTO NUEVO
     cuando viene un id o un folio, y con eso TIRA todos los demas filtros sin
     avisar. Eso no es un detalle: tambien tira `IDUsuarioActual`, que en otras
     pantallas es el candado de almacen. Aqui la prioridad de filtros se decide
     en el servicio, de forma explicita y con nombre, y se deja anotado en cada
     metodo cual gana.

  LO QUE NO SE HEREDA
  -------------------
  Legacy valida `IDUsuario == 0` en el controlador y luego manda ese mismo
  IDUsuario al procedimiento, leido del cuerpo. Aqui el idUsuario llega como
  parametro desde el controlador, sacado del token, y por eso ninguno de estos
  metodos lo lee de un request.
*/
public class VentasUsadosOperacionesRepository : SpLegacyRepositoryBase, IVentasUsadosOperacionesRepository
{
    private const string SpCrearCobroDinero = "sp_n_InsertarCobroDineroUsado";
    private const string SpActualizarCobroDinero = "sp_n_ActualizarCobroDineroUsado";
    private const string SpAplicarCobroDinero = "sp_n_AplicarCobroDineroUsado";
    private const string SpCancelarCobroDinero = "sp_n_CancelarAplicacionCobroDineroCasco";
    private const string SpConsultarCobrosDinero = "sp_n_ConsultarCobrosDineroUsados";
    private const string SpConsultarClientesDelCobro = "sp_n_ConsultarCobroClientes";
    private const string SpCrearCheque = "sp_n_InsertarCobroDineroUsadoCheques";
    private const string SpActualizarCheque = "sp_n_ActualizarCobroDineroUsadoCheques";
    private const string SpConsultarCheques = "sp_n_ConsultarCobrosDineroUsadosCheques";
    private const string SpCrearCorteCs = "sp_n_InsertarCobroDineroUsadoCortesCS";
    private const string SpEliminarCorteCs = "sp_n_EliminarCobroDineroUsadoCortesCS";
    private const string SpConsultarCortesCs = "sp_n_ConsultarCobrosDineroUsadosCortesCS";
    private const string SpCrearCobroUsado = "sp_n_InsertarVentasCobroUsados";
    private const string SpCancelarCobroUsado = "sp_n_CancelarVentasCobroUsados";
    private const string SpConsultarCobrosUsado = "sp_n_ConsultarVentasCobrosUsados";
    private const string SpCrearExcedente = "sp_n_InsertarVentasUsadosExcendentes";
    private const string SpCancelarExcedente = "sp_n_CancelarVentasUsadosExcendentes";
    private const string SpConsultarExcedentes = "sp_n_ConsultarVentasUsadosExcendentes";
    private const string SpCrearMovimiento = "sp_n_InsertarUsadoMovimiento";
    private const string SpCancelarMovimiento = "sp_n_CancelarUsadoMovimiento";
    private const string SpConsultarMovimientos = "sp_n_ConsultarUsadosMovimientos";
    private const string SpConsultarMovimientosPagados = "sp_n_ConsultarUsadosMovimientosPagados";

    /* Los largos reales de cada parametro, leidos de sys.parameters. */
    private const int LargoLista = 4000;
    private const int LargoTexto = 4000;
    private const int LargoEquipo = 400;
    private const int LargoFecha = 20;
    private const int LargoVale = 100;
    private const int LargoNumeroCheque = 50;

    public VentasUsadosOperacionesRepository(IConfiguration configuration) : base(configuration) { }

    /* ============================================================ */
    /* Cobros de dinero por cascos                                   */
    /* ============================================================ */

    public async Task<ResultadoLegacyDto> CrearCobroDineroAsync(
        CobroDineroUsadoCrearRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpCrearCobroDinero, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDCobroDineroUsado", request.IdCobroDineroUsado));
            cmd.Parameters.Add(Entero("@IDCobro", request.IdCobro));
            cmd.Parameters.Add(Varchar("@Observaciones", LargoTexto, FiltrosLegacy.Texto(request.Observaciones)));
            cmd.Parameters.Add(Varchar("@IDsTipoUsado", LargoLista, FiltrosLegacy.IdsUsados(request.IdsTipoUsado)));
            cmd.Parameters.Add(Varchar("@PreciosUnitario", LargoLista, FiltrosLegacy.ImportesUsados(request.PreciosUnitario)));
            cmd.Parameters.Add(Varchar("@Cantidades", LargoLista, FiltrosLegacy.IdsUsados(request.Cantidades)));
            cmd.Parameters.Add(Decimal18("@Efectivo", request.Efectivo));
            cmd.Parameters.Add(Decimal18("@Cheque", request.Cheque));
            cmd.Parameters.Add(Decimal18("@Transferencia", request.Transferencia));
            cmd.Parameters.Add(Decimal18("@Tarjeta", request.Tarjeta));
            cmd.Parameters.Add(Decimal18("@DepositoEfectivo", request.DepositoEfectivo));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipo, FiltrosLegacy.Texto(equipo, LargoEquipo)));
        }, ct);

        return Resultado(tablas, "IDResult");
    }

    public async Task<ResultadoLegacyDto> RepartirCobroDineroAsync(
        CobroDineroUsadoRepartoRequest request, bool aplicar, int idUsuario, string equipo, CancellationToken ct)
    {
        /*
          LOS DOS PROCEDIMIENTOS RECIBEN LO MISMO, Y NO HACEN LO MISMO.

          sp_n_ActualizarCobroDineroUsado guarda el reparto y
          sp_n_AplicarCobroDineroUsado lo aplica a los saldos de los clientes.
          En legacy la eleccion viene de un entero del cuerpo (`esAplicacion`),
          y si el cliente manda cualquier cosa que no sea 0 ni 1 el repositorio
          se queda con el nombre de procedimiento vacio y revienta. Aqui lo
          decide la RUTA, que no se puede equivocar.
        */
        var procedimiento = aplicar ? SpAplicarCobroDinero : SpActualizarCobroDinero;

        var tablas = await EjecutarTablasAsync(procedimiento, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDCobroDineroUsado", request.IdCobroDineroUsado));
            cmd.Parameters.Add(Varchar("@IDsClientes", LargoLista, FiltrosLegacy.IdsUsados(request.IdsClientes)));
            cmd.Parameters.Add(Varchar("@ClientesNuevos", LargoLista, FiltrosLegacy.IdsUsados(request.ClientesNuevos)));
            cmd.Parameters.Add(Varchar("@IDsTipoUsado", LargoLista, FiltrosLegacy.IdsUsados(request.IdsTipoUsado)));
            cmd.Parameters.Add(Varchar("@CantidadesG1", LargoLista, FiltrosLegacy.IdsUsados(request.CantidadesG1)));
            cmd.Parameters.Add(Varchar("@CantidadesG2", LargoLista, FiltrosLegacy.IdsUsados(request.CantidadesG2)));
            cmd.Parameters.Add(Varchar("@CantidadesG3", LargoLista, FiltrosLegacy.IdsUsados(request.CantidadesG3)));
            cmd.Parameters.Add(Varchar("@CantidadesG4", LargoLista, FiltrosLegacy.IdsUsados(request.CantidadesG4)));
            cmd.Parameters.Add(Varchar("@CantidadesG5", LargoLista, FiltrosLegacy.IdsUsados(request.CantidadesG5)));
            cmd.Parameters.Add(Varchar("@CantidadesG6", LargoLista, FiltrosLegacy.IdsUsados(request.CantidadesG6)));
            cmd.Parameters.Add(Varchar("@CantidadesG7", LargoLista, FiltrosLegacy.IdsUsados(request.CantidadesG7)));
            cmd.Parameters.Add(Varchar("@CantidadesMoto", LargoLista, FiltrosLegacy.IdsUsados(request.CantidadesMoto)));
            cmd.Parameters.Add(Varchar("@PagosEfectivo", LargoLista, FiltrosLegacy.ImportesUsados(request.PagosEfectivo)));
            cmd.Parameters.Add(Varchar("@PagosTarjeta", LargoLista, FiltrosLegacy.ImportesUsados(request.PagosTarjeta)));
            cmd.Parameters.Add(Varchar("@ObservacionesAplicacion", LargoTexto, FiltrosLegacy.Texto(request.ObservacionesAplicacion)));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipo, FiltrosLegacy.Texto(equipo, LargoEquipo)));
        }, ct);

        return Resultado(tablas, "IDResult");
    }

    public async Task<ResultadoLegacyDto> CancelarCobroDineroAsync(
        CobroDineroUsadoCancelarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        /*
          SOLO EXISTE LA CANCELACION DE LA APLICACION.

          En el repositorio de legacy hay un sp_n_CancelarCobroDineroCasco
          declarado y la eleccion entre los dos esta COMENTADA con "////": el
          nombre del procedimiento se fuerza siempre al de la aplicacion. O sea
          que cancelar el cobro entero no esta en servicio desde quien sabe
          cuando. No se resucita aqui: una operacion que nadie ha podido
          ejecutar en anos no tiene como estar probada.
        */
        var tablas = await EjecutarTablasAsync(SpCancelarCobroDinero, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDCobroDineroUsado", request.IdCobroDineroUsado));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipo, FiltrosLegacy.Texto(equipo, LargoEquipo)));
        }, ct);

        return Resultado(tablas, "IDResult");
    }

    public async Task<FilasLegacyDto> ConsultarCobrosDineroAsync(
        CobroDineroUsadoConsultaRequest request, CancellationToken ct)
    {
        /*
          LAS DOS FECHAS DE MODIFICACION VAN SIN SUFIJO DE HORA, COMO EN LEGACY.

          Legacy les pone 00:00:00 y 23:59:59 a FechaInicial/FechaFinal y a
          estas dos NO: las manda tal como las escribio el cliente. Aqui se
          respeta, y por eso usan Fecha() y no FechaDesde()/FechaHasta(): si se
          "arreglara", el rango de la pantalla de modificaciones cambiaria de
          significado y nadie lo habria pedido.
        */
        var filas = await EjecutarFilasAsync(SpConsultarCobrosDinero, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDCobroDineroUsado", request.IdCobroDineroUsado));
            cmd.Parameters.Add(Entero("@IDCobro", request.IdCobro));
            cmd.Parameters.Add(Entero("@IDRepartidor", request.IdRepartidor));
            cmd.Parameters.Add(Entero("@FolioInicial", request.FolioInicial));
            cmd.Parameters.Add(Entero("@FolioFinal", request.FolioFinal));
            cmd.Parameters.Add(Varchar("@FechaInicial", LargoFecha, FiltrosLegacy.FechaDesde(request.FechaInicial)));
            cmd.Parameters.Add(Varchar("@FechaFinal", LargoFecha, FiltrosLegacy.FechaHasta(request.FechaFinal)));
            cmd.Parameters.Add(Varchar("@FechaModificacionInicial", LargoFecha, FiltrosLegacy.Fecha(request.FechaModificacionInicial)));
            cmd.Parameters.Add(Varchar("@FechaModificacionFinal", LargoFecha, FiltrosLegacy.Fecha(request.FechaModificacionFinal)));
        }, ct);

        return new FilasLegacyDto { Rows = filas };
    }

    public async Task<FilasLegacyDto> ConsultarClientesDelCobroAsync(
        CobroDineroUsadoClientesConsultaRequest request, CancellationToken ct)
    {
        /* Un solo parametro: la cobranza y el concentrado los deduce el propio
           procedimiento de la tabla Cobros. */
        var filas = await EjecutarFilasAsync(SpConsultarClientesDelCobro, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDCobro", request.IdCobro));
        }, ct);

        return new FilasLegacyDto { Rows = filas };
    }

    public async Task<ResultadoLegacyDto> CrearChequeAsync(
        CobroDineroUsadoChequeCrearRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpCrearCheque, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDCobroDineroUsado", request.IdCobroDineroUsado));
            cmd.Parameters.Add(Entero("@IDCobro", request.IdCobro));
            cmd.Parameters.Add(Entero("@IDBancoCheque", request.IdBancoCheque));
            cmd.Parameters.Add(Varchar("@NumeroCheque", LargoNumeroCheque, FiltrosLegacy.Texto(request.NumeroCheque, LargoNumeroCheque)));
            cmd.Parameters.Add(Varchar("@FechaCheque", LargoFecha, FiltrosLegacy.Fecha(request.FechaCheque)));
            cmd.Parameters.Add(Decimal18("@ImporteCheque", request.ImporteCheque));
            cmd.Parameters.Add(Varchar("@Observaciones", LargoTexto, FiltrosLegacy.Texto(request.Observaciones)));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipo, FiltrosLegacy.Texto(equipo, LargoEquipo)));
        }, ct);

        return Resultado(tablas, "IDResult");
    }

    public async Task<ResultadoLegacyDto> ActualizarChequeAsync(
        CobroDineroUsadoChequeActualizarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpActualizarCheque, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDCobroDineroUsadoCheque", request.IdCobroDineroUsadoCheque));
            cmd.Parameters.Add(Entero("@IDCobroDineroUsado", request.IdCobroDineroUsado));
            cmd.Parameters.Add(Entero("@IDCobro", request.IdCobro));
            cmd.Parameters.Add(Entero("@IDStatusCheque", request.IdStatusCheque));
            cmd.Parameters.Add(Entero("@IDBancoCheque", request.IdBancoCheque));
            cmd.Parameters.Add(Varchar("@NumeroCheque", LargoNumeroCheque, FiltrosLegacy.Texto(request.NumeroCheque, LargoNumeroCheque)));
            cmd.Parameters.Add(Varchar("@FechaCheque", LargoFecha, FiltrosLegacy.Fecha(request.FechaCheque)));
            cmd.Parameters.Add(Decimal18("@ImporteCheque", request.ImporteCheque));
            cmd.Parameters.Add(Varchar("@Observaciones", LargoTexto, FiltrosLegacy.Texto(request.Observaciones)));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipo, FiltrosLegacy.Texto(equipo, LargoEquipo)));
        }, ct);

        return Resultado(tablas, "IDResult");
    }

    public async Task<FilasLegacyDto> ConsultarChequesAsync(
        CobroDineroUsadoChequeConsultaRequest request, CancellationToken ct)
    {
        var filas = await EjecutarFilasAsync(SpConsultarCheques, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDCobroDineroUsadoCheque", request.IdCobroDineroUsadoCheque));
            cmd.Parameters.Add(Entero("@IDCobroDineroUsado", request.IdCobroDineroUsado));
            cmd.Parameters.Add(Entero("@IDCobro", request.IdCobro));
            cmd.Parameters.Add(Varchar("@FechaInicial", LargoFecha, FiltrosLegacy.FechaDesde(request.FechaInicial)));
            cmd.Parameters.Add(Varchar("@FechaFinal", LargoFecha, FiltrosLegacy.FechaHasta(request.FechaFinal)));
        }, ct);

        return new FilasLegacyDto { Rows = filas };
    }

    public async Task<ResultadoLegacyDto> CrearCorteCsAsync(
        CobroDineroUsadoCorteCsRequest request, int idUsuario, string equipo, CancellationToken ct)
        => await CorteCsAsync(SpCrearCorteCs, request, idUsuario, equipo, ct);

    public async Task<ResultadoLegacyDto> EliminarCorteCsAsync(
        CobroDineroUsadoCorteCsRequest request, int idUsuario, string equipo, CancellationToken ct)
        => await CorteCsAsync(SpEliminarCorteCs, request, idUsuario, equipo, ct);

    /*
      LOS DOS DE CORTES RECIBEN LOS MISMOS SEIS PARAMETROS, Y UNO DE ELLOS SOBRA.

      sp_n_EliminarCobroDineroUsadoCortesCS DECLARA tambien
      @IDCobroDineroUsadoCorteCS, pero legacy nunca se lo manda: borra por la
      combinacion cobro + centro + corte. Se respeta: mandarle el id del renglon
      cambiaria QUE renglon se borra, y eso no es una mejora, es otra operacion.
    */
    private async Task<ResultadoLegacyDto> CorteCsAsync(
        string procedimiento, CobroDineroUsadoCorteCsRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(procedimiento, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDCobroDineroUsado", request.IdCobroDineroUsado));
            cmd.Parameters.Add(Entero("@IDCobro", request.IdCobro));
            cmd.Parameters.Add(Entero("@IDCentro", request.IdCentro));
            cmd.Parameters.Add(Entero("@IDCorte", request.IdCorte));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipo, FiltrosLegacy.Texto(equipo, LargoEquipo)));
        }, ct);

        return Resultado(tablas, "IDResult");
    }

    public async Task<FilasLegacyDto> ConsultarCortesCsAsync(
        CobroDineroUsadoCorteCsConsultaRequest request, CancellationToken ct)
    {
        /*
          @IDsCobro SI SE MANDA, Y LEGACY NO LO MANDABA.

          El procedimiento lo declara (nvarchar(max)) y el repositorio de legacy
          se quedo en cinco parametros, asi que la pantalla no podia pedir los
          cortes de varios cobros de una vez aunque la base ya lo soportara.
          Mandarlo vacio da exactamente el comportamiento de legacy, asi que
          ofrecerlo no cambia nada de lo que hoy funciona.
        */
        var filas = await EjecutarFilasAsync(SpConsultarCortesCs, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDCobroDineroUsadoCorteCS", request.IdCobroDineroUsadoCorteCs));
            cmd.Parameters.Add(Entero("@IDCobroDineroUsado", request.IdCobroDineroUsado));
            cmd.Parameters.Add(Entero("@IDCobro", request.IdCobro));
            cmd.Parameters.Add(Varchar("@FechaInicial", LargoFecha, FiltrosLegacy.FechaDesde(request.FechaInicial)));
            cmd.Parameters.Add(Varchar("@FechaFinal", LargoFecha, FiltrosLegacy.FechaHasta(request.FechaFinal)));
            cmd.Parameters.Add(new Microsoft.Data.SqlClient.SqlParameter("@IDsCobro", System.Data.SqlDbType.NVarChar, -1)
            {
                Value = FiltrosLegacy.Ids(request.IdsCobro)
            });
        }, ct);

        return new FilasLegacyDto { Rows = filas };
    }

    /* ============================================================ */
    /* Ingresos de cascos usados                                     */
    /* ============================================================ */

    public async Task<ResultadoLegacyDto> CrearCobroUsadoAsync(
        VentasCobroUsadoCrearRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpCrearCobroUsado, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDCobro", request.IdCobro));
            cmd.Parameters.Add(Entero("@IDEmpresa", request.IdEmpresa));
            cmd.Parameters.Add(Varchar("@Observaciones", LargoTexto, FiltrosLegacy.Texto(request.Observaciones)));
            cmd.Parameters.Add(Varchar("@IDsTipoUsado", LargoLista, FiltrosLegacy.IdsUsados(request.IdsTipoUsado)));
            cmd.Parameters.Add(Varchar("@CantidadesUsado", LargoLista, FiltrosLegacy.IdsUsados(request.CantidadesUsado)));
            cmd.Parameters.Add(Varchar("@CostosConIvaUsado", LargoLista, FiltrosLegacy.ImportesUsados(request.CostosConIvaUsado)));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipo, FiltrosLegacy.Texto(equipo, LargoEquipo)));
        }, ct);

        return Resultado(tablas, "IDResult");
    }

    public async Task<ResultadoLegacyDto> CancelarCobroUsadoAsync(
        VentasCobroUsadoCancelarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpCancelarCobroUsado, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDVentaCobroUsado", request.IdVentaCobroUsado));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipo, FiltrosLegacy.Texto(equipo, LargoEquipo)));
        }, ct);

        return Resultado(tablas, "IDResult");
    }

    public async Task<FilasLegacyDto> ConsultarCobrosUsadoAsync(
        VentasCobroUsadoConsultaRequest request, CancellationToken ct)
    {
        var filas = await EjecutarFilasAsync(SpConsultarCobrosUsado, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDVentaCobroUsado", request.IdVentaCobroUsado));
            cmd.Parameters.Add(Entero("@IDCobro", request.IdCobro));
            cmd.Parameters.Add(Varchar("@FechaInicial", LargoFecha, FiltrosLegacy.FechaDesde(request.FechaInicial)));
            cmd.Parameters.Add(Varchar("@FechaFinal", LargoFecha, FiltrosLegacy.FechaHasta(request.FechaFinal)));
        }, ct);

        return new FilasLegacyDto { Rows = filas };
    }

    /* ============================================================ */
    /* Excedentes de cascos                                          */
    /* ============================================================ */

    public async Task<ResultadoLegacyDto> CrearExcedenteAsync(
        VentasUsadoExcedenteCrearRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        /*
          EL VALE SI SE LIMPIA DE APOSTROFOS, Y LEGACY NO LO LIMPIABA.

          Legacy limpia Observaciones y Equipo y se salta el Vale, que es
          justamente el campo con el que el cliente reclama el excedente. No
          hace diferencia en la base (todo va parametrizado), pero un vale con
          apostrofo no se puede teclear igual en Mac31 que en el web, y entonces
          el mismo vale deja de encontrarse desde una de las dos mitades.
        */
        var tablas = await EjecutarTablasAsync(SpCrearExcedente, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDCobro", request.IdCobro));
            cmd.Parameters.Add(Entero("@IDCliente", request.IdCliente));
            cmd.Parameters.Add(Entero("@IDRepartidor", request.IdRepartidor));
            cmd.Parameters.Add(Entero("@IDVenta", request.IdVenta));
            cmd.Parameters.Add(Entero("@IDPagoVenta", request.IdPagoVenta));
            cmd.Parameters.Add(Entero("@IDEmpresa", request.IdEmpresa));
            cmd.Parameters.Add(Varchar("@Vale", LargoVale, FiltrosLegacy.Texto(request.Vale, LargoVale)));
            cmd.Parameters.Add(Varchar("@Observaciones", LargoTexto, FiltrosLegacy.Texto(request.Observaciones)));
            cmd.Parameters.Add(Varchar("@IDsTipoUsado", LargoLista, FiltrosLegacy.IdsUsados(request.IdsTipoUsado)));
            cmd.Parameters.Add(Varchar("@CantidadesUsado", LargoLista, FiltrosLegacy.IdsUsados(request.CantidadesUsado)));
            cmd.Parameters.Add(Varchar("@CostosConIvaUsado", LargoLista, FiltrosLegacy.ImportesUsados(request.CostosConIvaUsado)));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipo, FiltrosLegacy.Texto(equipo, LargoEquipo)));
        }, ct);

        return Resultado(tablas, "IDResult");
    }

    public async Task<ResultadoLegacyDto> CancelarExcedenteAsync(
        VentasUsadoExcedenteCancelarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpCancelarExcedente, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDVentaUsadoExcendente", request.IdVentaUsadoExcedente));
            cmd.Parameters.Add(Entero("@IDPagoVenta", request.IdPagoVenta));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipo, FiltrosLegacy.Texto(equipo, LargoEquipo)));
        }, ct);

        return Resultado(tablas, "IDResult");
    }

    public async Task<FilasLegacyDto> ConsultarExcedentesAsync(
        VentasUsadoExcedenteConsultaRequest request, CancellationToken ct)
    {
        var filas = await EjecutarFilasAsync(SpConsultarExcedentes, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDVentaUsadoExcendente", request.IdVentaUsadoExcedente));
            cmd.Parameters.Add(Entero("@IDCobro", request.IdCobro));
            cmd.Parameters.Add(Entero("@IDCliente", request.IdCliente));
            cmd.Parameters.Add(Entero("@IDRepartidor", request.IdRepartidor));
            cmd.Parameters.Add(Varchar("@Vale", LargoVale, FiltrosLegacy.Texto(request.Vale, LargoVale)));
            cmd.Parameters.Add(Varchar("@FechaInicial", LargoFecha, FiltrosLegacy.FechaDesde(request.FechaInicial)));
            cmd.Parameters.Add(Varchar("@FechaFinal", LargoFecha, FiltrosLegacy.FechaHasta(request.FechaFinal)));
        }, ct);

        return new FilasLegacyDto { Rows = filas };
    }

    /* ============================================================ */
    /* Movimientos del almacen de usados                             */
    /* ============================================================ */

    public async Task<ResultadoLegacyDto> CrearMovimientoAsync(
        UsadoMovimientoCrearRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpCrearMovimiento, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDAlmacenUsado", request.IdAlmacenUsado));
            cmd.Parameters.Add(Entero("@IDGallero", request.IdGallero));
            cmd.Parameters.Add(Entero("@IDRepartidor", request.IdRepartidor));
            cmd.Parameters.Add(Varchar("@Motivo", LargoTexto, FiltrosLegacy.Texto(request.Motivo)));
            cmd.Parameters.Add(Varchar("@IDsTipoUsado", LargoLista, FiltrosLegacy.IdsUsados(request.IdsTipoUsado)));
            cmd.Parameters.Add(Varchar("@PreciosCambio", LargoLista, FiltrosLegacy.ImportesUsados(request.PreciosCambio)));
            cmd.Parameters.Add(Varchar("@PreciosUnitario", LargoLista, FiltrosLegacy.ImportesUsados(request.PreciosUnitario)));
            cmd.Parameters.Add(Varchar("@CantidadesSalida", LargoLista, FiltrosLegacy.IdsUsados(request.CantidadesSalida)));
            cmd.Parameters.Add(Varchar("@CantidadesEntrada", LargoLista, FiltrosLegacy.IdsUsados(request.CantidadesEntrada)));
            cmd.Parameters.Add(Decimal18("@TotalPagarEntrada", request.TotalPagarEntrada));
            cmd.Parameters.Add(Entero("@IDCobro", request.IdCobro));
            cmd.Parameters.Add(Entero("@IDVenta", request.IdVenta));
            cmd.Parameters.Add(Entero("@IDTipoCompra", request.IdTipoCompra));
            cmd.Parameters.Add(Entero("@IDTipoVenta", request.IdTipoVenta));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipo, FiltrosLegacy.Texto(equipo, LargoEquipo)));
        }, ct);

        return Resultado(tablas, "IDResult");
    }

    public async Task<ResultadoLegacyDto> CancelarMovimientoAsync(
        UsadoMovimientoCancelarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        var tablas = await EjecutarTablasAsync(SpCancelarMovimiento, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDUsadoMovimiento", request.IdUsadoMovimiento));
            cmd.Parameters.Add(Entero("@IDUsuario", idUsuario));
            cmd.Parameters.Add(Varchar("@Equipo", LargoEquipo, FiltrosLegacy.Texto(equipo, LargoEquipo)));
        }, ct);

        return Resultado(tablas, "IDResult");
    }

    public async Task<FilasLegacyDto> ConsultarMovimientosAsync(
        UsadoMovimientoConsultaRequest request, CancellationToken ct)
    {
        var filas = await EjecutarFilasAsync(SpConsultarMovimientos, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDUsadoMovimiento", request.IdUsadoMovimiento));
            cmd.Parameters.Add(Entero("@IDGallero", request.IdGallero));
            cmd.Parameters.Add(Entero("@IDRepartidor", request.IdRepartidor));
            cmd.Parameters.Add(Entero("@FolioInicial", request.FolioInicial));
            cmd.Parameters.Add(Entero("@FolioFinal", request.FolioFinal));
            cmd.Parameters.Add(Varchar("@FechaInicial", LargoFecha, FiltrosLegacy.FechaDesde(request.FechaInicial)));
            cmd.Parameters.Add(Varchar("@FechaFinal", LargoFecha, FiltrosLegacy.FechaHasta(request.FechaFinal)));
            cmd.Parameters.Add(Entero("@TipoConsulta", request.TipoConsulta));
        }, ct);

        return new FilasLegacyDto { Rows = filas };
    }

    public async Task<FilasLegacyDto> ConsultarMovimientosPagadosAsync(
        UsadoMovimientoPagadoConsultaRequest request, CancellationToken ct)
    {
        /* El orden de este es distinto al de ConsultarMovimientos: TipoConsulta
           va en tercer lugar y no al final. Son parametros nombrados, asi que no
           cambia nada, pero anotarlo evita que alguien "ordene" los dos igual. */
        var filas = await EjecutarFilasAsync(SpConsultarMovimientosPagados, cmd =>
        {
            cmd.Parameters.Add(Entero("@IDGallero", request.IdGallero));
            cmd.Parameters.Add(Entero("@IDRepartidor", request.IdRepartidor));
            cmd.Parameters.Add(Entero("@TipoConsulta", request.TipoConsulta));
            cmd.Parameters.Add(Varchar("@FechaInicial", LargoFecha, FiltrosLegacy.FechaDesde(request.FechaInicial)));
            cmd.Parameters.Add(Varchar("@FechaFinal", LargoFecha, FiltrosLegacy.FechaHasta(request.FechaFinal)));
            cmd.Parameters.Add(Entero("@IDCorte", request.IdCorte));
        }, ct);

        return new FilasLegacyDto { Rows = filas };
    }

    /* ---------------------------------------------------------------- */
    /* Lectura del renglon de resultado                                 */
    /* ---------------------------------------------------------------- */

    /// <summary>
    /// Arma el resultado a partir de lo que devolvio el procedimiento.
    ///
    /// Se leen `result` y `mensaje` solo SI existen. Varios de estos
    /// procedimientos devuelven las dos columnas y legacy no las miraba (el
    /// repositorio de cobros de cascos usados ni lee IDResult), asi que la
    /// pantalla no se enteraba de un -1 con su explicacion. Aqui bajan siempre.
    /// </summary>
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
