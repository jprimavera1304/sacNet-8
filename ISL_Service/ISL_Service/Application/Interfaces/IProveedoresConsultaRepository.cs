using ISL_Service.Application.DTOs.ProveedoresConsulta;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// La consulta del padron de proveedores. Solo lectura: llama a
/// sp_n_ConsultaProveedores, que no se toca.
/// </summary>
public interface IProveedoresConsultaRepository
{
    Task<List<ProveedorConsultaDto>> ConsultarAsync(ProveedoresConsultaFiltro filtro, CancellationToken ct);
}

/// <summary>Los filtros de la pantalla de consulta de proveedores.</summary>
public class ProveedoresConsultaFiltro
{
    public int IDProveedor { get; set; }
    public string? Numero { get; set; }
    public string? Nombre { get; set; }
    public string? ApellidoPaterno { get; set; }
    public string? ApellidoMaterno { get; set; }
    public string? Rfc { get; set; }

    /// <summary>
    /// 0 = todos, 1 = activos (default del endpoint, como el combo de Mac31),
    /// 2 = inactivos. Entra a fn_RegresaTablaStatus.
    /// </summary>
    public int IDStatus { get; set; } = 1;

    public int IDCondicionesCredito { get; set; }

    /// <summary>
    /// SE ACEPTA PERO EL PROCEDIMIENTO NO LO USA. Ver el comentario del
    /// repositorio: sp_n_ConsultaProveedores declara @IDDescuento y nunca lo mete
    /// en su WHERE.
    /// </summary>
    public int IDDescuento { get; set; }

    /// <summary>true = igualdad (@Identico = 1); false = LIKE.</summary>
    public bool Exacto { get; set; }
}
