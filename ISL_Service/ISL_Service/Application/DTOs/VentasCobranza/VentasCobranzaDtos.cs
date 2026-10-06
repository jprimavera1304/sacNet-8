namespace ISL_Service.Application.DTOs.VentasCobranza;

/*
  LAS CONSULTAS DE LA COBRANZA DE MAC31 (COBROS, COBRANZAS Y CONCENTRADOS)

  Son las lecturas que MacServicios2 servia para las tres pantallas de la
  cobranza: Cobranza (el paquete de notas que se le entrega a un repartidor),
  Concentrado (el corte de ruta) y Cobro (el dinero y los cascos que vuelven).

  Solo se migro la parte que de verdad LEE. Las pantallas completas de
  "consultar cobros" y "consultar cobranzas" NO estan aqui: su primer paso en
  legacy es `sp_n_ConsultaConstantes`, que no es una consulta — cuando la fecha
  de operacion esta atrasada hace el cierre del dia (borra y rehace
  AlmacenProductoHist y adelanta Constantes.FechaOperacion). Esta documentado en
  `migracion/CUIDADO-consultas-que-escriben.md` y por eso esas capacidades
  quedaron fuera de esta ola.
*/

/// <summary>
/// "¿Puedo meter este folio de venta a esta cobranza?" — la pregunta que Mac31
/// hace al teclear un folio en la pantalla de Cobranza.
/// </summary>
public class CobranzaFolioRequest
{
    /// La empresa del folio. Obligatoria: el folio por si solo no es unico,
    /// se repite entre empresas (asi lo valida tambien legacy).
    public int IDEmpresaVenta { get; set; }

    /// El folio de la VENTA que se quiere agregar. Es texto porque en legacy
    /// llega como texto y el procedimiento lo convierte a entero adentro.
    public string? FolioVenta { get; set; }

    /// La cobranza que se esta modificando, 0 si es nueva. Si el folio ya
    /// pertenece a ESTA misma cobranza, el procedimiento deja de quejarse.
    public int IDCobranzaActual { get; set; }

    /// Tipo de documento: entra en la funcion que arma el prefijo del folio
    /// (`fn_DevuelveQueryFolio`). 0 = sin distinguir.
    public int IDTipoDocumento { get; set; }
}

/// <summary>
/// La misma pregunta, pero para el Concentrado.
/// </summary>
public class ConcentradoFolioRequest
{
    public int IDEmpresaVenta { get; set; }
    public string? FolioVenta { get; set; }

    /// El concentrado que se esta modificando, 0 si es nuevo.
    public int IDConcentradoActual { get; set; }
    public int IDTipoDocumento { get; set; }
}

/// <summary>
/// Los folios y los cascos de un concentrado, una cobranza o un cobro.
/// </summary>
/// <remarks>
/// Es el `sp_n_ConcentradoFoliosUsados`, que en legacy servia la pestaña
/// "detalle" de la pantalla de Cobros. Los tres ids son alternativos: se manda
/// el que se tenga.
/// </remarks>
public class CobroFoliosUsadosRequest
{
    public int IDConcentrado { get; set; }
    public int IDCobranza { get; set; }
    public int IDCobro { get; set; }

    /// true = solo la parte del concentrado. Legacy manda 1/0; aqui es booleano
    /// porque es lo que es, y el repositorio lo traduce.
    public bool SoloConcentrado { get; set; }

    /// true = solo la parte de la cobranza.
    public bool SoloCobranza { get; set; }
}

/// <summary>
/// Los pagos que se aplicaron dentro de un concentrado, cobranza o cobro.
/// </summary>
public class CobroPagosRequest
{
    public int IDConcentrado { get; set; }
    public int IDCobranza { get; set; }
    public int IDCobro { get; set; }

    /// El interruptor de presentacion de Mac31 (0 = pantalla). Se expone porque
    /// el procedimiento cambia las columnas que devuelve segun su valor.
    public int Formato { get; set; }
}

/// <summary>
/// Los pagos por kilo: el dinero que entra pesando los cascos en vez de
/// contandolos.
/// </summary>
/// <remarks>
/// Los cuatro ids son filtros alternativos y el rango de fechas es sobre la
/// fecha del pago. Es el filtro de `sp_n_ConsultarVentasPagosKilos`.
/// </remarks>
public class PagosKilosRequest
{
    public int IDVentaPagoKilo { get; set; }
    public int IDCobro { get; set; }
    public int IDVenta { get; set; }
    public int IDCliente { get; set; }

    /// Desde. Se acepta ISO ("2026-02-01") o con diagonales ("01/02/2026").
    public string? FechaInicial { get; set; }

    /// Hasta, inclusive: el dia completo hasta las 23:59:59.
    public string? FechaFinal { get; set; }
}
