using ISL_Service.Infrastructure.Repositories;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// El acceso a datos de Multi pago. Todo son procedimientos de Mac31: ni uno
/// nuevo, ni uno tocado. El porque de cada uno esta en la implementacion.
/// </summary>
public interface IVentasMultiPagoRepository
{
    /// <summary>Constantes de la empresa (Funcionalidad, precios, tolerancias).</summary>
    Task<MultiPagoConstantes> ConstantesAsync(CancellationToken ct);

    /// <summary>Las remisiones seleccionadas, por sp_n_ConsultaVentas.</summary>
    Task<List<MultiPagoRemisionCruda>> RemisionesAsync(IEnumerable<int> idsVenta, CancellationToken ct);

    /// <summary>Catalogo de bancos activos (sp_n_ConsultaBancos).</summary>
    Task<List<(int Id, string Nombre)>> BancosAsync(CancellationToken ct);

    /// <summary>Catalogo de repartidores activos (sp_n_ConsultaRepartidores).</summary>
    Task<List<(int Id, string Nombre)>> RepartidoresAsync(CancellationToken ct);

    /// <summary>La contrasena de autorizacion propia del usuario, si la tiene.</summary>
    Task<(int Aplica, string Contrasena)> ContrasenaDeUsuarioAsync(int idUsuario, CancellationToken ct);

    /// <summary>Deja constancia del intento de clave, acertado o no.</summary>
    Task RegistrarIntentoDeClaveAsync(string forma, bool correcto, string clave, int idUsuario, string equipo, CancellationToken ct);

    /// <summary>Un abono a UNA remision (sp_n_InsertarVentasPagos).</summary>
    Task<MultiPagoResultadoSp> InsertarPagoAsync(MultiPagoParametrosSp p, CancellationToken ct);
}
