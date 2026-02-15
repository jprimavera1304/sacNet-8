using Microsoft.EntityFrameworkCore;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Infrastructure.Repositories;

public class EmpresaRepository : IEmpresaRepository
{
    private const int EMPRESA_ID_FIJA = 1;
    private readonly AppDbContext _db;

    public EmpresaRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<int>> ListEmpresaIdsAsync(CancellationToken ct)
    {
        // No depende de tabla Empresas, sale de Usuarios para que compile si o si.
        return await _db.Usuarios.AsNoTracking()
            .Select(u => u.EmpresaId)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(ct);
    }

    public async Task<string> GetCompanyKeyAsync(CancellationToken ct)
    {
        var clave = await _db.EmpresasWeb.AsNoTracking()
            .Where(e => e.Id == EMPRESA_ID_FIJA)
            .Select(e => e.Clave)
            .SingleOrDefaultAsync(ct);

        var normalized = (clave ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InvalidOperationException(
                "Configuracion invalida: dbo.EmpresaWeb(Id=1).Clave es obligatoria y no puede estar vacia.");

        return normalized;
    }
}
