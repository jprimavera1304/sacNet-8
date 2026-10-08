using ISL_Service.Application.DTOs.ClientesConsulta;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/// <summary>
/// Normaliza los filtros del padron de clientes.
///
/// EL APOSTROFO SE QUITA AQUI, como en el resto de los catalogos:
/// sp_n_ConsultaClientes arma su WHERE concatenando @Numero, @Nombre,
/// @ApellidoPaterno, @ApellidoMaterno, @RFC y @Referencia y lo corre con
/// EXEC(@Sql). Pasarlos como parametros de SqlCommand NO protege: el parametro
/// entra limpio al SP y el SP lo concatena adentro. El SP es `sp_n_` y no se
/// toca, asi que se cierra la puerta del lado que si es nuestro.
///
/// LO QUE ESTE SERVICIO NO HACE: resolver el agente. Eso vive en el repositorio
/// porque necesita la conexion y el cache, y porque es el unico lugar donde
/// puede garantizarse que ningun camino lo salte.
/// </summary>
public class ClientesConsultaService : IClientesConsultaService
{
    private readonly IClientesConsultaRepository _repository;

    public ClientesConsultaService(IClientesConsultaRepository repository)
    {
        _repository = repository;
    }

    private static string Texto(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return string.Empty;
        return valor.Replace("'", string.Empty).Trim();
    }

    public Task<List<ClienteConsultaDto>> ConsultarAsync(
        ClientesConsultaFiltro filtro, int idUsuario, CancellationToken ct)
        => _repository.ConsultarAsync(new ClientesConsultaFiltro
        {
            IDCliente = Math.Max(filtro.IDCliente, 0),
            Numero = Texto(filtro.Numero),
            Nombre = Texto(filtro.Nombre),
            ApellidoPaterno = Texto(filtro.ApellidoPaterno),
            ApellidoMaterno = Texto(filtro.ApellidoMaterno),
            Rfc = Texto(filtro.Rfc),
            Referencia = Texto(filtro.Referencia),
            IDStatus = filtro.IDStatus,
            IDTipoCliente = filtro.IDTipoCliente,
            ConHuella = filtro.ConHuella,
            /*
              IDEmpresaCS VIAJA TAL CUAL, INCLUIDO EL NULO.
              Nulo significa "no lo mandaron" y lo resuelve el repositorio desde
              dbo.Constantes (EmpresaCsPorOmision), que es quien tiene la conexion
              y el cache. Aqui NO se normaliza a 0: ese 0 era justamente el que
              dejaba GET api/clientes en {"total":0} sobre los 887 clientes de
              MacZ, porque el padron entero tiene IDEmpresaCS = 1.
            */
            IDEmpresaCS = filtro.IDEmpresaCS,
            EsMostrador = filtro.EsMostrador,
            Exacto = filtro.Exacto,
            IDAgenteAcotar = Math.Max(filtro.IDAgenteAcotar, 0)
        }, idUsuario, ct);
}
