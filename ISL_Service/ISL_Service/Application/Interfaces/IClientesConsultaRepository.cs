using ISL_Service.Application.DTOs.ClientesConsulta;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// La consulta del padron de clientes (pantalla ConsultarClientes de Mac31).
/// Solo lectura: llama a sp_n_ConsultaClientes, que no se toca.
/// </summary>
public interface IClientesConsultaRepository
{
    /// <summary>
    /// Devuelve los clientes que le toca ver a <paramref name="idUsuario"/>.
    ///
    /// El filtro por agente NO viene en el filtro: se resuelve aqui dentro a
    /// partir del usuario (ver FiltroDeAgente). Por eso el idUsuario es un
    /// parametro aparte y obligatorio.
    /// </summary>
    Task<List<ClienteConsultaDto>> ConsultarAsync(ClientesConsultaFiltro filtro, int idUsuario, CancellationToken ct);
}

/// <summary>
/// Los filtros de la pantalla ConsultarClientes.
///
/// OJO CON LO QUE NO ESTA AQUI: no hay IDAgente del lado del que pide. El agente
/// por el que se filtra lo decide el backend a partir del usuario del token, no
/// el que llama. Lo unico que se acepta es <see cref="IDAgenteAcotar"/>, y solo
/// sirve para que quien ve a TODOS pueda acotar; nunca para ampliar.
/// </summary>
public class ClientesConsultaFiltro
{
    public int IDCliente { get; set; }
    public string? Numero { get; set; }
    public string? Nombre { get; set; }
    public string? ApellidoPaterno { get; set; }
    public string? ApellidoMaterno { get; set; }
    public string? Rfc { get; set; }
    public string? Referencia { get; set; }

    /// <summary>
    /// 0 = todos, 1 = activos, 2 = inactivos. El default del endpoint es 1,
    /// igual que el combo ESTATUS de Mac31, que arranca en ACTIVOS.
    /// </summary>
    public int IDStatus { get; set; } = 1;

    /// <summary>0 = todos, 1 = si factura, 2 = no factura.</summary>
    public int IDTipoCliente { get; set; }

    /// <summary>0 = todos, 1 = con huella, 2 = sin huella.</summary>
    public int ConHuella { get; set; }

    /// <summary>
    /// Empresa de centro de servicio. Decide a que base apunta el SP: la de
    /// Zaragoza, la de Luro, o la local si no coincide con ninguna.
    ///
    /// NULO = NO LO MANDARON, y entonces lo resuelve el repositorio desde
    /// dbo.Constantes.IDEmpresaCS, que es de donde lo saca Mac31. No es lo mismo
    /// que 0: sp_n_ConsultaClientes le pega SIEMPRE
    /// ' AND C.IDEmpresaCS = @IDEmpresaCS' a su SQL, y en MacZ los 887 clientes
    /// tienen IDEmpresaCS = 1, asi que un 0 cableado devolvia CERO clientes sobre
    /// un padron de 887. El 0 explicito se sigue respetando por si alguien lo
    /// quiere a proposito. Ver EmpresaCsPorOmision.
    /// </summary>
    public int? IDEmpresaCS { get; set; }

    /// <summary>0 = todos, 1 = solo los de mostrador.</summary>
    public int EsMostrador { get; set; }

    /// <summary>true = igualdad (@Identico = 1); false = LIKE.</summary>
    public bool Exacto { get; set; }

    /// <summary>
    /// Acotar a UN agente. Solo se obedece cuando el filtro resuelto del usuario
    /// es 0 (ve a todos). Si el usuario esta amarrado a un agente, este campo se
    /// IGNORA: no es una forma de pedir otro agente. Ver ClientesConsultaRepository.
    /// </summary>
    public int IDAgenteAcotar { get; set; }
}
