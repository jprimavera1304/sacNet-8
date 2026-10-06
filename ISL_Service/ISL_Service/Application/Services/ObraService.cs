using ISL_Service.Application.DTOs.Obra;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/// <summary>
/// Normaliza los filtros de obra antes de llegar al SP.
///
/// EL APOSTROFO SE QUITA AQUI, Y NO ES PARANOIA
/// --------------------------------------------
/// Los `sp_n_Consultar_MC*` arman el WHERE concatenando el filtro de texto y lo
/// corren con EXEC(@Sql). Mandarlo como SqlParameter no protege: la inyeccion
/// pasa DENTRO del procedimiento. Legacy quitaba el apostrofo solo en las ramas
/// de alta y modificacion (MCGerenteService.cs:60) y se le olvidaba en las de
/// consulta — las 12 que se migran aqui llegaban al EXEC con el texto crudo.
/// Es el mismo arreglo que ya se hizo en los catalogos generales.
///
/// Se quita en vez de escapar duplicando (`''`) porque el SP lo mete en un
/// LIKE y una comilla duplicada ahi tampoco buscaria lo que el usuario escribio:
/// el unico apellido con apostrofo que hay en estas tablas es ninguno, y
/// callarlo es preferible a un 500.
/// </summary>
public class ObraService : IObraService
{
    private readonly IObraRepository _repository;

    public ObraService(IObraRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Deja el texto como lo espera el SP: sin apostrofo, recortado, y el vacio
    /// convertido a "" porque es el valor con el que estos SP entienden "sin
    /// filtro" (comparan `IF @X <> ''`, no `IS NULL`).
    /// </summary>
    private static string Texto(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return string.Empty;
        return valor.Replace("'", string.Empty).Trim();
    }

    private static ObraCatalogoFiltro Normalizar(ObraCatalogoFiltro filtro) => new()
    {
        Id = filtro.Id,
        IDStatus = filtro.IDStatus,
        Texto = Texto(filtro.Texto),
        Exacto = filtro.Exacto
    };

    /// <summary>
    /// Un booleano opcional al 0/1 que el SP entiende. Sin valor va 0, que en
    /// estos SP significa "no filtres" (comparan `<> 0`): un `false` explicito
    /// NO puede pedir "los que no lo son", ni aqui ni en Mac31.
    /// </summary>
    private static int Bandera(bool? valor) => valor == true ? 1 : 0;

    public Task<List<ObraGerenteDto>> ConsultarGerentesAsync(ObraCatalogoFiltro filtro, int idTipoGerente, CancellationToken ct)
        => _repository.ConsultarGerentesAsync(Normalizar(filtro), idTipoGerente, ct);

    public Task<List<ObraPuestoDto>> ConsultarPuestosAsync(ObraCatalogoFiltro filtro, bool? esRepresentanteLegal, bool? esRepresentanteTecnico, CancellationToken ct)
        => _repository.ConsultarPuestosAsync(Normalizar(filtro), Bandera(esRepresentanteLegal), Bandera(esRepresentanteTecnico), ct);

    public Task<List<ObraResidenteDto>> ConsultarResidentesAsync(ObraCatalogoFiltro filtro, int idPuesto, bool? esSupervision, bool? esObra, CancellationToken ct)
        => _repository.ConsultarResidentesAsync(Normalizar(filtro), idPuesto, Bandera(esSupervision), Bandera(esObra), ct);

    public Task<List<ObraTipoDto>> ConsultarTipoAsync(ObraTipoCatalogo tipo, ObraCatalogoFiltro filtro, CancellationToken ct)
        => _repository.ConsultarTipoAsync(tipo, Normalizar(filtro), ct);

    public Task<List<ObraUnidadMedidaDto>> ConsultarUnidadesMedidaAsync(ObraCatalogoFiltro filtro, CancellationToken ct)
        => _repository.ConsultarUnidadesMedidaAsync(Normalizar(filtro), ct);

    public Task<List<ObraProveedorDto>> ConsultarProveedoresAsync(ObraProveedorFiltro filtro, CancellationToken ct)
        => _repository.ConsultarProveedoresAsync(new ObraProveedorFiltro
        {
            IDProveedor = filtro.IDProveedor,
            IDStatus = filtro.IDStatus,
            Numero = Texto(filtro.Numero),
            Nombre = Texto(filtro.Nombre),
            ApellidoPaterno = Texto(filtro.ApellidoPaterno),
            ApellidoMaterno = Texto(filtro.ApellidoMaterno),
            Rfc = Texto(filtro.Rfc),
            Exacto = filtro.Exacto
        }, ct);

    public Task<List<ObraContratoGrupoDto>> ConsultarContratoGruposAsync(int idContratoGrupo, int idContrato, ObraCatalogoFiltro filtro, CancellationToken ct)
        => _repository.ConsultarContratoGruposAsync(idContratoGrupo, idContrato, Normalizar(filtro), ct);

    public Task<List<ObraContratoConceptoDto>> ConsultarContratoConceptosAsync(int idContratoConcepto, int idContratoGrupo, int idContrato, ObraCatalogoFiltro filtro, CancellationToken ct)
        => _repository.ConsultarContratoConceptosAsync(idContratoConcepto, idContratoGrupo, idContrato, Normalizar(filtro), ct);

    public Task<List<ObraContratoEstimacionDto>> ConsultarContratoEstimacionesAsync(int idContratoEstimacion, int idContrato, ObraCatalogoFiltro filtro, CancellationToken ct)
        => _repository.ConsultarContratoEstimacionesAsync(idContratoEstimacion, idContrato, Normalizar(filtro), ct);
}
