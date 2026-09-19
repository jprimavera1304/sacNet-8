using System.Globalization;
using ISL_Service.Application.DTOs.VentasMultiPago;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Repositories;

namespace ISL_Service.Application.Services;

/*
  ======================================================================
  MULTI PAGO — LAS REGLAS, TODAS, Y DE DONDE SALEN
  ======================================================================

  Origen: Mac31, Forms/Ventas/ConsultarVentas.cs btnMultiPago_Click (4017) y
  Forms/Ventas/VentasPago.cs (constructor de la linea 158, "Multipago Cobro").

  UNA SOLA IMPLEMENTACION PARA LAS DOS EMPRESAS
  ---------------------------------------------
  No hay un "if Tauro" y un "if Zaragoza" con dos caminos paralelos. Se
  bifurca por Constantes.Funcionalidad ("TAU" / "ZARA"), que es EXACTAMENTE lo
  que hace legacy (Variables.funcionalidad), y solo en los cuatro sitios donde
  legacy tambien se bifurca:

    1. Que columna es el SALDO y cual el IMPORTE del grid.
    2. Casco por kilo existe en Tauro y no en Zaragoza.
    3. La liquidacion (agente / camioneta) existe en Tauro y no en Zaragoza.
    4. Zaragoza pide hoja de cobro, limita efectivo/tarjeta al dinero de esa
       hoja, pregunta credito/debito en la tarjeta y pide la clave del dia.

  Todo lo demas —la tolerancia del documento, el catalogo de bancos, el precio
  del casco por kilo— NO se bifurca: se lee de la base, que ya trae el valor de
  cada empresa. Escribirlo a mano seria acertarle a una y fallarle a la otra.
  ======================================================================
*/
public class VentasMultiPagoService : IVentasMultiPagoService
{
    /* enumTipoPagos de Mac31 (Utils/Enums.cs, linea 134). */
    private const int PagoEfectivo = 1;
    private const int PagoCheque = 2;
    private const int PagoDescuento = 3;
    private const int PagoTransferencia = 5;
    private const int PagoCargoAdicional = 7;
    private const int PagoTarjeta = 9;
    private const int PagoDepositoEfectivo = 10;
    private const int PagoExcedenteUsados = 11;
    private const int PagoSaldoDineroFavor = 13;
    private const int PagoNotaCredito = 16;
    private const int PagoCascoKilo = 17;

    /*
      Zaragoza manda 911 en vez de 11 cuando el pago es por excedente de cascos
      a un precio, "para evitar chocar con otra funcionalidad existente"
      (VentasPago.cs 3655-3661; el propio procedimiento lo traduce de vuelta a
      11 en su linea 143).
    */
    private const int PagoExcedenteUsadosZara = 911;

    /// <summary>El nombre de la forma con el que legacy firma la bitacora de claves.</summary>
    private const string FormaLegacy = "VentasPago";

    private readonly IVentasMultiPagoRepository _repo;

    public VentasMultiPagoService(IVentasMultiPagoRepository repo)
    {
        _repo = repo;
    }

    /* ================================================================ */
    /* LA PANTALLA                                                      */
    /* ================================================================ */

    public async Task<VentasMultiPagoPantallaResponse> PantallaAsync(
        VentasMultiPagoPantallaRequest request, CancellationToken ct)
    {
        var ids = (request?.IdsVenta ?? new List<int>()).Where(x => x > 0).Distinct().ToList();

        var cons = await _repo.ConstantesAsync(ct);
        var esTauro = EsTauro(cons);

        var resp = new VentasMultiPagoPantallaResponse
        {
            Funcionalidad = cons.Funcionalidad,
            EsCentroServicio = cons.EsCentroServicio,
            EtiquetaImporte = esTauro ? "Total a pagar" : "Importe",
            PrecioCascoKilo = cons.PrecioCascoKilo,
            ToleranciaDocumento = cons.DiferenciaPagoVsTransCheq,
            PideLiquidacion = esTauro,
            FechaPagos = FechaPagos(cons)
        };

        if (ids.Count == 0)
        {
            resp.Impedimento = "Selecciona al menos una remisión en Ventas.";
            return resp;
        }

        var crudas = await _repo.RemisionesAsync(ids, ct);
        if (crudas.Count == 0)
        {
            resp.Impedimento = "No se encontraron las remisiones seleccionadas.";
            return resp;
        }

        /*
          LOS SALDOS A FAVOR SALEN DEL PRIMER RENGLON, NO DE LA SUMA.

          Asi lo hace Mac31 (VentasPago.cs 1184-1185): son saldos DEL CLIENTE,
          no de la remision, y sp_n_ConsultaVentas los repite igual en cada
          renglon. Sumarlos multiplicaria el saldo a favor por el numero de
          remisiones — un cliente con 300 de saldo y cuatro remisiones podria
          "pagar" 1,200 que no tiene.
        */
        resp.SaldoFavorDineroCliente = crudas[0].SaldoFavorCliente;
        resp.SaldoFavorCascosCliente = crudas[0].SaldoFavorCascosCliente;

        /*
          QUE REMISIONES NO ADMITE MULTI PAGO

          Las tres son de ConsultarVentas.btnMultiPago_Click (4040-4064), y las
          tres ABORTAN la operacion completa en Mac31, no solo saltan el
          renglon. Aqui se listan todas las que estorban en vez de parar en la
          primera: quien marco ocho remisiones prefiere ver de una vez cuales
          son las dos que sobran a descubrirlas de una en una.

          "CV-" es el folio de las notas de credito de Zaragoza. Mac31 se sale
          SIN decir nada (linea 4040: return sin mensaje), que es el peor aviso
          posible; aqui se explica.
        */
        foreach (var c in crudas)
        {
            string? motivo = null;

            if (c.FolioFtm.Contains("CV-", StringComparison.OrdinalIgnoreCase))
                motivo = "Es una nota de crédito (CV-): no se cobra desde Multi pago.";
            else if (string.Equals(c.Cancelada, "SI", StringComparison.OrdinalIgnoreCase))
                motivo = "La remisión está cancelada.";
            else if (string.Equals(c.Pagada, "SI", StringComparison.OrdinalIgnoreCase))
                motivo = "La remisión ya está pagada.";

            if (motivo is null)
            {
                resp.Remisiones.Add(new VentasMultiPagoRemision
                {
                    IdVenta = c.IdVenta,
                    IdCliente = c.IdCliente,
                    Empresa = c.Empresa,
                    FolioFtm = c.FolioFtm,
                    Fecha = c.Fecha,
                    NombreCliente = c.NombreCliente,
                    /*
                      TAURO PINTA "TotalPagar" Y ZARAGOZA "ImporteZ", Y NO SON
                      EL MISMO NUMERO: TotalPagar incluye los cascos y ImporteZ
                      no (SetColumnasRemisiones, VentasPago.cs 1310-1328).
                    */
                    Importe = esTauro ? c.TotalPagar : c.ImporteZ,
                    Cargos = c.Cargos,
                    Descuentos = c.Descuentos,
                    Abonos = c.Abonos,
                    /*
                      Y el SALDO tampoco: Tauro usa la columna `Saldo` (dinero +
                      cascos) y Zaragoza `SaldoImporte` (solo dinero). Es
                      exactamente el reparto de VentasPago.cs 1256-1259 y
                      1370-1388. Elegir mal la columna descuadra el total de la
                      pantalla contra Mac31.
                    */
                    Saldo = esTauro ? c.Saldo : c.SaldoImporte,
                    SaldoFavor = c.SaldoFavor,
                    SaldoCascos = c.SaldoCascos,
                    FolioCobro = c.FolioCobro
                });
            }
            else
            {
                resp.Rechazadas.Add(new VentasMultiPagoRechazo
                {
                    IdVenta = c.IdVenta,
                    FolioFtm = c.FolioFtm,
                    Motivo = motivo
                });
            }
        }

        /*
          LAS QUE NI SIQUIERA VOLVIERON DE LA BASE.

          sp_n_ConsultaVentas, cuando se le busca por @IDsVenta, arrastra un
          filtro que NO se ve en el `WHERE` que arma para la lista de ids:
          "AND C.IDUsuarioImpresion IS NOT NULL" (sp_n_ConsultaVentas, linea 540
          en Produccion_svr y 409 en MacZ). Ese pedazo se mete en @SqlParte2, y
          la busqueda por @IDsVenta solo reescribe @SqlWhere — asi que sigue
          ahi. Resultado: una remision sin imprimir NO vuelve, y vuelve sin
          error.

          Se comprobo en MacZ: 140962 y 140963 tienen IDUsuarioImpresion NULL y
          el procedimiento devuelve cero filas por @IDsVenta, mientras que
          140964 (impresa) si vuelve. La clausula es IDENTICA en las dos bases,
          asi que no es una diferencia entre empresas: es el comportamiento del
          procedimiento.

          En la practica no deberia verse, porque la pantalla de Ventas se
          alimenta del MISMO procedimiento y por tanto solo enseña impresas. Se
          avisa igual: una remision que desaparece de la lista sin decir nada es
          justo la clase de fallo que se descubre cuadrando la caja.
        */
        foreach (var id in ids)
        {
            if (crudas.Any(c => c.IdVenta == id)) continue;
            resp.Rechazadas.Add(new VentasMultiPagoRechazo
            {
                IdVenta = id,
                FolioFtm = $"#{id}",
                Motivo = "No se pudo leer: la remisión no está impresa o ya no existe."
            });
        }

        resp.SaldoTotal = resp.Remisiones.Sum(x => x.Saldo);
        resp.ImporteTotal = resp.Remisiones.Sum(x => x.Importe);
        resp.CargosTotal = resp.Remisiones.Sum(x => x.Cargos);
        resp.DescuentosTotal = resp.Remisiones.Sum(x => x.Descuentos);
        resp.AbonosTotal = resp.Remisiones.Sum(x => x.Abonos);

        if (resp.Remisiones.Count == 0)
            resp.Impedimento = "Ninguna de las remisiones seleccionadas admite Multi pago.";
        else if (resp.Rechazadas.Count > 0)
            /*
              Mac31 aborta si UNA sola estorba. Se replica: si se dejara cobrar
              las demas, el total de la pantalla ya no seria el mismo que
              Mac31 y el usuario no tendria forma de notarlo.
            */
            resp.Impedimento = "Quita de la selección las remisiones marcadas abajo y vuelve a entrar.";

        var bancos = await _repo.BancosAsync(ct);
        resp.Bancos = bancos.Select(b => new VentasMultiPagoCatalogoItem { Id = b.Id, Nombre = b.Nombre }).ToList();

        if (esTauro)
        {
            var reps = await _repo.RepartidoresAsync(ct);
            resp.Repartidores = reps.Select(b => new VentasMultiPagoCatalogoItem { Id = b.Id, Nombre = b.Nombre }).ToList();
        }

        resp.FormasDePago = FormasDePago(cons, resp);
        return resp;
    }

    /// <summary>
    /// El rotulo "FECHA PAGOS". Tauro trabaja por DIA DE OPERACION y pinta el
    /// de Constantes (VentasPago.cs 195). Zaragoza, cuando no es centro de
    /// servicio y no hay hoja de cobro, pinta la fecha de HOY (linea 226) — que
    /// es justo el caso de Multi pago desde Ventas.
    /// </summary>
    private static string FechaPagos(MultiPagoConstantes cons)
    {
        if (!EsTauro(cons) && cons.EsCentroServicio == 0)
            return DateTime.Now.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

        return cons.FechaOperacionPagosFtm;
    }

    private static bool EsTauro(MultiPagoConstantes cons) =>
        cons.Funcionalidad.Contains("TAU", StringComparison.OrdinalIgnoreCase);

    /* ================================================================ */
    /* QUE FORMAS DE PAGO SE OFRECEN, Y QUE PIDE CADA UNA               */
    /* ================================================================ */

    /*
      Esto es el rbEfectivo_Click de Mac31 (VentasPago.cs 1783): el metodo que
      al elegir una opcion tapa TODOS los campos y destapa solo los suyos.
      Aqui se describe en datos en vez de en codigo de pantalla, para que el
      front no tenga que repetir la regla.

      QUE SE VE Y QUE NO, EN MULTI PAGO DESDE VENTAS (VentasPago.cs 519-629):

        se ocultan  Descuento, Nota de credito, Excedente de usados y Cargo
                    ...salvo que haya UNA SOLA remision, que los devuelve.
        se ocultan  el ajuste de abono (solo existe con NuevoEsquemaPagos = 1,
                    que por esta puerta siempre es 0)
                    y la casilla de diferencia de usados (solo con una remision
                    y SaldoCascos > 0, y por esta puerta SaldoCascos siempre
                    llega en 0 — ConsultarVentas.cs 4024).
        aparece     Saldo a favor, solo si el cliente tiene saldo a favor.
        Tauro       Casco por kilo.
    */
    private static List<VentasMultiPagoFormaDePago> FormasDePago(
        MultiPagoConstantes cons, VentasMultiPagoPantallaResponse p)
    {
        var esTauro = EsTauro(cons);
        var una = p.Remisiones.Count == 1;

        /*
          ZARAGOZA SIN HOJA DE COBRO NO PUEDE COBRAR. NO ES UN OLVIDO.

          btnGuardar empieza con esto (VentasPago.cs 2405-2432):

            ZARA && EsCentroServicio == 0 && IDCobro == 0 && saldoFavorDinero <= 0
              -> "SE REQUIERE UNA HOJA DE COBRO PARA APLICAR PAGOS/DESCTOS."

          Y desde Ventas el IDCobro SIEMPRE vale 0: ConsultarVentas.cs lo
          declara en la linea 4025 y no lo asigna nunca. O sea que en Zaragoza
          Multi pago solo sirve para aplicar el saldo a favor del cliente.

          Se dice ARRIBA y en cada forma, en vez de dejar que la persona
          capture el reparto completo y se lo rechacen al guardar.
        */
        var zaraSinCobro = !esTauro && cons.EsCentroServicio == 0 && p.SaldoFavorDineroCliente <= 0;
        var motivoZara = "Zaragoza necesita una hoja de cobro para aplicar pagos; desde Ventas no la hay.";

        if (zaraSinCobro && string.IsNullOrEmpty(p.Impedimento))
            p.Impedimento = motivoZara;

        /*
          Y ADEMAS, EN ZARAGOZA, EFECTIVO Y TARJETA VAN CONTRA EL DINERO DE LA
          HOJA DE COBRO.

          VentasPago.cs 3296-3319 compara el abono contra saldoEfectivo y
          saldoTarjeta, que se llenan en ConsultaCobrosDineroUsados — y ese
          metodo se sale en su primera linea si FolioCobro == 0 (linea 854).
          Sin hoja de cobro los dos valen 0, asi que cualquier importe rebota
          con "EL MONTO MAXIMO PARA PAGAR EN EFECTIVO DISPONIBLE ES DE $ 0.00".
        */
        var motivoZaraEfectivo = !esTauro && cons.EsCentroServicio == 0
            ? "En Zaragoza el efectivo y la tarjeta salen del dinero de la hoja de cobro, y aquí no hay hoja."
            : string.Empty;

        /* La clave del dia: ZARA, esquema viejo, para estas formas (VentasPago.cs 3604-3640). */
        bool ClaveZara(int idTipoPago) =>
            !esTauro && (idTipoPago == PagoDescuento || idTipoPago == PagoNotaCredito
                      || idTipoPago == PagoTransferencia || idTipoPago == PagoDepositoEfectivo
                      || idTipoPago == PagoEfectivo);

        var lista = new List<VentasMultiPagoFormaDePago>
        {
            new()
            {
                IdTipoPago = PagoEfectivo, Clave = "efectivo", Nombre = "Efectivo",
                Impedimento = zaraSinCobro ? motivoZara : motivoZaraEfectivo,
                PideAutorizacion = ClaveZara(PagoEfectivo)
            },
            new()
            {
                IdTipoPago = PagoCheque, Clave = "cheque", Nombre = "Cheque",
                PideBanco = true, PideNumero = true, PideMontoTotal = true,
                EtiquetaNumero = "N° de cheque",
                Impedimento = zaraSinCobro ? motivoZara : string.Empty
            },
            new()
            {
                IdTipoPago = PagoTransferencia, Clave = "transferencia", Nombre = "Transferencia",
                PideBanco = true, PideNumero = true, PideMontoTotal = true,
                EtiquetaNumero = "N° de transferencia",
                Impedimento = zaraSinCobro ? motivoZara : string.Empty,
                PideAutorizacion = ClaveZara(PagoTransferencia)
            },
            new()
            {
                IdTipoPago = PagoTarjeta, Clave = "tarjeta", Nombre = "Tarjeta",
                PideBanco = true, PideNumero = true, PideMontoTotal = true,
                /*
                  Credito o debito SOLO lo pregunta Zaragoza: los dos botones
                  viven dentro de pnlTarjeta, que solo se muestra con ZARA
                  (VentasPago.cs 2003-2011 y el Designer, linea 1939). En Tauro
                  legacy marca credito sin preguntar, y eso se replica al
                  guardar.
                */
                PideTipoTarjeta = !esTauro,
                EtiquetaNumero = "N° de tarjeta",
                Impedimento = zaraSinCobro ? motivoZara : motivoZaraEfectivo
            },
            new()
            {
                IdTipoPago = PagoDepositoEfectivo, Clave = "deposito", Nombre = "Depósito en efectivo",
                PideBanco = true, PideNumero = true, PideMontoTotal = true,
                EtiquetaNumero = "N° / clave del depósito",
                /* Mac31 preselecciona el banco de la empresa (VentasPago.cs 300-307). */
                BancoPorOmision = cons.IdBancoEmpresa,
                Impedimento = zaraSinCobro ? motivoZara : string.Empty,
                PideAutorizacion = ClaveZara(PagoDepositoEfectivo)
            }
        };

        /*
          El saldo a favor solo aparece si lo hay: rbSaldoFavorActualClienteDinero
          .Visible = saldoFavorDineroCliente > 0 (VentasPago.cs 575). Es tambien
          la unica forma que Zaragoza deja usar sin hoja de cobro, porque es
          justo la excepcion que abre la validacion del guardado.
        */
        if (p.SaldoFavorDineroCliente > 0)
        {
            lista.Add(new VentasMultiPagoFormaDePago
            {
                IdTipoPago = PagoSaldoDineroFavor,
                Clave = "saldo_favor",
                Nombre = $"Saldo a favor ({p.SaldoFavorDineroCliente:N2})"
            });
        }

        /*
          Casco por kilo es de Tauro: rbCascoKilo.Visible = true con TAU y
          false con ZARA (VentasPago.cs 205 y 221). El precio NO se teclea: sale
          de Constantes.PrecioCascoKilo y el total es cantidad x precio
          (CalculaTotalCascos, VentasPago.cs 4243).
        */
        if (esTauro)
        {
            lista.Add(new VentasMultiPagoFormaDePago
            {
                IdTipoPago = PagoCascoKilo,
                Clave = "casco_kilo",
                Nombre = "Casco por kilo",
                PideKilos = true,
                PideMotivo = true
            });
        }

        /*
          Con UNA SOLA remision vuelven las cuatro que Multi pago esconde
          (VentasPago.cs 598-604). Con dos o mas no: descuentos y cargos son
          por remision y legacy no tiene donde preguntar a cual.
        */
        if (una)
        {
            lista.Add(new VentasMultiPagoFormaDePago
            {
                IdTipoPago = PagoDescuento, Clave = "descuento", Nombre = "Descuento",
                PideMotivo = true,
                Impedimento = zaraSinCobro ? motivoZara : string.Empty,
                PideAutorizacion = ClaveZara(PagoDescuento)
            });
            lista.Add(new VentasMultiPagoFormaDePago
            {
                IdTipoPago = PagoNotaCredito, Clave = "nota_credito", Nombre = "Nota de crédito",
                PideMotivo = true,
                Impedimento = zaraSinCobro ? motivoZara : string.Empty,
                PideAutorizacion = ClaveZara(PagoNotaCredito)
            });
            lista.Add(new VentasMultiPagoFormaDePago
            {
                IdTipoPago = PagoCargoAdicional, Clave = "cargo", Nombre = "Cargo adicional",
                PideMotivo = true, PideCargo = true,
                Impedimento = zaraSinCobro ? motivoZara : string.Empty
            });
            lista.Add(new VentasMultiPagoFormaDePago
            {
                IdTipoPago = PagoExcedenteUsados, Clave = "excedente_usados", Nombre = "Excedente de usados",
                PideMotivo = true,
                /*
                  Se ofrece porque en Mac31 esta ahi, pero todavia no se puede
                  capturar: pide el tablero de cascos por tipo (G1..G7 y moto)
                  con cantidad y costo por renglon, sus topes contra el saldo de
                  cascos del cliente y la autorizacion cuando se cambia un
                  precio (VentasPago.cs 2440-2560). Eso es otra pantalla, no un
                  campo mas. El boton se pulsa y contesta; no se apaga.
                */
                Impedimento = "El excedente de usados se sigue capturando en Mac31: pide el tablero de cascos por tipo."
            });
        }

        return lista;
    }

    /* ================================================================ */
    /* GUARDAR                                                          */
    /* ================================================================ */

    public async Task<VentasMultiPagoGuardarResponse> GuardarAsync(
        VentasMultiPagoGuardarRequest request, int idUsuarioLegacy, string equipo, CancellationToken ct)
    {
        var mal = new Func<string, VentasMultiPagoGuardarResponse>(m =>
            new VentasMultiPagoGuardarResponse { Guardo = false, Mensaje = m });

        var ids = (request?.IdsVenta ?? new List<int>()).Where(x => x > 0).Distinct().ToList();
        if (request is null || ids.Count == 0)
            return mal("Selecciona una remisión.");

        var cons = await _repo.ConstantesAsync(ct);
        var esTauro = EsTauro(cons);

        /*
          SE VUELVE A LEER TODO DE LA BASE ANTES DE ESCRIBIR.

          Nada de lo que decide si el pago procede —saldos, si la remision
          sigue viva, el saldo a favor— se toma del cuerpo de la peticion. Entre
          que se pinto la pantalla y que se pulso Guardar pudo cancelarse la
          remision o cobrarla otro cajero. Mac31 vive en una sola maquina y
          puede confiar en lo que tiene en memoria; una API no.
        */
        var crudas = await _repo.RemisionesAsync(ids, ct);
        if (crudas.Count == 0)
            return mal("No se encontraron las remisiones.");

        /* Si alguna no volvio de la base, se para: cobrar "casi todo" lo
           seleccionado descuadra la caja sin que nadie se entere. El porque de
           que pueda faltar esta en PantallaAsync. */
        var faltante = ids.FirstOrDefault(id => crudas.All(c => c.IdVenta != id));
        if (faltante != 0)
            return mal("Una de las remisiones seleccionadas ya no se puede leer. Vuelve a consultar en Ventas.");

        foreach (var c in crudas)
        {
            if (c.FolioFtm.Contains("CV-", StringComparison.OrdinalIgnoreCase))
                return mal($"La remisión {c.FolioFtm} es una nota de crédito y no se cobra desde Multi pago.");
            if (string.Equals(c.Cancelada, "SI", StringComparison.OrdinalIgnoreCase))
                return mal($"La remisión {c.FolioFtm} está cancelada.");
            if (string.Equals(c.Pagada, "SI", StringComparison.OrdinalIgnoreCase))
                return mal($"La remisión {c.FolioFtm} ya está pagada.");
        }

        var saldoFavorDinero = crudas[0].SaldoFavorCliente;
        var saldoActual = crudas.Sum(c => esTauro ? c.Saldo : c.SaldoImporte);
        var idCliente = crudas[0].IdCliente;
        var idTipoPago = request.IdTipoPago;

        /* ---- 1. Zaragoza: la hoja de cobro (VentasPago.cs 2405-2432) ---- */
        /*
          El IDCobro va en 0 porque desde Ventas siempre es 0
          (ConsultarVentas.cs 4025). No se inventa uno: si un dia Multi pago se
          abriera desde la hoja de cobro, ahi habria un folio de verdad.
        */
        const int idCobro = 0;

        if (!esTauro && cons.EsCentroServicio == 0 && idCobro == 0 && saldoFavorDinero <= 0)
            return mal("Se requiere una hoja de cobro para aplicar pagos o descuentos.");

        /* ---- 2. Tauro: la liquidacion (VentasPago.cs 2676-2707) ---- */
        var idAgente = 0;
        var idRepartidor = 0;
        if (esTauro)
        {
            var camioneta = string.Equals(request.Liquidacion, "camioneta", StringComparison.OrdinalIgnoreCase);
            if (camioneta)
            {
                if (request.IdRepartidorLiquidacion <= 0) return mal("Selecciona el repartidor.");
                idRepartidor = request.IdRepartidorLiquidacion;
            }
            else
            {
                if (request.IdAgenteLiquidacion <= 0) return mal("Selecciona el agente.");
                idAgente = request.IdAgenteLiquidacion;
            }
        }

        /* ---- 3. Lo que pide cada forma de pago ---- */
        var abono = Redondear(request.Abono);
        var montoTotal = Redondear(request.MontoTotal);
        var cargo = Redondear(request.Cargo);
        var motivo = (request.Motivo ?? string.Empty).Trim();
        var tolerancia = cons.DiferenciaPagoVsTransCheq;

        var idBancoCheque = 0;
        var idBancoTransfer = 0;
        var idBancoTarjeta = 0;
        var idBancoDeposito = 0;
        var numeroCheque = string.Empty;
        var transferencia = string.Empty;
        var tarjeta = string.Empty;
        var depositoNumero = string.Empty;
        var tipoTarjeta = 0;
        var cargoMotivo = string.Empty;
        var cascoKiloCantidad = 0m;
        var cascoKiloTotal = 0m;
        var cascoKiloPrecio = 0m;
        var cascoKiloMotivo = string.Empty;

        switch (idTipoPago)
        {
            case PagoEfectivo:
                break;

            case PagoCheque:
                if (request.IdBancoCheque <= 0) return mal("Selecciona el banco del cheque.");
                numeroCheque = (request.NumeroCheque ?? string.Empty).Trim();
                if (numeroCheque.Length == 0) return mal("Ingresa el número de cheque.");
                idBancoCheque = request.IdBancoCheque;
                /*
                  Zaragoza deja el importe del documento en blanco y toma el
                  abono (VentasPago.cs 2793-2800). Tauro no: ahi el cheque tiene
                  su propio monto y puede ser mayor que lo que se abona.
                */
                if (!esTauro && montoTotal == 0) montoTotal = abono;
                if (montoTotal == 0) return mal("Ingresa el monto total del cheque.");
                if (abono > montoTotal + tolerancia)
                    return mal($"El abono no puede ser mayor a $ {(montoTotal + tolerancia):N2}.");
                break;

            case PagoTransferencia:
                if (request.IdBancoTransfer <= 0) return mal("Selecciona el banco de la transferencia.");
                transferencia = (request.Transferencia ?? string.Empty).Trim();
                if (transferencia.Length == 0) return mal("Ingresa el número de transferencia.");
                idBancoTransfer = request.IdBancoTransfer;
                if (!esTauro && montoTotal == 0) montoTotal = abono;
                if (montoTotal == 0) return mal("Ingresa el monto total de la transferencia.");
                if (abono > montoTotal + tolerancia)
                    return mal($"El abono no puede ser mayor a $ {(montoTotal + tolerancia):N2}.");
                break;

            case PagoTarjeta:
                if (request.IdBancoTarjeta <= 0) return mal("Selecciona el banco de la tarjeta.");
                tarjeta = (request.NumeroTarjeta ?? string.Empty).Trim();
                if (tarjeta.Length == 0) return mal("Ingresa el número de tarjeta.");
                idBancoTarjeta = request.IdBancoTarjeta;
                /*
                  La tarjeta SI toma el abono en las dos empresas si no se
                  captura el monto (VentasPago.cs 2916-2924), a diferencia del
                  cheque. Es una asimetria de legacy, no un descuido de aqui.
                */
                if (montoTotal == 0) montoTotal = abono;
                if (abono == 0) abono = montoTotal;
                if (montoTotal == 0) return mal("Ingresa el monto total de la tarjeta.");
                if (abono > montoTotal + tolerancia)
                    return mal($"El abono no puede ser mayor a $ {(montoTotal + tolerancia):N2}.");
                /*
                  En Tauro no se pregunta: legacy marca credito por su cuenta
                  (rbTarjetaCredito.Checked = true, VentasPago.cs 2006) aunque
                  el panel este oculto, y eso acaba en TipoTarjeta = 1.
                */
                tipoTarjeta = esTauro ? 1 : (request.TipoTarjeta == 2 ? 2 : 1);
                break;

            case PagoDepositoEfectivo:
                if (request.IdBancoDepositoEfe <= 0) return mal("Selecciona el banco del depósito.");
                depositoNumero = (request.DepositoEfectivoNumero ?? string.Empty).Trim();
                if (depositoNumero.Length == 0) return mal("Ingresa el número o clave del depósito.");
                idBancoDeposito = request.IdBancoDepositoEfe;
                if (!esTauro && montoTotal == 0) { montoTotal = abono; if (abono == 0) abono = montoTotal; }
                if (montoTotal == 0) return mal("Ingresa el monto total del depósito.");
                if (abono > montoTotal + tolerancia)
                    return mal($"El abono no puede ser mayor a $ {(montoTotal + tolerancia):N2}.");
                break;

            case PagoDescuento:
                if (crudas.Count != 1) return mal("El descuento solo se aplica a una remisión a la vez.");
                if (motivo.Length == 0) return mal("Ingresa el motivo del descuento.");
                cargoMotivo = motivo;
                /*
                  Mac31 manda el motivo en CargoMotivo, no en Motivo
                  (VentasPago.cs 3718). Es como se llama el parametro del
                  procedimiento; cambiarlo dejaria el descuento sin razon
                  escrita.
                */
                if (abono > saldoActual)
                    return mal($"El descuento no puede ser mayor a $ {saldoActual:N2}.");
                break;

            case PagoNotaCredito:
                if (crudas.Count != 1) return mal("La nota de crédito solo se aplica a una remisión a la vez.");
                if (motivo.Length == 0) return mal("Ingresa el motivo de la nota de crédito.");
                cargoMotivo = motivo;
                if (abono > saldoActual)
                    return mal($"El monto de la nota de crédito no puede ser mayor a $ {saldoActual:N2}.");
                break;

            case PagoCargoAdicional:
                if (crudas.Count != 1) return mal("El cargo adicional solo se aplica a una remisión a la vez.");
                if (motivo.Length == 0) return mal("Ingresa el motivo del cargo.");
                if (cargo <= 0) return mal("Ingresa el monto del cargo.");
                cargoMotivo = motivo;
                if (!request.ConfirmaCargo)
                    return Confirmar($"¿Aplicar un cargo adicional de $ {cargo:N2}?", "cargo");
                break;

            case PagoSaldoDineroFavor:
                /* VentasPago.cs 3191-3200 y 3238-3249: dos topes, no uno. */
                if (abono <= 0) return mal("Ingresa el monto del abono.");
                if (abono > saldoFavorDinero)
                    return mal($"El abono no puede ser mayor al saldo a favor de dinero: $ {saldoFavorDinero:N2}.");
                if (abono > saldoActual)
                    return mal($"El abono no puede ser mayor al saldo de la remisión: $ {saldoActual:N2}.");
                if (!request.ConfirmaSaldoAFavor)
                    return Confirmar($"¿Aplicar $ {abono:N2} del saldo a favor del cliente?", "saldo_favor");
                break;

            case PagoCascoKilo:
                if (!esTauro) return mal("El casco por kilo solo existe en Tauro.");
                cascoKiloCantidad = Redondear(request.CascoKiloCantidad);
                if (cascoKiloCantidad <= 0) return mal("Ingresa el número de kilos.");
                if (motivo.Length == 0) return mal("Ingresa el motivo.");
                cascoKiloMotivo = motivo;
                /*
                  EL PRECIO LO PONE EL SERVIDOR.

                  Mac31 lo pinta en una etiqueta que no se puede escribir
                  (lblCascoKiloPrecio2 = Variables.PrecioCascoKilo,
                  VentasPago.cs 201) y el total es cantidad x precio
                  (CalculaTotalCascos, linea 4243). Si el precio viajara en la
                  peticion, cualquiera podria liquidar sus cascos al precio que
                  se le antojara.
                */
                cascoKiloPrecio = cons.PrecioCascoKilo;
                cascoKiloTotal = Redondear(cascoKiloCantidad * cascoKiloPrecio);
                /* VentasPago.cs 3822: el abono del casco por kilo ES el total. */
                abono = cascoKiloTotal;
                break;

            case PagoExcedenteUsados:
            case PagoExcedenteUsadosZara:
                return mal("El excedente de usados todavía se captura en Mac31.");

            default:
                return mal("Selecciona la forma de pago.");
        }

        if (abono < 0) return mal("El abono no puede ser negativo.");

        /* ---- 4. Zaragoza: efectivo y tarjeta contra la hoja de cobro ---- */
        /*
          VentasPago.cs 3296-3319. Sin hoja de cobro, saldoEfectivo y
          saldoTarjeta valen 0 porque ConsultaCobrosDineroUsados se sale en su
          primera linea (linea 854). Se deja explicito el 0 en vez de omitir la
          regla: el dia que Multi pago se abra desde una hoja de cobro, aqui es
          donde hay que leer EfectivoSaldo y TarjetaSaldo.
        */
        if (!esTauro && cons.EsCentroServicio == 0)
        {
            const decimal saldoEfectivoDeLaHoja = 0m;
            const decimal saldoTarjetaDeLaHoja = 0m;

            if (idTipoPago == PagoEfectivo && abono > saldoEfectivoDeLaHoja)
                return mal($"El monto máximo para pagar en efectivo disponible es de $ {saldoEfectivoDeLaHoja:N2}.");

            if (idTipoPago == PagoTarjeta && abono > saldoTarjetaDeLaHoja)
                return mal($"El monto máximo para pagar en tarjeta disponible es de $ {saldoTarjetaDeLaHoja:N2}.");
        }

        /* ---- 5. El reparto por remision (VentasPago.cs 2624-2670 y 3538-3600) ---- */
        var porVenta = new Dictionary<int, decimal>();
        foreach (var r in request.Pagos ?? new List<VentasMultiPagoRenglonPago>())
        {
            if (r.IdVenta <= 0) continue;
            var m = Redondear(r.Monto);
            if (m <= 0) continue;
            if (crudas.All(c => c.IdVenta != r.IdVenta))
                return mal("Se intentó abonar a una remisión que no está en la selección.");
            porVenta[r.IdVenta] = porVenta.TryGetValue(r.IdVenta, out var y) ? y + m : m;
        }

        if (porVenta.Count == 0)
        {
            /*
              Con VARIAS remisiones hay que decir cuanto lleva cada una: legacy
              no reparte solo (VentasPago.cs 3566-3577). Con UNA sola, el abono
              completo se va a esa remision (linea 3581).
            */
            if (crudas.Count > 1)
                return mal("Ingresa el monto del abono para cada remisión.");

            porVenta[crudas[0].IdVenta] = abono;
        }

        var totalPagado = Redondear(porVenta.Values.Sum());

        /*
          EL REPARTO NO PUEDE PASARSE DEL DINERO QUE ENTRA.

          VentasPago.cs 2645-2655. Si el abono venia en cero, legacy lo toma del
          importe del documento —transferencia, cheque, deposito o tarjeta, en
          ese orden (lineas 2631-2642)— porque con esas formas el monto se
          captura ahi y no en el campo del abono.
        */
        if (abono == 0) abono = montoTotal;

        if (totalPagado > abono)
            return mal($"El reparto suma $ {totalPagado:N2} y el pago es de $ {abono:N2}. " +
                       $"Sobran $ {(totalPagado - abono):N2}.");

        /* ---- 6. Pagar de mas deja saldo a favor: se pregunta (VentasPago.cs 3325-3358) ---- */
        if (cons.EsCentroServicio == 0 && idTipoPago != PagoCargoAdicional && abono > saldoActual)
        {
            if (!request.ConfirmaPagoMayorAlSaldo)
                return Confirmar(
                    $"¿Cobrar más que el saldo? Los $ {(abono - saldoActual):N2} de diferencia quedan " +
                    "como saldo a favor del cliente.", "pago_mayor");
        }

        /* ---- 7. Zaragoza: la clave del dia (VentasPago.cs 3604-3640) ---- */
        var pideClave = !esTauro &&
            (idTipoPago == PagoDescuento || idTipoPago == PagoNotaCredito ||
             idTipoPago == PagoTransferencia || idTipoPago == PagoDepositoEfectivo ||
             idTipoPago == PagoEfectivo);

        if (pideClave)
        {
            var dada = (request.ContrasenaAutorizacion ?? string.Empty).Trim();
            if (dada.Length == 0)
                return Confirmar("Esta operación necesita la clave de autorización.", "autorizacion");

            var ok = await ClaveCorrectaAsync(dada, cons, idUsuarioLegacy, ct);

            /*
              La constancia se deja SIEMPRE, acertada o no, igual que Mac31
              (ConfirmarContrasena.cs 126: se escribe antes de mirar si acerto).
              Esa bitacora es justo la que sirve para ver quien anduvo probando.
            */
            await _repo.RegistrarIntentoDeClaveAsync(FormaLegacy, ok, dada, idUsuarioLegacy, equipo, ct);

            if (!ok)
            {
                var r = Confirmar("La clave de autorización es incorrecta.", "autorizacion");
                return r;
            }
        }

        /* ---- 8. Zaragoza traduce el 11 a 911 (VentasPago.cs 3655-3661) ---- */
        var idTipoPagoSp = (!esTauro && idTipoPago == PagoExcedenteUsados) ? PagoExcedenteUsadosZara : idTipoPago;

        /* ---- 9. Una llamada por remision, con SU monto ---- */
        /*
          Es literalmente lo que hace el API de Mac31: recorre IDsVentaLst y
          llama al procedimiento una vez por remision cambiando solo IDVenta y
          Abono (Mac3Servicios, VentasPagosService.insertAsync, lineas 84-95).
          El resto de los datos —el numero del cheque, el banco, el monto del
          documento— va IGUAL en todas: un cheque que cubre cuatro remisiones
          queda anotado con su numero en las cuatro.

          El procedimiento hace COMMIT dentro de cada llamada, asi que si la
          tercera falla las dos primeras YA quedaron grabadas. Legacy se
          comporta igual (corta el recorrido y devuelve el error). Aqui al menos
          se dice cuantas y cuales alcanzaron a pasar, que es lo que necesita
          quien tiene que arreglarlo en Mac31.
        */
        var resp = new VentasMultiPagoGuardarResponse();
        var folioPorId = crudas.ToDictionary(c => c.IdVenta, c => c.FolioFtm);

        foreach (var c in crudas)
        {
            if (!porVenta.TryGetValue(c.IdVenta, out var monto) || monto <= 0) continue;

            var p = new MultiPagoParametrosSp
            {
                IdCobro = idCobro,
                IdVenta = c.IdVenta,
                /*
                  IDCliente va en 0: Mac31 solo lo manda cuando el pago es por
                  excedente de dinero (VentasPago.cs 3675), que no es este caso.
                  Mandarlo lleno cambiaria de rama dentro del procedimiento.
                */
                IdCliente = 0,
                IdTipoPago = idTipoPagoSp,
                IdUsuario = idUsuarioLegacy,
                Abono = monto,
                MontoTotal = montoTotal,
                Observaciones = (request.Observaciones ?? string.Empty).Trim(),
                IdBancoCheque = idBancoCheque,
                IdBancoTransfer = idBancoTransfer,
                IdBancoTarjeta = idBancoTarjeta,
                Transferencia = transferencia,
                Cheque = numeroCheque,
                Tarjeta = tarjeta,
                /*
                  Siempre 0: la casilla "es diferencia de usados" solo se
                  muestra con una remision y SaldoCascos > 0, y desde Ventas el
                  SaldoCascos llega en 0 (ConsultarVentas.cs 4024).
                */
                DiffUsados = 0,
                DepositoEfectivoNumero = depositoNumero,
                IdBancoDepositoEfe = idBancoDeposito,
                ExcedenteUsadosMotivo = string.Empty,
                Cargo = cargo,
                CargoMotivo = cargoMotivo,
                IdAgenteLiquidacion = idAgente,
                IdRepartidorLiquidacion = idRepartidor,
                TipoTarjeta = tipoTarjeta,
                CascoKiloCantidad = cascoKiloCantidad,
                CascoKiloTotal = cascoKiloTotal,
                CascoKiloPrecio = cascoKiloPrecio,
                CascoKiloMotivo = cascoKiloMotivo,
                Equipo = equipo
            };

            MultiPagoResultadoSp r;
            try
            {
                r = await _repo.InsertarPagoAsync(p, ct);
            }
            catch (Exception ex)
            {
                resp.Guardo = resp.Aplicadas > 0;
                resp.Mensaje = Parcial(resp.Aplicadas, folioPorId.GetValueOrDefault(c.IdVenta, ""), ex.Message);
                return resp;
            }

            if (r.Result != 1 || !string.IsNullOrWhiteSpace(r.Mensaje))
            {
                resp.Guardo = resp.Aplicadas > 0;
                /* El procedimiento separa renglones con @@@@ (VentasPago.cs 3800). */
                var detalle = (r.Mensaje ?? string.Empty).Replace("@@@@", " ").Trim();
                resp.Mensaje = Parcial(resp.Aplicadas, folioPorId.GetValueOrDefault(c.IdVenta, ""), detalle);
                return resp;
            }

            resp.Aplicadas++;
            resp.FoliosAplicados.Add(c.FolioFtm);
            resp.SaldoFavorDineroCliente = r.SaldoFavorDineroCliente;
            resp.SaldoFavorCascosCliente = r.SaldoFavorCascosCliente;
        }

        if (resp.Aplicadas == 0)
            return mal("No se aplicó ningún pago.");

        resp.Guardo = true;
        resp.Mensaje = resp.Aplicadas == 1
            ? $"Pago aplicado a la remisión {resp.FoliosAplicados[0]}."
            : $"Pago aplicado a {resp.Aplicadas} remisiones.";
        return resp;
    }

    /* ---------------------------------------------------------------- */

    private static VentasMultiPagoGuardarResponse Confirmar(string mensaje, string que) =>
        new() { Guardo = false, Mensaje = mensaje, RequiereConfirmacion = que };

    private static string Parcial(int aplicadas, string folio, string detalle)
    {
        var baseMsg = $"No se pudo aplicar el pago de la remisión {folio}. {detalle}".Trim();
        if (aplicadas == 0) return baseMsg;
        return baseMsg + $" Ojo: {aplicadas} remisión(es) anterior(es) YA quedaron pagadas.";
    }

    /// <summary>
    /// Dos decimales, redondeando como SQL Server: 0.5 hacia arriba. Los
    /// campos del procedimiento son decimal(18,2); mandar mas decimales los
    /// haria redondear alla, y entonces el numero de la pantalla no seria el
    /// numero guardado.
    /// </summary>
    private static decimal Redondear(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// LA CLAVE DEL DIA DE ZARAGOZA.
    ///
    /// Copiada de Mac31, ConfirmarContrasena.cs GenerarContrasena (linea 48):
    ///
    ///     HH + mm  de la hora ACTUAL
    ///   + dd       del dia de la FECHA DE OPERACION (Constantes)
    ///   + dos letras del MES de esa misma fecha (EN FE MA AB MA JU JU AG SE OC NO DI)
    ///
    /// Y si el usuario tiene AplicaContrasenaAutorizacion = 1, entonces su
    /// clave no es esa sino la suya (ConfirmarContrasena.cs 99-100).
    ///
    /// Se valida AQUI y no en el navegador a proposito: una clave que el front
    /// puede calcular es una clave que cualquiera puede leer en el bundle.
    ///
    /// Se acepta el minuto anterior ademas del actual. En Mac31 la clave se
    /// teclea y se acepta en la misma maquina en milisegundos; aqui hay un
    /// viaje de red en medio, y una clave que cambia cada minuto rechazaria a
    /// quien la tecleo en el segundo 59 por culpa del reloj, no de la persona.
    /// </summary>
    private async Task<bool> ClaveCorrectaAsync(
        string dada, MultiPagoConstantes cons, int idUsuario, CancellationToken ct)
    {
        var propia = await _repo.ContrasenaDeUsuarioAsync(idUsuario, ct);
        if (propia.Aplica == 1)
            return !string.IsNullOrWhiteSpace(propia.Contrasena)
                && string.Equals(dada, propia.Contrasena.Trim(), StringComparison.OrdinalIgnoreCase);

        var fechaOp = cons.FechaOperacion ?? DateTime.Now;
        var dia = fechaOp.Day.ToString("00", CultureInfo.InvariantCulture);
        var mes = MesEnLetras(fechaOp.Month);

        var ahora = DateTime.Now;
        foreach (var t in new[] { ahora, ahora.AddMinutes(-1) })
        {
            var esperada = t.ToString("HH", CultureInfo.InvariantCulture)
                         + t.ToString("mm", CultureInfo.InvariantCulture)
                         + dia + mes;
            if (string.Equals(dada, esperada, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    /* Las mismas dos letras que Mac31, repeticiones incluidas: marzo y mayo
       comparten "MA", y junio y julio comparten "JU". No es un error de copia:
       es lo que valida la clave de todos los dias en Zaragoza. */
    private static string MesEnLetras(int mes) => mes switch
    {
        1 => "EN", 2 => "FE", 3 => "MA", 4 => "AB",
        5 => "MA", 6 => "JU", 7 => "JU", 8 => "AG",
        9 => "SE", 10 => "OC", 11 => "NO", 12 => "DI",
        _ => string.Empty
    };
}
