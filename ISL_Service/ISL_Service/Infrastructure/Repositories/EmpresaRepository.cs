using Microsoft.EntityFrameworkCore;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Infrastructure.Repositories;

public class EmpresaRepository : IEmpresaRepository
{
    private readonly AppDbContext _db;

    public EmpresaRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<int>> ListEmpresaIdsAsync(CancellationToken ct)
    {
        // No depende de tabla Empresas, sale de Usuarios para que compile sí o sí.
        return await _db.Usuarios.AsNoTracking()
            .Select(u => u.EmpresaId)
            .Distinct()
            .OrderBy(x => x)
            .ToListAsync(ct);
    }
}
