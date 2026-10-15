using ISL_Service.Application.DTOs.ClientesAnexos;
using ISL_Service.Application.DTOs.VentasConsulta;
using ISL_Service.Application.DTOs.VentasFacturacion;
using ISL_Service.Application.DTOs.VentasPagosOperaciones;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Facturacion;

using Reglas = ISL_Service.Infrastructure.Facturacion.ReglasPreparacionFactura;

namespace ISL_Service.Application.Services;

/*
  ══════════════════════════════════════════════════════════════════════════
  PREPARAR UN COMPROBANTE: LAS LECTURAS
  ══════════════════════════════════════════════════════════════════════════

  Las REGLAS viven en `ReglasPreparacionFactura` —funciones puras, probadas sin
  base—. Aqui solo esta el orden de las lecturas y el armado de la respuesta.
  La division es a proposito: una regla fiscal enterrada entre llamadas a
  repositorios no se puede probar, y lo que no se puede probar en facturacion es
  lo que se descubre leyendo un PDF del SAT.

  NO SE DUPLICA NINGUN ENDPOINT
  -----------------------------
  Todo lo que hace falta YA se podia leer; lo que faltaba era que alguien lo
  juntara y lo validara del lado del servidor. Se reusan, tal cual:

    IVentasConsultaService.ConsultarRemisionesAsync   las remisiones
    IVentasPagosOperacionesService.ConsultarDetalleAsync  las partidas
    IClientesAnexosService.ConsultarFacturacionAsync  los domicilios fiscales
    ICascosAFacturarRepository.ConsultarCatalogoAsync el catalogo de cascos
    ICatalogos*Service                                los catalogos del SAT
    IVentasFacturacionService.ConsultarAsync          la factura acreditada
    IVentasFacturacionRepository.LeerConstantesAsync   `funcionalidad` -> ZARA

  Un endpoint nuevo de lectura habria sido un segundo camino a los mismos
  procedimientos, con sus propios candados de almacen y de agente que podrian
  no coincidir con los de la reja. Reusar el servicio de la consulta es lo que
  garantiza que `preparar` NO abre ninguna remision que el usuario no pudiera
  ver en la pantalla de ventas.

  POR QUE LAS LECTURAS VAN EN PARALELO
  ------------------------------------
  Son cuatro viajes independientes (partidas, cascos, catalogos, domicilios
  fiscales) y la pantalla no sirve sin ninguno de ellos. En serie son cuatro
  idas y vueltas antes del primer pixel; medido en el web, eso era la diferencia
  entre abrir la pantalla y esperarla. Lo que NO va en paralelo son las
  remisiones: de ellas sale el `idCliente` con el que se piden los domicilios
  fiscales, asi que tienen que ir primero.
*/
public class VentasFacturacionPreparacionService : IVentasFacturacionPreparacionService
{
    private readonly IVentasConsultaService _consulta;
    private readonly IVentasPagosOperacionesService _detalles;
    private readonly IClientesAnexosService _clientes;
    private readonly ICascosAFacturarRepository _cascos;
    private readonly ICatalogosComplementosService _catalogosFe;
    private readonly ICatalogosGeneralesService _catalogosGenerales;
    private readonly IVentasFacturacionService _facturas;
    private readonly IVentasFacturacionRepository _facturacion;

    public VentasFacturacionPreparacionService(
        IVentasConsultaService consulta,
        IVentasPagosOperacionesService detalles,
        IClientesAnexosService clientes,
        ICascosAFacturarRepository cascos,
        ICatalogosComplementosService catalogosFe,
        ICatalogosGeneralesService catalogosGenerales,
        IVentasFacturacionService facturas,
        IVentasFacturacionRepository facturacion)
    {
        _consulta = consulta;
        _detalles = detalles;
        _clientes = clientes;
        _cascos = cascos;
        _catalogosFe = catalogosFe;
        _catalogosGenerales = catalogosGenerales;
        _facturas = facturas;
        _facturacion = facturacion;
    }

    // ======================================================================
    // FACTURA DE REMISION
    // ======================================================================

    public async Task<FacturaPreparadaDto> PrepararFacturaAsync(
        FacturaPrepararRequest request, int idUsuario, bool puedeVerDeOtros, CancellationToken ct)
    {
        var ids = request.IdsVenta.Where(x => x > 0).Distinct().ToList();

        if (ids.Count == 0 && request.IdPedido <= 0)
        {
            throw new InvalidOperationException(
                "No se recibió ninguna remisión ni un pedido. Una factura sin remisión necesita el pedido.");
        }

        var salida = new FacturaPreparadaDto();

        /*
          LA VARIANTE DE EMPRESA SALE DE `funcionalidad`, NO DEL NOMBRE.

          Es `Variables.funcionalidad.ToUpper().Contains("ZARA")` de Mac31
          (VentasFacturacion.cs:1410, :1496). Con el NOMBRE de la empresa, una
          tercera instalacion —centro de servicio, Luro, aviacion— entra por la
          rama equivocada y el sintoma sale en produccion. El web ya toma esta
          decision igual (FacturaDeRemisionPage.tsx:310-319) y la capacidad
          `facturacionMuestraPreciosConIva` se deriva de lo mismo
          (MapaDeCapacidades.cs:83).
        */
        var constantes = await _facturacion.LeerConstantesAsync(ct);
        var esZaragoza = (constantes.Funcionalidad ?? string.Empty)
            .ToUpperInvariant().Contains("ZARA", StringComparison.Ordinal);
        salida.PreciosConIva = esZaragoza;

        // ---- las remisiones, leidas del servidor y NO de lo que llego ----
        //
        // No es desconfianza del cliente: las puertas de `btnFacturar` —mismo
        // cliente, total distinto de cero, no cancelada— se deciden con el
        // ESTADO DE AHORA de cada remision, no con el que tenia la reja cuando
        // se marco. Y de la lectura salen el cliente, los importes y los
        // correos, que es todo lo que decide el resto.
        var remisiones = ids.Count > 0
            ? await LeerRemisionesAsync(ids, idUsuario, puedeVerDeOtros, ct)
            : new List<FacturaPreparadaRemision>();

        if (ids.Count > 0 && remisiones.Count == 0)
            throw new InvalidOperationException("No se encontró ninguna de esas remisiones.");

        var crudasPorId = remisiones.ToDictionary(r => r.IdVenta);

        // ---- las seis puertas del llamador, otra vez y aqui ----
        var impedimento = Reglas.MotivoQueImpideFacturar(remisiones
            .Select(r => new Reglas.RemisionParaFacturar
            {
                IdVenta = r.IdVenta,
                IdCliente = _clientePorRemision.TryGetValue(r.IdVenta, out var c) ? c : 0,
                FolioFtm = r.FolioFtm,
                TotalPagar = _totalPagarPorRemision.TryGetValue(r.IdVenta, out var t) ? t : 0m,
                Cancelada = r.Cancelada
            })
            .ToList());

        if (ids.Count > 0 && !string.IsNullOrEmpty(impedimento))
        {
            // Es un BLOQUEO, no una excepcion: la pantalla tiene que poder
            // ensenar el motivo con el resto de la informacion a la vista. Un
            // 400 pelado deja a la persona sin saber que remision es la mala.
            salida.Bloqueo = new FacturaPreparadaReclamo { Mensaje = impedimento, Campo = "partidas" };
        }

        var idCliente = _idCliente;
        var idEmpresaFiscal = _idEmpresaMayorista;

        salida.Cliente = new FacturaPreparadaCliente
        {
            IdCliente = idCliente,
            NumeroCliente = _numeroCliente,
            NombreCliente = _nombreCliente,
            IdEmpresa = idEmpresaFiscal != 0 ? idEmpresaFiscal : _idEmpresa
        };

        // ---- las cuatro lecturas que no dependen unas de otras ----
        var tareaPartidas = ids.Count > 0
            ? _detalles.ConsultarDetalleAsync(new VentasDetalleConsultaRequest { IdsVenta = ids }, ct)
            : Task.FromResult(new DTOs.VentasEscrituras.FilasLegacyDto());

        var tareaCascos = _cascos.ConsultarCatalogoAsync(ct);
        var tareaCatalogos = LeerCatalogosDelCfdiAsync(ct);

        var tareaRfcs = idCliente > 0
            ? _clientes.ConsultarFacturacionAsync(
                new ClienteFacturacionFiltro
                {
                    IDCliente = idCliente,
                    // Solo los domicilios ACTIVOS, que es lo que consulta Mac31
                    // (VentasNotaCredito.cs:439).
                    IDStatus = 1,
                    // Mac31 los pide con `IDEmpresaCS = IDEmpresaMayorista`
                    // (VentasFacturacion.cs:1157).
                    IDEmpresaCS = request.IdEmpresaCs != 0 ? request.IdEmpresaCs : idEmpresaFiscal
                }, ct)
            : Task.FromResult(new List<ClienteFacturacionDto>());

        await Task.WhenAll(tareaPartidas, tareaCascos, tareaCatalogos, tareaRfcs);

        var partidas = (await tareaPartidas).Rows;
        var catalogoDeCascos = await tareaCascos;
        salida.Catalogos = await tareaCatalogos;
        var rfcs = await tareaRfcs;

        // ---- el domicilio fiscal: el elegido, o el primero como Mac31 ----
        salida.Rfcs = rfcs.Select(AVista).ToList();

        var elegido = request.IdClienteFacturacion > 0
            ? rfcs.FirstOrDefault(r => r.IDClienteFacturacion == request.IdClienteFacturacion)
            : rfcs.FirstOrDefault();

        /*
          LOS TRES COMBOS SALEN DEL RENGLON DE RFC, no de un valor por omision
          nuestro: al elegir una fila, Mac31 sobreescribe forma de pago, uso de
          CFDI y metodo de pago con los del domicilio fiscal
          (VentasFacturacion.cs:3539-3549). El METODO era el que se perdia —se
          leia solo forma y uso— y no es un adorno: el metodo de pago va DENTRO
          del CFDI.
        */
        var formaPago = Preferir(request.FormaPago, elegido?.IDFEFormaPago);
        var metodoPago = Preferir(request.MetodoPago, elegido?.IDFEMetodoPago);
        var usoCfdi = Preferir(request.UsoCfdi, elegido?.IDFEUsoCFDI);

        // ---- las partidas: marcadas y cantidades ----
        //
        // Sin nada capturado van TODAS marcadas con su cantidad completa, que es
        // como abre Mac31 (`chkSeleccionar.Checked = true`, :218).
        var capturadas = request.Partidas
            .Where(p => p.IdVentaDetalle > 0)
            .GroupBy(p => p.IdVentaDetalle)
            .ToDictionary(g => g.Key, g => g.Last());

        HashSet<int>? marcadas = null;
        Dictionary<int, decimal>? cantidades = null;

        if (capturadas.Count > 0)
        {
            marcadas = capturadas.Where(x => x.Value.Marcada).Select(x => x.Key).ToHashSet();
            cantidades = capturadas.ToDictionary(x => x.Key, x => x.Value.Cantidad);
        }

        /*
          EL IVA VIAJA A LA REGLA PORQUE TAURO LO NECESITA.

          En Tauro el precio del concepto sale de `ImportePartida / Cantidad`,
          que viene SIN IVA, y hay que ponerselo (FacturapiService.cs:768-769).
          Sale de `Constantes.IVA`, igual que lo lee MacServicios2
          (sp_n_VentasDetalleMultiple arranca con `SELECT @PorcentajeIva = IVA/100
          FROM Constantes`), y no de una constante nuestra: el dia que una
          empresa facture a otra tasa, un 0.16 escrito aqui es un CFDI mal hecho.
        */
        var porcentajeIva = constantes.PorcentajeIva;

        var conceptos = Reglas.ConceptosDeLasPartidas(
            partidas.Select(p => (IReadOnlyDictionary<string, object?>)p),
            esZaragoza,
            cantidades,
            marcadas,
            porcentajeIva);

        // La vista lleva TODAS las partidas —tambien las desmarcadas— porque la
        // pantalla tiene que poder volver a marcarlas. Al CFDI solo van las
        // marcadas, que es `conceptos`.
        var todas = Reglas.ConceptosDeLasPartidas(
            partidas.Select(p => (IReadOnlyDictionary<string, object?>)p),
            esZaragoza,
            cantidades,
            null,
            porcentajeIva);

        // ---- los cascos a facturar: catalogo con cantidades en cero ----
        var cascosCapturados = request.Cascos
            .Where(c => c.IdTipoUsado > 0)
            .GroupBy(c => c.IdTipoUsado)
            .ToDictionary(g => g.Key, g => g.Last().Cantidad);

        salida.CatalogoDeCascos = catalogoDeCascos.Select(c => new FacturaPreparadaCasco
        {
            IdTipoUsado = c.IdTipoUsado,
            Tipo = c.Tipo,
            CostoConIva = c.CostoConIva,
            ClaveSat = c.ClaveSat,
            Orden = c.Orden,
            Cantidad = cascosCapturados.TryGetValue(c.IdTipoUsado, out var q) ? q : 0
        }).ToList();

        /*
          EL IMPORTE DE LOS CASCOS: `CostoVentaCreditoConIva * Cantidad`, igual
          que `CalculaTotalCascos` (VentasFacturacion.cs:4042-4085). El precio ya
          viene CON IVA y redondeado por la base; multiplicarlo por 1.16 aqui
          daria centavos de diferencia contra Mac31, y un centavo de diferencia
          en un CFDI es un CFDI distinto.
        */
        var totalCascos = salida.CatalogoDeCascos.Sum(c => c.CostoConIva * c.Cantidad);
        var piezasDeCascos = salida.CatalogoDeCascos.Sum(c => c.Cantidad);

        var conceptosDeCascos = salida.CatalogoDeCascos
            .Where(c => c.Cantidad > 0)
            .Select(c => new FacturaConcepto
            {
                ClaveProdServ = c.ClaveSat,
                Descripcion = c.Tipo,
                ClaveUnidad = Reglas.UnidadDelCasco,
                Cantidad = c.Cantidad,
                PrecioUnitarioConIva = c.CostoConIva,
                Descuento = 0m,
                IdProducto = 0,
                Codigo = string.Empty
            })
            .ToList();

        // ---- los montos por remision ----
        var montosCapturados = request.Montos
            .Where(m => m.IdVenta > 0)
            .GroupBy(m => m.IdVenta)
            .ToDictionary(g => g.Key, g => g.Last().Monto);

        foreach (var r in remisiones)
        {
            r.MontoAFacturar = montosCapturados.TryGetValue(r.IdVenta, out var m)
                ? m
                : r.MontoPropuesto;
        }

        var totalDinero = remisiones.Sum(r => r.MontoAFacturar);
        var totalPorFacturar = remisiones.Sum(r => r.PorFacturar);
        var factor = Reglas.FactorDeComision(formaPago);

        /*
          AQUI SE CIERRA EL COMPROBANTE CONTRA EL COBRO.

          Hasta esta linea cada concepto trae su precio NETO leido de la base,
          que es lo correcto cuando se factura la remision completa. Pero el
          total es editable y casi siempre se mueve, y Legacy entonces no manda
          los netos: reescala los precios y corrige la ultima partida para que la
          suma de EXACTAMENTE lo que se va a facturar
          (FacturapiService.cs:799-889). Sin este paso, el CFDI y el cobro
          difieren —en las ocho facturas reales de MacZ con monto modificado la
          diferencia va de 1,785.00 a 7,290.00 pesos—, y una factura no se puede
          corregir, solo cancelar.

          Va DESPUES de `totalDinero` porque necesita el monto a facturar, y se
          aplica a `conceptos` (los que van al CFDI) y a `todas` (lo que ensena
          la pantalla) para que el usuario vea el mismo precio que se va a
          timbrar.
        */
        var totalEnDineroDeLasRemisiones = Reglas.TotalEnDineroDeLasRemisiones(
            partidas.Select(p => (IReadOnlyDictionary<string, object?>)p),
            esZaragoza);

        // Las piezas que TRAEN las remisiones, para la rama (C) de Zaragoza:
        // total completo pero no todas las piezas (FacturapiService.cs:938-942).
        var piezasDeLasRemisiones = partidas
            .Select(pp => (IReadOnlyDictionary<string, object?>)pp)
            .Sum(pp => Reglas.Numero(Reglas.Crudo(pp, "Cantidad", "cantidad")));

        Reglas.AjustarConceptosAlTotalAFacturar(
            conceptos, esZaragoza, totalDinero, totalEnDineroDeLasRemisiones,
            porcentajeIva, piezasDeLasRemisiones);

        Reglas.AjustarConceptosAlTotalAFacturar(
            todas, esZaragoza, totalDinero, totalEnDineroDeLasRemisiones,
            porcentajeIva, piezasDeLasRemisiones);

        salida.Conceptos = todas.Select(c => new FacturaPreparadaConcepto
        {
            IdVenta = c.IdVenta,
            IdVentaDetalle = c.IdVentaDetalle,
            IdProducto = c.IdProducto,
            Codigo = c.Codigo,
            Descripcion = c.Descripcion,
            ClaveProdServ = c.ClaveProdServ,
            ClaveUnidad = c.ClaveUnidad,
            Cantidad = c.Cantidad,
            CantidadOriginal = c.CantidadOriginal,
            PrecioUnitarioConIva = c.PrecioUnitarioConIva,
            Descuento = c.Descuento,
            PrecioListaLegacy = c.PrecioListaLegacy,
            Importe = c.PrecioUnitarioConIva * c.Cantidad,
            Marcada = marcadas is null || marcadas.Contains(c.IdVentaDetalle)
        }).ToList();


        // Los cascos se suman COMPLETOS a la primera remision (mac :1578-1582).
        // Esto es lo que se refleja en `MontoAFacturar` de la vista, para que la
        // pantalla ensene el mismo numero que va a viajar.
        var reparto = Reglas.RepartirPorRemision(
            remisiones.Select(r => (r.IdVenta, string.IsNullOrEmpty(r.FolioFtm) ? r.Folio.ToString() : r.FolioFtm, r.MontoAFacturar)).ToList(),
            totalCascos);

        foreach (var (idVenta, _, total) in reparto)
        {
            if (crudasPorId.TryGetValue(idVenta, out var vista)) vista.MontoAFacturar = total;
        }

        salida.Remisiones = remisiones;

        var piezasDeProducto = conceptos.Sum(c => c.Cantidad);

        salida.Totales = new FacturaPreparadaTotales
        {
            TotalFacturarDinero = totalDinero,
            TotalFacturarCascos = totalCascos,
            TotalFacturarDineroCascos = totalDinero + totalCascos,
            TotalProductos = (int)decimal.Truncate(piezasDeProducto),
            TotalUsados = piezasDeCascos,
            TotalPorFacturar = totalPorFacturar,
            TopeDeFacturacion = totalPorFacturar * factor,
            FactorDeComision = factor
        };

        // ---- los correos ----
        var disponibles = _correosDeLasRemisiones.Count > 0
            ? _correosDeLasRemisiones
            : Reglas.CorreosDe(elegido?.Email, elegido?.Email2, elegido?.Email3, elegido?.Email4, elegido?.Email5);

        salida.CorreosDisponibles = disponibles;

        // Vacio = TODOS, como Mac31 (:652-653). Y los que llegan se filtran
        // contra los disponibles: un correo que el cliente no tiene dado de alta
        // no se le manda su factura a nadie desde aqui.
        salida.Correos = request.Correos.Count == 0
            ? disponibles.ToList()
            : request.Correos
                .Select(c => c.Trim())
                .Where(c => disponibles.Any(d => string.Equals(d, c, StringComparison.OrdinalIgnoreCase)))
                .ToList();

        // ---- el cuerpo del timbrado ----
        salida.Timbrado = new FacturaTimbrarRequest
        {
            // En centro de servicio la empresa que factura es la MAYORISTA
            // (mac :1633-1634). Fuera de CS las dos son la misma.
            IdEmpresa = idEmpresaFiscal != 0 ? idEmpresaFiscal : _idEmpresa,
            IdCliente = idCliente,
            IdClienteFacturacion = elegido?.IDClienteFacturacion ?? 0,
            IdPedido = request.IdPedido,

            IdsVenta = reparto.Select(x => x.IdVenta).ToList(),
            FoliosVenta = reparto.Select(x => x.Folio).ToList(),
            TotalesFacturar = reparto.Select(x => x.Total).ToList(),

            // `tipo: 0` es FACTURA. Los campos de nota de credito van en cero o
            // vacios porque esto no acredita nada.
            Tipo = 0,
            IdTipoNotaCredito = 0,
            IdVentaFacturaRelacionada = 0,
            IdFacturapiRelacionada = string.Empty,
            RelacionFactura = string.Empty,

            MetodoPago = metodoPago,
            FormaPago = formaPago,
            // El regimen fiscal NO es un desplegable: sale del renglon de RFC
            // elegido (mac :1562).
            RegimenFiscal = elegido?.IDFERegimenFiscal ?? string.Empty,
            UsoCfdi = usoCfdi,

            Receptor = Receptor(elegido, salida.Correos),
            Conceptos = conceptos.Select(c => new FacturaConcepto
            {
                ClaveProdServ = c.ClaveProdServ,
                Descripcion = c.Descripcion,
                ClaveUnidad = c.ClaveUnidad,
                Cantidad = c.Cantidad,
                PrecioUnitarioConIva = c.PrecioUnitarioConIva,
                Descuento = c.Descuento,
                IdProducto = c.IdProducto,
                Codigo = c.Codigo
            }).Concat(conceptosDeCascos).ToList(),

            Observaciones = request.Observaciones ?? string.Empty,

            TotalFacturarDinero = totalDinero,
            TotalFacturarCascos = totalCascos,
            TotalFacturarDineroCascos = totalDinero + totalCascos,
            TotalProductos = salida.Totales.TotalProductos,
            TotalUsados = piezasDeCascos,
            Descuento = 0m,
            IncluirImportes = request.IncluirImportes,
            IdEmpresaCs = 0
        };

        // ---- las validaciones, si no hay ya un bloqueo de las puertas ----
        if (salida.Bloqueo is null)
        {
            var reclamo = Reglas.PrimerReclamoDeFactura(new Reglas.DatosDeLaFactura
            {
                RfcsDisponibles = rfcs.Count,
                HayRfcElegido = elegido is not null,
                FormaPago = formaPago,
                MetodoPago = metodoPago,
                UsoCfdi = usoCfdi,
                TotalDinero = totalDinero,
                TotalCascos = totalCascos,
                TopeDeFacturacion = salida.Totales.TopeDeFacturacion,
                Partidas = conceptos.Select(c => (c.Cantidad, c.CantidadOriginal)).ToList(),
                PiezasDeCascos = piezasDeCascos,
                CorreosDisponibles = disponibles.Count,
                CorreosMarcados = salida.Correos.Count,
                EsZaragoza = esZaragoza,
                // Esta suma se calcula DESPUES del reescalado, asi que es el
                // importe que va a timbrarse, no una "suma de lista". No decide
                // nada: la validacion de descuento de Mac31 se resuelve con
                // `SumaDeListaLegacy` (ver `DescuentoRealDeMac31`). Se deja por
                // si la pantalla la quiere ensenar.
                SumaDeListaWeb = conceptos.Sum(c => c.PrecioUnitarioConIva * c.Cantidad),
                SumaDeListaLegacy = conceptos.Sum(c => c.PrecioListaLegacy * c.Cantidad)
            });

            if (reclamo is not null)
            {
                salida.Bloqueo = new FacturaPreparadaReclamo
                {
                    Mensaje = reclamo.Mensaje,
                    Campo = reclamo.Campo
                };
            }
        }

        /*
          LA CONFIRMACION LITERAL DE MAC31 (VentasFacturacion.cs:1550-1553), con
          el total CON IVA, el nombre de facturacion y el RFC, y con sus saltos de
          linea. Se arma AQUI y no en el cliente por lo mismo que todo lo demas:
          es lo ultimo que se ve antes de un documento que no se puede borrar, y
          dos clientes con dos textos distintos son dos confirmaciones distintas
          sobre la misma factura.
        */
        salida.TextoDeConfirmacion =
            $"¿LA FACTURA SE VA A GENERAR POR\nTOTAL CON IVA: $ {Reglas.Dinero(totalDinero + totalCascos)}\n\n" +
            $"{elegido?.NombreFacturacion ?? string.Empty}\nRFC: {elegido?.RFC ?? string.Empty}\n\n¿DESEA CONTINUAR?";

        // ---- los avisos ----
        salida.Avisos.Add(Reglas.DivergenciasConLegacy.PrecioDelConcepto);
        salida.Avisos.Add(Reglas.DivergenciasConLegacy.SumaDeListaDelDescuento);

        // El web sigue armando los conceptos por su cuenta y sigue mandando el
        // descuento sobre un precio ya descontado. Mientras no pida `preparar`,
        // el aviso viaja en la respuesta: es el unico sitio que leen los dos
        // clientes, y lo que esta mal son comprobantes fiscales.
        salida.Avisos.Add(Reglas.DivergenciasConLegacy.ElWebAplicaElDescuentoDosVeces);

        if (esZaragoza)
        {
            salida.Avisos.Add(Reglas.DivergenciasConLegacy.DescuentoNoBloqueaComoEnMac31);

            // El sobreprecio SE DICE aunque no bloquee: es un dato util —se esta
            // facturando por encima de los precios de lista— y Mac31 lo detecta
            // en su primera pasada aunque despues se lo tape a si mismo.
            var sobreprecio = Reglas.AvisoDeSobreprecio(
                true,
                conceptos.Sum(c => c.PrecioListaLegacy * c.Cantidad),
                totalDinero);

            if (!string.IsNullOrEmpty(sobreprecio)) salida.Avisos.Add(sobreprecio);
        }

        if (!esZaragoza)
        {
            salida.Avisos.Add(
                "Esta empresa no muestra precios con IVA: el precio del concepto sale de " +
                "PrecioLista y el importe de ImportePartida, que es la rama `else` de Mac31 " +
                "(VentasFacturacion.cs:1416-1420). Esa rama NO está medida contra una base: " +
                "la única empresa sin precios con IVA es Tauro y no hay una tercera con la " +
                "que comparar cifras.");
        }

        if (request.IdPedido > 0 && ids.Count == 0)
        {
            salida.Avisos.Add(
                "Factura SIN remisión: el servidor exige el pedido únicamente en este caso " +
                "(\"Una factura sin remisión necesita el pedido\"). Las tres listas de " +
                "remisiones van vacías a propósito.");
        }

        return salida;
    }

    // ======================================================================
    // NOTA DE CREDITO
    // ======================================================================

    public async Task<NotaCreditoPreparadaDto> PrepararNotaCreditoAsync(
        NotaCreditoPrepararRequest request, CancellationToken ct)
    {
        if (request.IdVentaFacturaRelacionada <= 0)
            throw new InvalidOperationException("SELECCIONE UNA FACTURA.");

        var salida = new NotaCreditoPreparadaDto();

        var constantes = await _facturacion.LeerConstantesAsync(ct);
        var esZaragoza = (constantes.Funcionalidad ?? string.Empty)
            .ToUpperInvariant().Contains("ZARA", StringComparison.Ordinal);
        salida.PreciosConIva = esZaragoza;

        /*
          LA FACTURA QUE SE ACREDITA, con `tipo = 0`: son FACTURAS, ni notas de
          credito ni complementos. Es lo que pide Mac31 en
          VentasNotaCredito.cs:256.
        */
        var consulta = await _facturas.ConsultarAsync(new FacturaConsultaRequest
        {
            IdVentaFactura = request.IdVentaFacturaRelacionada,
            Tipo = 0
        }, ct);

        var factura = consulta.Rows.FirstOrDefault();
        if (factura is null)
            throw new InvalidOperationException("No se encontró la factura.");

        if (string.Equals(Reglas.Texto(Reglas.Crudo(factura, "Cancelada")).Trim(), "SI",
                StringComparison.OrdinalIgnoreCase))
        {
            salida.Bloqueo = new FacturaPreparadaReclamo
            {
                Mensaje = "LA FACTURA ESTA CANCELADA.",
                Campo = "facturas"
            };
        }

        var idCliente = Reglas.Entero(Reglas.Crudo(factura, "IDCliente"));
        var idEmpresa = Reglas.Entero(Reglas.Crudo(factura, "IDEmpresa"));

        salida.FolioDeLaFactura = Reglas.Texto(Reglas.Crudo(factura, "FolioFtm"));
        salida.UuidDeLaFactura = Reglas.Texto(Reglas.Crudo(factura, "Uuid", "UUID"));

        // EL SALDO LO MANDA EL PROCEDIMIENTO y no se recalcula: es el numero
        // contra el que se compara el total de la nota (:3066), y una resta
        // propia podria dejar pasar una nota mayor al saldo por un centavo.
        salida.SaldoDeLaFactura = Reglas.Numero(Reglas.Crudo(factura, "Saldo"));

        salida.Total = request.Total;

        var rfcs = await _clientes.ConsultarFacturacionAsync(
            new ClienteFacturacionFiltro { IDCliente = idCliente, IDStatus = 1 }, ct);

        salida.Rfcs = rfcs.Select(AVista).ToList();

        var elegido = request.IdClienteFacturacion > 0
            ? rfcs.FirstOrDefault(r => r.IDClienteFacturacion == request.IdClienteFacturacion)
            : rfcs.FirstOrDefault();

        var formaPago = Preferir(request.FormaPago, elegido?.IDFEFormaPago);
        var metodoPago = Preferir(request.MetodoPago, elegido?.IDFEMetodoPago);
        var usoCfdi = Preferir(request.UsoCfdi, elegido?.IDFEUsoCFDI);

        /*
          LOS CORREOS SALEN DE LA FACTURA ACREDITADA, no del cliente: son los
          cinco `Email`..`Email5` que trae el renglon
          (VentasNotaCredito.cs:543-548). Si la factura no los trae se cae al
          domicilio fiscal, que es de donde salieron en su dia.
        */
        var disponibles = Reglas.CorreosDe(
            Reglas.Texto(Reglas.Crudo(factura, "Email")),
            Reglas.Texto(Reglas.Crudo(factura, "Email2")),
            Reglas.Texto(Reglas.Crudo(factura, "Email3")),
            Reglas.Texto(Reglas.Crudo(factura, "Email4")),
            Reglas.Texto(Reglas.Crudo(factura, "Email5")));

        if (disponibles.Count == 0)
        {
            disponibles = Reglas.CorreosDe(
                elegido?.Email, elegido?.Email2, elegido?.Email3, elegido?.Email4, elegido?.Email5);
        }

        salida.CorreosDisponibles = disponibles;
        salida.Correos = request.Correos.Count == 0
            ? disponibles.ToList()
            : request.Correos.Select(c => c.Trim())
                .Where(c => disponibles.Any(d => string.Equals(d, c, StringComparison.OrdinalIgnoreCase)))
                .ToList();

        /*
          LAS PARTIDAS DE LA NOTA NO SE LEEN AQUI, Y HAY QUE DECIR POR QUE.

          Mac31 las captura en un PEDIDO de verdad y manda ese `IDPedido` al
          timbrado (:1782 y :2864). El backend nuevo tiene ese camino en su
          propio controlador, `/api/ventas/facturas/nota-credito/*`, y es el que
          devuelve el `snapshot` con las partidas, las dos rejillas de usados y
          TODOS los totales ya calculados por la base.

          Este `preparar` NO vuelve a leerlo: duplicarlo seria un segundo camino
          al mismo procedimiento y, sobre todo, obligaria a reimplementar aqui
          los descuentos, las existencias, los cascos y el IVA. Lo que se prepara
          es el ENCABEZADO fiscal. Los conceptos los manda el cliente con lo que
          le devolvio la captura, y se validan contra el saldo de la factura.
        */
        salida.Avisos.Add(
            "Las partidas de la nota de crédito NO las arma este endpoint: se capturan en " +
            "/api/ventas/facturas/nota-credito/* (un pedido de verdad, como Mac31 en " +
            "VentasNotaCredito.cs:1782 y :2864) y ese IDPedido se IMPRIME en el comprobante " +
            "(\"APLICA A PEDIDOS 116379\", comprobado en un PDF real de MacZ). Aquí se " +
            "prepara el encabezado fiscal: receptor, claves del SAT, relación y validaciones.");

        salida.Timbrado = new FacturaTimbrarRequest
        {
            IdEmpresa = idEmpresa,
            IdCliente = idCliente,
            IdClienteFacturacion = elegido?.IDClienteFacturacion ?? 0,
            IdPedido = request.IdPedido,

            // Vacias: una nota de credito no factura remisiones. Mac31 tiene esas
            // tres listas COMENTADAS en esta pantalla (:2815-2834).
            IdsVenta = new List<int>(),
            FoliosVenta = new List<string>(),
            TotalesFacturar = new List<decimal>(),

            // 1 nota de credito, 2 nota de credito de CASCOS. No se acepta 0
            // desde fuera: un 0 aqui seria simular una VENTA desde facturacion.
            Tipo = request.Tipo == 2 ? 2 : 1,
            IdTipoNotaCredito = request.IdTipoNotaCredito,
            IdVentaFacturaRelacionada = request.IdVentaFacturaRelacionada,
            IdFacturapiRelacionada = Reglas.Texto(Reglas.Crudo(factura, "IDFacturapi")),

            RelacionFactura = ReglasFacturacion.ClaveSat(request.RelacionFactura),
            MetodoPago = metodoPago,
            FormaPago = formaPago,
            RegimenFiscal = elegido?.IDFERegimenFiscal ?? string.Empty,
            UsoCfdi = usoCfdi,

            Receptor = Receptor(elegido, salida.Correos),
            Conceptos = request.Conceptos,
            Observaciones = request.Observaciones ?? string.Empty,

            // mac :2847 — Mac31 tiene `InlcuirDetalleTipos` (con la errata en el
            // nombre) y lo manda SIEMPRE en 0, con la linea que lo leia de una
            // casilla comentada justo arriba. Se copia el 0 y no la casilla
            // muerta.
            IncluirImportes = 0,
            IdEmpresaCs = 0
        };

        if (salida.Bloqueo is null)
        {
            // El orden es el de `ValidacionesFacturacion` (:2996) y las dos de
            // `btnFacturar` (:2755 y :2794). El web ya puso la de "agregue
            // productos" PRIMERO a proposito: sin una sola partida no hay nota, y
            // hacer que la persona elija cinco cosas para enterarse al final de
            // que falta lo primero son cinco pasos tirados.
            if (elegido is null)
                salida.Bloqueo = Recl("INGRESE EL RFC DEL CLIENTE.", "rfc");
            else if (string.IsNullOrWhiteSpace(formaPago))
                salida.Bloqueo = Recl("SELECCIONE LA FORMA DE PAGO.", "formaPago");
            else if (string.IsNullOrWhiteSpace(metodoPago))
                salida.Bloqueo = Recl("SELECCIONE EL MÉTODO DE PAGO.", "metodoPago");
            else if (string.IsNullOrWhiteSpace(usoCfdi))
                salida.Bloqueo = Recl("SELECCIONE EL USO DEL CFDI.", "usoCfdi");
            else if (request.IdTipoNotaCredito <= 0)
                // ERRATA de Mac31 (:3036): dice "SELECCIONE EL TIPO DE CRÉDITO."
                // y el campo que enfoca se llama TIPO DE NOTA DE CRÉDITO. "Tipo
                // de credito" es OTRA COSA en MAC, con pantalla propia, asi que
                // el mensaje manda a buscar un campo que aqui no existe. Se
                // corrige, como ya lo corrigio el web.
                salida.Bloqueo = Recl("SELECCIONE EL TIPO DE NOTA DE CRÉDITO.", "tipoNotaCredito");
            else if (string.IsNullOrWhiteSpace(request.RelacionFactura))
                salida.Bloqueo = Recl("SELECCIONE LA RELACIÓN CON LA FACTURA.", "relacionFactura");
            else if (disponibles.Count == 0)
                // Aqui Mac31 lo escribe BIEN (:3054); la errata "TIENER" es del
                // complemento de pago.
                salida.Bloqueo = Recl("NECESITA TENER AL MENOS UN CORREO ELECTRÓNICO.", "correos");
            else if (salida.Correos.Count == 0)
                salida.Bloqueo = Recl("SELECCIONE UN CORREO.", "correos");
            else if (request.IdPedido <= 0 || request.Conceptos.Count == 0)
                // ERRATA de Mac31 (:2755): dice "SELECCIONE LOS PRODUCTOS..."
                // pero el bucle de :2741-2751 mete TODOS los renglones sin mirar
                // ninguna casilla —la comprobacion de SELECCIONAR esta comentada
                // en :2746—, asi que esto solo salta cuando la rejilla esta
                // VACIA. O sea que pide "seleccionar" algo que no se selecciona,
                // y lo que de verdad falta es AGREGAR.
                salida.Bloqueo = Recl("AGREGUE LOS PRODUCTOS PARA LA NOTA DE CRÉDITO.", "partidas");
            else if (request.Total > salida.SaldoDeLaFactura)
                // :3066. El saldo viene del servidor con la factura.
                salida.Bloqueo = Recl(
                    "EL TOTAL DE LA NOTA DE CRÉDITO NO PUEDE SER MAYOR AL SALDO DE LA FACTURA.",
                    "partidas");
            else if (request.Total <= 0m)
                // :2794. Va al final porque en Mac31 es la ultima antes de
                // confirmar.
                salida.Bloqueo = Recl("EL TOTAL DE LA NOTA DE CRÉDITO DEBE SER MAYOR DE CERO.", "partidas");
        }

        salida.TextoDeConfirmacion =
            $"¿LA NOTA DE CRÉDITO SE VA A GENERAR POR\nTOTAL CON IVA: $ {Reglas.Dinero(salida.Total)}\n\n" +
            $"{elegido?.NombreFacturacion ?? string.Empty}\nRFC: {elegido?.RFC ?? string.Empty}\n\n¿DESEA CONTINUAR?";

        return salida;
    }

    // ======================================================================
    // COMPLEMENTO DE PAGO
    // ======================================================================

    public async Task<ComplementoPreparadoDto> PrepararComplementoAsync(
        ComplementoPrepararRequest request, CancellationToken ct)
    {
        var ids = request.IdsVentaFactura.Where(x => x > 0).Distinct().ToList();
        if (ids.Count == 0)
            throw new InvalidOperationException("SELECCIONE UNA FACTURA.");

        var salida = new ComplementoPreparadoDto();

        var consulta = await _facturas.ConsultarAsync(new FacturaConsultaRequest
        {
            IdsVentaFactura = ids,
            Tipo = 0
        }, ct);

        if (consulta.Rows.Count == 0)
            throw new InvalidOperationException("No se encontró ninguna de esas facturas.");

        var pagosCapturados = request.Pagos
            .Where(p => p.IdVentaFactura > 0)
            .GroupBy(p => p.IdVentaFactura)
            .ToDictionary(g => g.Key, g => g.Last().Importe);

        foreach (var f in consulta.Rows)
        {
            var id = Reglas.Entero(Reglas.Crudo(f, "IDVentaFactura"));
            var total = Reglas.Numero(Reglas.Crudo(f, "TotalPagarFactura"));
            var abonos = Reglas.Numero(Reglas.Crudo(f, "Abonos"));
            var notas = Reglas.Numero(Reglas.Crudo(f, "NotasCredito"));
            var saldo = Reglas.SaldoDeFactura(total, abonos, notas);

            salida.Documentos.Add(new ComplementoPreparadoDocumento
            {
                IdVentaFactura = id,
                IdFacturapi = Reglas.Texto(Reglas.Crudo(f, "IDFacturapi")),
                FolioFtm = Reglas.Texto(Reglas.Crudo(f, "FolioFtm")),
                Uuid = Reglas.Texto(Reglas.Crudo(f, "Uuid", "UUID")),
                FechaFtm = Reglas.Texto(Reglas.Crudo(f, "FechaFtm", "FacturaDateFtm")),
                TotalPagarFactura = total,
                Abonos = abonos,
                NotasCredito = notas,
                Saldo = saldo,
                // Arranca en el saldo COMPLETO. Es el caso normal —se paga lo que
                // se debe— y asi la pantalla abre con el importe ya puesto en vez
                // de con un cero que hay que teclear factura por factura.
                Importe = pagosCapturados.TryGetValue(id, out var m) ? m : saldo,
                NumeroPagos = Reglas.Entero(Reglas.Crudo(f, "NumeroPagos")),
                Cancelada = Reglas.Texto(Reglas.Crudo(f, "Cancelada")).Trim()
            });
        }

        var primera = consulta.Rows[0];
        var idCliente = Reglas.Entero(Reglas.Crudo(primera, "IDCliente"));

        salida.Cliente = new FacturaPreparadaCliente
        {
            IdCliente = idCliente,
            NumeroCliente = Reglas.Texto(Reglas.Crudo(primera, "NumeroCliente")),
            NombreCliente = Reglas.Texto(Reglas.Crudo(primera, "NombreCliente")),
            IdEmpresa = Reglas.Entero(Reglas.Crudo(primera, "IDEmpresa"))
        };

        var tareaRfcs = _clientes.ConsultarFacturacionAsync(
            new ClienteFacturacionFiltro { IDCliente = idCliente, IDStatus = 1 }, ct);
        var tareaFormas = _catalogosFe.ConsultarCatalogoFeAsync(CatalogoFe.FormasPago, 0, null, false, ct);

        await Task.WhenAll(tareaRfcs, tareaFormas);

        var rfcs = await tareaRfcs;
        salida.Rfcs = rfcs.Select(AVista).ToList();
        salida.FormasDePago = (await tareaFormas)
            .Select(x => new OpcionSatDto { Clave = x.FEClave, Descripcion = x.Descripcion })
            .Where(x => !string.IsNullOrEmpty(x.Clave))
            .ToList();

        var elegido = request.IdClienteFacturacion > 0
            ? rfcs.FirstOrDefault(r => r.IDClienteFacturacion == request.IdClienteFacturacion)
            : rfcs.FirstOrDefault();

        var formaPago = Preferir(request.FormaPago, elegido?.IDFEFormaPago);

        var disponibles = Reglas.CorreosDe(
            consulta.Rows.Select(f => Reglas.Texto(Reglas.Crudo(f, "Email"))).ToArray());

        if (disponibles.Count == 0)
        {
            disponibles = Reglas.CorreosDe(
                elegido?.Email, elegido?.Email2, elegido?.Email3, elegido?.Email4, elegido?.Email5);
        }

        salida.CorreosDisponibles = disponibles;
        salida.Correos = request.Correos.Count == 0
            ? disponibles.ToList()
            : request.Correos.Select(c => c.Trim())
                .Where(c => disponibles.Any(d => string.Equals(d, c, StringComparison.OrdinalIgnoreCase)))
                .ToList();

        salida.TotalSaldo = salida.Documentos.Sum(d => d.Saldo);
        salida.TotalAPagar = salida.Documentos.Sum(d => d.Importe);

        /*
          LA FECHA DEL PAGO ES OBLIGATORIA Y LLEVA HORA: las dos van DENTRO del
          CFDI. Si no llega, se pone el momento de ahora — que es lo que hace
          Mac31 al abrir la pantalla — en vez de rechazar: rechazar por un campo
          que la pantalla puede rellenar sola es un paso de mas en una pantalla
          que ya tiene siete.
        */
        salida.Timbrado = new ComplementoPagoTimbrarRequest
        {
            IdEmpresa = salida.Cliente.IdEmpresa,
            IdCliente = idCliente,
            IdClienteFacturacion = elegido?.IDClienteFacturacion ?? 0,
            FormaPago = formaPago,
            FechaPago = request.FechaPago ?? DateTime.Now,
            Receptor = Receptor(elegido, salida.Correos),
            Documentos = salida.Documentos
                .Where(d => d.Importe > 0m)
                .Select(d => new ComplementoPagoDocumento
                {
                    IdFacturapi = d.IdFacturapi,
                    IdVentaFactura = d.IdVentaFactura,
                    Importe = d.Importe
                })
                .ToList(),
            Observaciones = request.Observaciones ?? string.Empty,
            IdEmpresaCs = 0
        };

        var cancelada = salida.Documentos.FirstOrDefault(d =>
            string.Equals(d.Cancelada, "SI", StringComparison.OrdinalIgnoreCase));
        var sinSaldo = salida.Documentos.FirstOrDefault(d => d.Saldo <= 0m);

        var reclamo = Reglas.PrimerReclamoDeComplemento(new Reglas.DatosDelComplemento
        {
            Facturas = salida.Documentos.Count,
            Clientes = consulta.Rows
                .Select(f => Reglas.Entero(Reglas.Crudo(f, "IDCliente")))
                .ToHashSet(),
            FolioCancelado = cancelada?.FolioFtm ?? string.Empty,
            FolioSinSaldo = sinSaldo?.FolioFtm ?? string.Empty,
            FormaPago = formaPago,
            APagar = salida.TotalAPagar,
            Saldo = salida.TotalSaldo,
            Renglones = salida.Documentos.Select(d => (d.Importe, d.Saldo, d.FolioFtm)).ToList(),
            CorreosDisponibles = disponibles.Count,
            CorreosMarcados = salida.Correos.Count
        });

        if (reclamo is not null)
            salida.Bloqueo = Recl(reclamo.Mensaje, reclamo.Campo);

        /*
          LA PARCIALIDAD, EL SALDO ANTERIOR Y EL SALDO INSOLUTO NO VIAJAN, y eso
          hay que decirlo donde se note: son los tres datos que el SAT exige
          DENTRO del complemento, y el DTO de Mac31 tiene campos para ellos
          (`InstallmentsLst`, `LastBalancesLst`, `UuidsLst`,
          `NumComplementosPago`) que la pantalla de alla NO LLENA. Los calcula el
          procedimiento a partir de lo que se paga y de los abonos que la factura
          ya tiene. Quien lea este `preparar` y los eche en falta tiene que poder
          enterarse aqui y no leyendo 1,310 lineas de Mac31.
        */
        salida.Avisos.Add(
            "La parcialidad, el saldo anterior y el saldo insoluto NO van en el cuerpo: los " +
            "calcula el procedimiento con lo que se paga y los abonos que la factura ya " +
            "tiene. Mac31 tiene campos para ellos (InstallmentsLst, LastBalancesLst, " +
            "UuidsLst, NumComplementosPago) y tampoco los llena " +
            "(VentasComplementoPago.cs). Repetir ese cálculo sería tener dos reglas sobre " +
            "un dato fiscal.");

        return salida;
    }

    // ======================================================================
    // AUXILIARES
    // ======================================================================

    private static FacturaPreparadaReclamo Recl(string mensaje, string campo)
        => new() { Mensaje = mensaje, Campo = campo };

    /// Lo que mande el cliente gana; si no manda nada, lo del domicilio fiscal.
    private static string Preferir(string? delCliente, string? delDomicilio)
    {
        if (!string.IsNullOrWhiteSpace(delCliente)) return delCliente.Trim();
        return (delDomicilio ?? string.Empty).Trim();
    }

    /*
      EL RECEPTOR DEL CFDI, ARMADO DEL RENGLON FISCAL.

      web: FacturaDeRemisionPage.tsx:501-514
      Las piezas son las del domicilio fiscal, campo por campo. `municipio`
      viene de `DelegacionMunicipio`, que es como se llama la columna, y
      `correo` es la lista de marcados UNIDA POR COMA: el DTO del timbrado tiene
      un solo campo de correo y el servicio de legacy lo parte.
    */
    private static FacturaReceptor Receptor(ClienteFacturacionDto? r, IEnumerable<string> correos)
        => new()
        {
            RazonSocial = r?.NombreFacturacion ?? string.Empty,
            Rfc = r?.RFC ?? string.Empty,
            RegimenFiscal = r?.IDFERegimenFiscal ?? string.Empty,
            Correo = string.Join(",", correos),
            CodigoPostal = r?.CodigoPostal ?? string.Empty,
            Calle = r?.Calle ?? string.Empty,
            NumeroExterior = r?.NumeroExterior ?? string.Empty,
            NumeroInterior = r?.NumeroInterior ?? string.Empty,
            Colonia = r?.Colonia ?? string.Empty,
            Ciudad = r?.Ciudad ?? string.Empty,
            Municipio = r?.DelegacionMunicipio ?? string.Empty,
            Estado = r?.Estado ?? string.Empty
        };

    private static FacturaPreparadaRfc AVista(ClienteFacturacionDto r) => new()
    {
        IdClienteFacturacion = r.IDClienteFacturacion,
        NombreFacturacion = r.NombreFacturacion,
        Rfc = r.RFC ?? string.Empty,
        // La direccion la arma el SP y se conserva TAL CUAL: es la direccion
        // fiscal como la imprime la factura, y rearmarla aqui con otro orden o
        // otra puntuacion seria ensenar un domicilio distinto del que se timbro.
        Direccion = r.Direccion ?? string.Empty,
        RegimenFiscal = r.IDFERegimenFiscal ?? string.Empty,
        RegimenFiscalNombre = r.FERegimenFiscal ?? string.Empty,
        FormaPago = r.IDFEFormaPago ?? string.Empty,
        MetodoPago = r.IDFEMetodoPago ?? string.Empty,
        UsoCfdi = r.IDFEUsoCFDI ?? string.Empty,
        Correos = Reglas.CorreosDe(r.Email, r.Email2, r.Email3, r.Email4, r.Email5)
    };

    /*
      LOS TRES CATALOGOS DEL SAT QUE PIDE UNA FACTURA, EN UNA SOLA ESPERA.

      SON TRES, NO CINCO, y por eso no se reusa el bloque de la nota de credito:
      los TIPOS DE NOTA y las RELACIONES de factura son de ella. Y no es solo
      traer dos listas de mas — `tipos-nota-credito` esta tras `catalogos.ver`,
      que es otra familia de permisos, y un 403 en una lista que la factura NO
      USA dejaba los tres desplegables vacios (web: facturarRemision.ts:268-278,
      medido con DEMO, que factura en Mac31 y no tiene `catalogos.ver`).
    */
    private async Task<FacturaPreparadaCatalogos> LeerCatalogosDelCfdiAsync(CancellationToken ct)
    {
        var tareaFormas = _catalogosFe.ConsultarCatalogoFeAsync(CatalogoFe.FormasPago, 0, null, false, ct);
        var tareaMetodos = _catalogosFe.ConsultarCatalogoFeAsync(CatalogoFe.MetodosPago, 0, null, false, ct);
        var tareaUsos = _catalogosGenerales.ConsultarFeUsosCfdiAsync(null, null, null, null, null, null, false, ct);

        await Task.WhenAll(tareaFormas, tareaMetodos, tareaUsos);

        return new FacturaPreparadaCatalogos
        {
            FormasDePago = (await tareaFormas)
                .Select(x => new OpcionSatDto { Clave = x.FEClave, Descripcion = x.Descripcion })
                .Where(x => !string.IsNullOrEmpty(x.Clave)).ToList(),
            MetodosDePago = (await tareaMetodos)
                .Select(x => new OpcionSatDto { Clave = x.FEClave, Descripcion = x.Descripcion })
                .Where(x => !string.IsNullOrEmpty(x.Clave)).ToList(),
            /*
              CADA CATALOGO DICE SU DESCRIPCION EN UNA COLUMNA DISTINTA, y hay
              que medirlo. Formas y metodos la traen en `Descripcion`; los usos
              de CFDI en `FeUsoCfdi`. Si se lee el nombre equivocado no truena
              nada: el desplegable sale con la clave y un guion suelto —"03 -"—
              y la persona tiene que elegir el uso del CFDI por su codigo. Es lo
              que pasaba en el web, y por eso esta medido
              (facturarRemision.ts:287-302).
            */
            UsosCfdi = (await tareaUsos)
                .Select(x => new OpcionSatDto { Clave = x.FeClave, Descripcion = x.FeUsoCfdi })
                .Where(x => !string.IsNullOrEmpty(x.Clave)).ToList()
        };
    }

    // ---- lo que la lectura de remisiones deja a mano ----
    //
    // Son campos y no un retorno con seis cosas porque los lee UN solo metodo y
    // los usa UN solo metodo, en la misma peticion. El servicio es Scoped: una
    // peticion, una instancia.
    private int _idCliente;
    private int _idEmpresa;
    private int _idEmpresaMayorista;
    private string _numeroCliente = string.Empty;
    private string _nombreCliente = string.Empty;
    private List<string> _correosDeLasRemisiones = new();
    private readonly Dictionary<int, int> _clientePorRemision = new();
    private readonly Dictionary<int, decimal> _totalPagarPorRemision = new();

    /*
      LAS REMISIONES, EN EL ORDEN EN QUE SE PIDIERON.

      No en el que contesta el procedimiento: las tres listas que viajan al
      timbrado van ALINEADAS POR POSICION —es el contrato del servidor— y la
      persona tiene que ver los renglones en el mismo orden en que los marco. Un
      reordenamiento silencioso aqui significa facturar la remision equivocada
      con el monto de otra.

      Se pide al MISMO servicio que la consulta de ventas, asi que pasa por el
      mismo candado de almacen y de agente: `preparar` no abre ninguna remision
      que el usuario no pudiera ver en la reja.
    */
    private async Task<List<FacturaPreparadaRemision>> LeerRemisionesAsync(
        List<int> ids, int idUsuario, bool puedeVerDeOtros, CancellationToken ct)
    {
        var resp = await _consulta.ConsultarRemisionesAsync(
            new VentasConsultaRequest { IDsVentaLst = ids }, idUsuario, puedeVerDeOtros, ct);

        var porId = new Dictionary<int, Dictionary<string, object?>>();
        foreach (var fila in resp.Rows)
        {
            var id = Reglas.Entero(Reglas.Crudo(fila, "IDVenta"));
            if (id > 0) porId[id] = fila;
        }

        var salida = new List<FacturaPreparadaRemision>();
        var correos = new List<string?>();

        foreach (var id in ids)
        {
            if (!porId.TryGetValue(id, out var fila)) continue;

            var importeZ = Reglas.Numero(Reglas.Crudo(fila, "ImporteZ"));
            var cascos = Reglas.Numero(Reglas.Crudo(fila, "Cascos"));
            var facturado = Reglas.Numero(Reglas.Crudo(fila, "TotalFacturado"));

            salida.Add(new FacturaPreparadaRemision
            {
                IdVenta = id,
                Folio = Reglas.Entero(Reglas.Crudo(fila, "Folio")),
                FolioFtm = Reglas.Texto(Reglas.Crudo(fila, "FolioFtm")),
                ImporteZ = importeZ,
                Cascos = cascos,
                TotalFacturado = facturado,
                PorFacturar = Reglas.PorFacturar(importeZ, cascos, facturado),
                MontoPropuesto = Reglas.MontoQueProponeMac31(importeZ, cascos, facturado),
                Cancelada = Reglas.Texto(Reglas.Crudo(fila, "Cancelada")).Trim()
            });

            _clientePorRemision[id] = Reglas.Entero(Reglas.Crudo(fila, "IDCliente"));
            _totalPagarPorRemision[id] = Reglas.Numero(Reglas.Crudo(fila, "TotalPagar"));

            correos.Add(Reglas.Texto(Reglas.Crudo(fila, "Email")));
            correos.Add(Reglas.Texto(Reglas.Crudo(fila, "Email2")));
            correos.Add(Reglas.Texto(Reglas.Crudo(fila, "Email3")));
            correos.Add(Reglas.Texto(Reglas.Crudo(fila, "Email4")));
            correos.Add(Reglas.Texto(Reglas.Crudo(fila, "Email5")));
        }

        if (salida.Count > 0 && porId.TryGetValue(salida[0].IdVenta, out var primera))
        {
            _idCliente = Reglas.Entero(Reglas.Crudo(primera, "IDCliente"));
            _idEmpresa = Reglas.Entero(Reglas.Crudo(primera, "IDEmpresa"));
            _idEmpresaMayorista = Reglas.Entero(Reglas.Crudo(primera, "IDEmpresaMayorista"));
            _numeroCliente = Reglas.Texto(Reglas.Crudo(primera, "NumeroCliente"));
            _nombreCliente = Reglas.Texto(Reglas.Crudo(primera, "NombreCliente"));
        }

        _correosDeLasRemisiones = Reglas.CorreosDe(correos.ToArray());

        return salida;
    }
}
