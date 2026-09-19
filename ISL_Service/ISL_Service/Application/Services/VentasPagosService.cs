using System.Globalization;
using ISL_Service.Application.DTOs.VentasPagos;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/*
  LAS REGLAS DE LA PANTALLA DE PAGOS, EN UN SOLO SITIO

  Todas salen de Legacy/Mac31/Mac31/Forms/Ventas/ConsultarVentasPagos.cs y del
  procedimiento sp_n_CancelarVentasPagos. Se citan por linea una por una, para
  que el dia que alguien dude pueda ir a ver el original en vez de creerle a
  este archivo.

  QUE PASA CON LAS DOS EMPRESAS
  ------------------------------
  Se compararon los tres procedimientos de esta pantalla entre Produccion_svr
  (Tauro) y MacZ (Zaragoza), ignorando espacios:

    sp_n_ConsultaVentasPagos   IDENTICOS, byte por byte
    sp_n_CancelarVentasPagos   IDENTICOS salvo SET ANSI_WARNINGS/ANSI_NULLS
    sp_n_InsertarVentasPagos   difieren en el excedente de usados, que es del
                               alta de pago (otra pantalla), no de esta

  O sea: el texto del codigo NO cambia. Lo que cambia es el DATO que los dos
  procedimientos leen de la tabla Constantes:

    Tauro     EsquemaPago = 'TAU'   Funcionalidad = 'TAU'
    Zaragoza  EsquemaPago = 'ZARA'  Funcionalidad = 'ZARA'

  Y con eso cambian TRES cosas de verdad:

  1) El SALDO (sp_n_ConsultaVentasPagos, lineas 335-339)
       TAU   [Total a Pagar]  + Cargos - Descuentos - Abonos
       ZARA  ImporteConIvaRnd + Cargos - Descuentos - Abonos

  2) El SALDO ACUMULADO de cada renglon (lineas 319-328). En Zaragoza el
     renglon de la venta arranca en ImporteConIvaRnd y el acumulado se suma
     ignorando los movimientos con IDTipoPago = 0 y los de diferencia de
     cascos; en Tauro es la resta llana de cargos menos abonos.

  3) El nombre del movimiento sin tipo de pago: en Tauro se llama "VENTA" y en
     Zaragoza "IMPORTE" (lineas 299-309).

  Nada de eso se reimplementa aqui: los numeros llegan ya calculados. Se
  documenta porque explica por que el mismo folio se ve distinto en cada
  empresa y no es un error.

  4) Al CANCELAR, el saldo a favor (sp_n_CancelarVentasPagos, lineas 960-971).
     Zaragoza se niega si al deshacer el pago el cliente quedaria con saldo a
     favor negativo —"EL SALDO A FAVOR YA FUE APLICADO."—; Tauro fuerza ese
     saldo a cero justo antes de la comprobacion, asi que nunca se niega por
     ese motivo. Ese "if" SI vive en el procedimiento, y por eso no se copia:
     se deja que conteste el.
*/
public class VentasPagosService : IVentasPagosService
{
    private readonly IVentasPagosRepository _repo;

    public VentasPagosService(IVentasPagosRepository repo)
    {
        _repo = repo;
    }

    public async Task<VentasPagosRespuesta> ConsultarAsync(int idVenta, CancellationToken ct)
    {
        var crudos = await _repo.ConsultarAsync(idVenta, ct);
        var esquema = await _repo.EsquemaPagoAsync(ct);
        return Armar(idVenta, crudos, esquema);
    }

    public async Task<VentasPagoCancelarRespuesta> CancelarPagoAsync(
        VentasPagoCancelarRequest peticion,
        int idUsuario,
        string equipo,
        CancellationToken ct)
    {
        /*
          SE VUELVE A LEER ANTES DE ESCRIBIR.

          Mac31 valida contra lo que tiene pintado en la rejilla, que puede
          llevar minutos ahi. En el web la pantalla puede estar abierta desde
          ayer y, peor, dos personas pueden tenerla abierta a la vez. Volver a
          consultar cuesta una ida a la base y evita cancelar dos veces el mismo
          pago o cancelar uno de una remision que ya se cancelo entera.
        */
        var esquema = await _repo.EsquemaPagoAsync(ct);
        var crudos = await _repo.ConsultarAsync(peticion.IdVenta, ct);
        var pantalla = Armar(peticion.IdVenta, crudos, esquema);

        if (crudos.Count == 0)
        {
            return new VentasPagoCancelarRespuesta
            {
                Cancelado = false,
                Mensaje = "NO SE ENCONTRÓ LA REMISIÓN.",
                Pantalla = pantalla
            };
        }

        var renglon = pantalla.Renglones.FirstOrDefault(x => x.IdPagoVenta == peticion.IdPagoVenta);
        if (renglon is null)
        {
            /*
              Mac31 nunca llega aqui porque toma el renglon de su propia
              rejilla. En el web el id viaja por la red, asi que hay que
              contestar algo cuando no corresponde a esta remision — y sobre
              todo, NO pasarselo al procedimiento: seria cancelar el pago de
              otra venta.
            */
            return new VentasPagoCancelarRespuesta
            {
                Cancelado = false,
                Mensaje = "SELECCIONE EL PAGO A CANCELAR.",
                Pantalla = pantalla
            };
        }

        /*
          LA REMISION CANCELADA NO ADMITE CANCELAR PAGOS.

          En Mac31 esto no es una validacion sino que el boton desaparece
          (ConsultarVentasPagos.cs, lineas 137-142). Aqui tiene que ser una
          validacion de verdad: el boton del web se puede pulsar siempre.
        */
        if (!pantalla.Cabecera.PuedeCancelarPago)
        {
            return new VentasPagoCancelarRespuesta
            {
                Cancelado = false,
                Mensaje = pantalla.Cabecera.MotivoCancelarPago,
                Pantalla = pantalla
            };
        }

        if (!renglon.PuedeCancelar)
        {
            return new VentasPagoCancelarRespuesta
            {
                Cancelado = false,
                Mensaje = renglon.MotivoNoCancelar,
                Pantalla = pantalla
            };
        }

        var resultado = await _repo.CancelarAsync(
            peticion.IdPagoVenta, peticion.IdVenta, idUsuario, equipo, ct);

        /*
          Se recarga SIEMPRE, salga bien o mal. El procedimiento hace su propio
          ROLLBACK cuando se niega, asi que en el "mal" la pantalla no cambio;
          pero es justo cuando mas hace falta enseñar el estado real, porque el
          motivo de la negativa casi siempre es que alguien mas movio algo.
        */
        var despues = Armar(
            peticion.IdVenta,
            await _repo.ConsultarAsync(peticion.IdVenta, ct),
            esquema);

        /*
          MAC31 DISTINGUE TRES FINALES, NO DOS (lineas 487-507):

            el servicio falla          -> "NO SE PUDO REALIZAR LA OPERACIÓN."
            contesta con mensaje       -> se enseña ESE mensaje, y no se cancelo
            contesta sin mensaje       -> "EL PAGO HA SIDO CANCELADO."

          El del medio es el importante: el procedimiento devuelve result = -1
          con un texto explicando por que —"EL ABONO YA SE ENCUENTRA CANCELADO",
          "EL SALDO A FAVOR YA FUE APLICADO"— y ese texto es informacion, no un
          error tecnico. Se pasa tal cual.
        */
        var ok = resultado.Result == 1 && string.IsNullOrWhiteSpace(resultado.Mensaje);

        return new VentasPagoCancelarRespuesta
        {
            Cancelado = ok,
            Mensaje = ok
                ? "EL PAGO HA SIDO CANCELADO."
                : string.IsNullOrWhiteSpace(resultado.Mensaje)
                    ? "NO SE PUDO REALIZAR LA OPERACIÓN."
                    /* Legacy separa los renglones del mensaje con '####' y
                       '@@@@'. En una caja de Windows eso era un salto de linea;
                       aqui se vuelve espacio para que no salga la marca cruda. */
                    : resultado.Mensaje.Replace("####", " ").Replace("@@@@", " ").Trim(),
            Pantalla = despues
        };
    }

    /* ---------------------------------------------------------------- */
    /* De los renglones crudos a la pantalla                             */
    /* ---------------------------------------------------------------- */

    private static VentasPagosRespuesta Armar(int idVenta, List<VentasPagoCrudo> crudos, string esquema)
    {
        var resp = new VentasPagosRespuesta();
        resp.Cabecera.IdVenta = idVenta;
        resp.Cabecera.EsquemaPago = esquema;

        if (crudos.Count == 0)
        {
            /*
              Mac31 se cierra solo cuando el servicio no contesta
              (ConsultarVentasPagos.cs, lineas 45-49). Una remision SIEMPRE trae
              al menos el renglon de la venta, asi que una lista vacia significa
              que el IDVenta no existe. Se devuelve la pantalla vacia con los
              dos botones apagados y su motivo, en vez de un error tecnico.
            */
            resp.Cabecera.MotivoNuevoPago = "NO SE ENCONTRÓ LA REMISIÓN.";
            resp.Cabecera.MotivoCancelarPago = "NO SE ENCONTRÓ LA REMISIÓN.";
            return resp;
        }

        /*
          TODA LA CABECERA SALE DEL RENGLON 0, igual que Mac31 (lineas 78-96).
          No es un atajo: el procedimiento repite los totales en todos los
          renglones, y tomar el primero es lo que hace que el web enseñe el
          mismo numero que la pantalla de al lado.
        */
        var c = crudos[0];

        resp.Cabecera.FolioFtm = c.FolioFtm;
        resp.Cabecera.Cliente = $"{c.NumeroCliente} - {c.NombreCliente}".Trim();
        resp.Cabecera.Empresa = c.Empresa;
        resp.Cabecera.VencimientoFtm = c.FechaVencimientoFtm;
        resp.Cabecera.Agente = c.NumeroNombreAgente;

        resp.Cabecera.Importe = c.TotalPagar;
        resp.Cabecera.Cargos = c.Cargos;
        resp.Cabecera.Descuentos = c.Descuentos;
        resp.Cabecera.Abonos = c.Abonos;
        resp.Cabecera.Saldo = c.Saldo;

        resp.Cabecera.PagaConEfectivo = c.PagaConEfectivo == 1;
        resp.Cabecera.PagaConCheque = c.PagaConCheque == 1;
        resp.Cabecera.PagaConTransferencia = c.PagaConTransferencia == 1;
        resp.Cabecera.PagaConDepositoEfectivo = c.PagaConDepositoEfectivo == 1;

        resp.Cabecera.CancelacionFtm = c.FechaCancelacionFtm;
        resp.Cabecera.Cancelada = !string.IsNullOrWhiteSpace(c.FechaCancelacionFtm);

        /*
          LOS DOS BOTONES, CON LAS REGLAS DE MAC31 (lineas 134-142).

          Alla se esconden; aqui se apagan y se dice por que. El orden importa:
          la remision cancelada gana sobre el saldo en cero, porque es el motivo
          mas fuerte y el que explica de verdad lo que pasa.
        */
        if (resp.Cabecera.Cancelada)
        {
            var motivo = $"LA REMISIÓN {c.FolioFtm} ESTÁ CANCELADA.";
            resp.Cabecera.PuedeNuevoPago = false;
            resp.Cabecera.MotivoNuevoPago = motivo;
            resp.Cabecera.PuedeCancelarPago = false;
            resp.Cabecera.MotivoCancelarPago = motivo;
        }
        else
        {
            resp.Cabecera.PuedeCancelarPago = true;
            resp.Cabecera.MotivoCancelarPago = string.Empty;

            /*
              Saldo cero = no hay nada que abonar. Mac31 esconde el boton
              (linea 134) y ademas lo vuelve a esconder despues de cada pago y
              de cada cancelacion (lineas 403, 443, 513): es la misma regla
              repetida seis veces alla, una sola vez aqui.
            */
            resp.Cabecera.PuedeNuevoPago = c.Saldo != 0m;
            resp.Cabecera.MotivoNuevoPago = c.Saldo != 0m
                ? string.Empty
                : "LA REMISIÓN NO TIENE SALDO.";
        }

        foreach (var x in crudos)
        {
            var cancelado = string.Equals(x.Cancelado, "SI", StringComparison.OrdinalIgnoreCase);

            /*
              EL CARGO SE MIRA EN VALOR ABSOLUTO.

              Mac31 hace Math.Abs al leerlo (linea 416) porque los descuentos se
              guardan en negativo. Sin el, un descuento de -500 daba cargo < 0,
              no entraba en el "if (cargo > 0)" de la confirmacion y se
              cancelaba SIN PREGUNTAR.
            */
            var cargo = Math.Abs(x.Cargo);
            var abono = x.Abono;

            var renglon = new VentasPagoRenglon
            {
                IdPagoVenta = x.IdPagoVenta,
                FechaFtm = x.FechaPagoFtm,
                Usuario = x.Usuario,
                Equipo = x.Equipo,
                NumeroPago = x.NumeroPago,
                Movimiento = x.Movimiento,
                FormaCorta = x.TipoPagoCorto,
                Cargo = x.Cargo,
                Abono = x.Abono,
                SaldoAcumulado = x.SaldoAcumulado,
                DiffUsados = string.Equals(x.DiffUsadosTexto, "SI", StringComparison.OrdinalIgnoreCase),
                AgenteLiquidacion = x.AgenteLiquidacion,
                RepartidorLiquidacion = x.RepartidorLiquidacion,
                DetallePago = x.DetallePago,
                Cancelado = cancelado,
                CancelacionFtm = x.FechaPagoCancelacionFtm
            };

            /*
              LAS TRES NEGATIVAS DE btnCancelar_Click, EN SU ORDEN (lineas
              424-447). El orden no es decorativo: un renglon de venta ya
              cancelada cumple varias a la vez y el mensaje que sale en Mac31 es
              el de la PRIMERA.

                1) sin numero de pago -> es el renglon de la VENTA, no un pago
                2) cargo y abono en 0 -> no mueve dinero, no hay nada que deshacer
                3) ya cancelado
            */
            if (string.IsNullOrWhiteSpace(x.NumeroPago))
            {
                renglon.PuedeCancelar = false;
                renglon.MotivoNoCancelar = "SELECCIONE EL PAGO A CANCELAR.";
            }
            else if (cargo == 0m && abono == 0m)
            {
                renglon.PuedeCancelar = false;
                renglon.MotivoNoCancelar = "EL MOVIMIENTO NO SE PUEDE CANCELAR.";
            }
            else if (cancelado)
            {
                renglon.PuedeCancelar = false;
                renglon.MotivoNoCancelar = "EL PAGO YA ESTA CANCELADO.";
            }
            else if (!resp.Cabecera.PuedeCancelarPago)
            {
                renglon.PuedeCancelar = false;
                renglon.MotivoNoCancelar = resp.Cabecera.MotivoCancelarPago;
            }
            else
            {
                renglon.PuedeCancelar = true;

                /*
                  EL TEXTO DE LA CONFIRMACION (lineas 449-473).

                  Son DOS frases distintas y no una: el abono dice "EL PAGO POR
                  $x" y el cargo dice "EL {movimiento} POR $x" — con el nombre
                  del movimiento adentro, porque un cargo puede ser un descuento,
                  una nota de credito o un cargo por cheque devuelto, y no es lo
                  mismo confirmar que cancelas "un pago" que "un DESCUENTO".

                  Mac31 pregunta las DOS cuando el renglon trae abono y cargo a
                  la vez. Aqui se junta en una sola frase: dos dialogos seguidos
                  para un mismo renglon es como se acaba diciendo que si sin
                  leer el segundo.
                */
                var partes = new List<string>();
                if (abono > 0m) partes.Add($"EL PAGO POR ${Pesos(abono)}");
                if (cargo > 0m) partes.Add($"EL {x.Movimiento} POR ${Pesos(cargo)}");

                renglon.TextoConfirmacion = partes.Count == 0
                    ? "¿DESEA CANCELAR ESTE MOVIMIENTO?"
                    : $"¿DESEA CANCELAR {string.Join(" Y ", partes)}?";
            }

            resp.Renglones.Add(renglon);
        }

        return resp;
    }

    /// <summary>El mismo formato de Mac31: "###,##0.00" (lineas 451 y 464).</summary>
    private static string Pesos(decimal valor) =>
        valor.ToString("#,##0.00", CultureInfo.InvariantCulture);
}
