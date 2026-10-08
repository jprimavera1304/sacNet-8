using ISL_Service.Application.DTOs.InventarioTransito;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// La CONSULTA del inventario en transito (la pantalla InventarioTransito de
/// Mac31). El procesado vive aparte, en ComprasController +
/// OperacionMacRepository, y es una escritura con su propio permiso.
/// </summary>
public interface IInventarioTransitoConsultaRepository
{
    Task<List<InventarioTransitoDto>> ConsultarAsync(InventarioTransitoFiltro filtro, CancellationToken ct);
}

/// <summary>Los filtros de la pantalla de inventario en transito.</summary>
public class InventarioTransitoFiltro
{
    /// <summary>
    /// 0 = todos, 1 = en transito (default, como el combo de Mac31, que arranca
    /// en EN TRASITO y viene deshabilitado), 2 = aceptado.
    /// </summary>
    public int StatusInventario { get; set; } = 1;

    /// <summary>
    /// Centro de servicio (es un cliente). 0 = el procedimiento usa
    /// Constantes.IDClienteCS y, si tampoco hay, todos los centros de servicio.
    /// </summary>
    public int IDCliente { get; set; }

    /// <summary>
    /// OBLIGATORIA, y no por gusto: con la fecha inicial VACIA el procedimiento
    /// deja de consultar y empieza a ESCRIBIR. Ver el comentario del repositorio.
    /// </summary>
    public DateTime FechaEmisionInicial { get; set; }

    public DateTime FechaEmisionFinal { get; set; }

    /// <summary>true = un renglon por remision; false = un renglon por pieza.</summary>
    public bool Agrupado { get; set; }
}
