namespace ISL_Service.Application.DTOs.VentasDescuentos;

/// <summary>
/// El renglonaje de un descuento: que porcentaje aplica a que
/// grupo-categoria / categoria / marca.
/// </summary>
/// <remarks>
/// Es el filtro de `sp_n_ConsultaDescuentosDetalle`. Los tres primeros son
/// listas porque la pantalla de Mac31 permite marcar varias casillas a la vez;
/// `IDVenta` sirve para traer el descuento tal como quedo aplicado en una
/// remision concreta.
/// </remarks>
public class DescuentoDetalleRequest
{
    public List<int> IDsMarca { get; set; } = new();
    public List<int> IDsCategoria { get; set; } = new();
    public List<int> IDsGrupoCategoria { get; set; } = new();

    /// Un renglon concreto del descuento.
    public int IDDescuentoDetalle { get; set; }

    /// El descuento (la cabecera).
    public int IDDescuento { get; set; }

    /// La remision, para ver el descuento como se aplico ahi.
    public int IDVenta { get; set; }
}

/// <summary>
/// La bitacora de cambios de un descuento: quien movio que porcentaje, cuando
/// y desde que equipo.
/// </summary>
public class DescuentoDetalleLogRequest
{
    public int IDDescuento { get; set; }
    public int IDCategoria { get; set; }
    public int IDMarca { get; set; }
}
