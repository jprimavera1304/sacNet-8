namespace ISL_Service.Application.DTOs.Productos;

/// <summary>
/// Un producto del catalogo, con su precio de lista y sus existencias.
///
/// NO ES UN DESPLEGABLE, y de ahi que tenga su propio permiso: trae precio,
/// precio anterior, diferencia y stock. Quien puede ver un combo de ciudades no
/// tiene por que ver el margen.
///
/// LO QUE NO SE HEREDA DE LEGACY:
///
/// * Los `*Ftm`. `unidadMedidaFtm` ("12 PZAS / CAJA") y `ordenFtm` los armaba la
///   base para las rejillas de WinForms; con `numPiezas` el front lo escribe en
///   el idioma del que mira.
/// * `MesAno`. El SP devuelve el nombre del mes y el ano calculados con
///   GETDATE() DENTRO de la base, iguales en todos los renglones. Es el
///   encabezado de la lista de precios impresa, no un dato del producto, y
///   ademas seria la fecha del servidor de base y no la del usuario.
/// * `UltimaModificacion` ("usuario - fecha - equipo" en una sola cadena). Los
///   tres pedazos ya viajan por separado.
/// * `CodigoLista` y `PrecioListaConIvaRnd`: ver las notas de abajo.
/// </summary>
public class ProductoDto
{
    public int IDGrupoCategoria { get; set; }
    public int IDCategoria { get; set; }
    public int IDMarca { get; set; }
    public int IDProducto { get; set; }
    /// <summary>
    /// Id del producto en Facturama. Es TEXTO aunque se llame "ID": la columna
    /// es varchar y trae un token (y cadena vacia en los productos que no se han
    /// dado de alta alla). Declararlo int reventaba la consulta completa con
    /// "The input string '' was not in a correct format" — o sea, el catalogo
    /// entero por un campo que casi nadie mira.
    /// </summary>
    public string? IDFacturama { get; set; }
    public int IDTipoUsado { get; set; }
    public int IDStatus { get; set; }

    public string GrupoCategoria { get; set; } = string.Empty;
    public string Categoria { get; set; } = string.Empty;
    public string Marca { get; set; } = string.Empty;
    public string Clave { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    /// <summary>
    /// Descripcion para el CFDI. Legacy la perdia: su DTO no declaraba esta
    /// columna, asi que lo que se timbra no se podia revisar desde el web.
    /// </summary>
    public string? DescripcionSat { get; set; }
    public string? ClaveSat { get; set; }
    public string? UnidadMedidaSat { get; set; }

    public int IDUnidadMedida { get; set; }
    public string? UnidadMedida { get; set; }
    /// <summary>Piezas por caja. 1 = se vende por pieza.</summary>
    public int NumPiezas { get; set; }

    public decimal PrecioLista { get; set; }
    public decimal PrecioLista2 { get; set; }
    public decimal PrecioListaCS { get; set; }
    public decimal PrecioListaAnterior { get; set; }
    /// <summary>PrecioLista - PrecioListaAnterior, como lo calcula el SP.</summary>
    public decimal Diferencia { get; set; }
    /// <summary>
    /// Precio de lista con IVA. El nombre de la columna en la base dice "Rnd"
    /// pero el SP NO redondea: multiplica por (1 + Constantes.IVA/100) y ya. Se
    /// publica con el nombre de lo que es.
    /// </summary>
    public decimal PrecioListaConIva { get; set; }
    /// <summary>
    /// Precio con los descuentos de la lista Roshfrans ya aplicados en cascada.
    /// Sale en 0 salvo que se pida `listaRoshfrans=true` con sus porcentajes.
    /// </summary>
    public decimal PrecioRoshfransConIva { get; set; }

    public decimal PesoKg { get; set; }
    public decimal Litros { get; set; }
    public decimal Stock { get; set; }
    public decimal StockMinimo { get; set; }
    public decimal StockMaximo { get; set; }

    public string? TipoUsado { get; set; }
    public string? TipoUsadoCorto { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Observaciones { get; set; }
    public int EsServicio { get; set; }
    public int EsCarga { get; set; }
    public int MostrarLista { get; set; }
    public string? CodigoLista { get; set; }

    public int IDUsuarioModificacion { get; set; }
    public string? UsuarioModificacion { get; set; }
    public DateTime? FechaModificacion { get; set; }
    /// <summary>
    /// Legacy tambien perdia esta: su DTO no la declaraba, asi que no se podia
    /// saber desde que maquina se cambio un precio.
    /// </summary>
    public string? EquipoModificacion { get; set; }
}
