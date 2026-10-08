using ISL_Service.Application.DTOs.ProveedoresConsulta;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/// <summary>
/// Normaliza los filtros del padron de proveedores.
///
/// El apostrofo se quita por la razon de siempre: sp_n_ConsultaProveedores arma
/// su WHERE concatenando @Numero, @Nombre, @ApellidoPaterno, @ApellidoMaterno y
/// @RFC y lo corre con EXEC(@Sql). Pasarlos como parametros de SqlCommand no
/// protege, porque el SP los concatena adentro.
/// </summary>
public class ProveedoresConsultaService : IProveedoresConsultaService
{
    private readonly IProveedoresConsultaRepository _repository;

    public ProveedoresConsultaService(IProveedoresConsultaRepository repository)
    {
        _repository = repository;
    }

    private static string Texto(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return string.Empty;
        return valor.Replace("'", string.Empty).Trim();
    }

    public Task<List<ProveedorConsultaDto>> ConsultarAsync(ProveedoresConsultaFiltro filtro, CancellationToken ct)
        => _repository.ConsultarAsync(new ProveedoresConsultaFiltro
        {
            IDProveedor = Math.Max(filtro.IDProveedor, 0),
            Numero = Texto(filtro.Numero),
            Nombre = Texto(filtro.Nombre),
            ApellidoPaterno = Texto(filtro.ApellidoPaterno),
            ApellidoMaterno = Texto(filtro.ApellidoMaterno),
            Rfc = Texto(filtro.Rfc),
            IDStatus = filtro.IDStatus,
            IDCondicionesCredito = Math.Max(filtro.IDCondicionesCredito, 0),
            IDDescuento = Math.Max(filtro.IDDescuento, 0),
            Exacto = filtro.Exacto
        }, ct);
}
