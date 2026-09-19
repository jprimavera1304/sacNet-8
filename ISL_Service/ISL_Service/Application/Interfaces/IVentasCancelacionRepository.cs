using ISL_Service.Application.DTOs.VentasCancelacion;

namespace ISL_Service.Application.Interfaces;

/// Los datos crudos de una remision que legacy mira ANTES de dejar cancelar.
/// Salen de la tabla Ventas, no del renglon de la pantalla: entre que se pinta
/// la tabla y se aprieta el boton, otro pudo pagar o facturar la misma.
public class VentaParaCancelar
{
    public int IdVenta { get; set; }
    public int Folio { get; set; }
    public string FolioFtm { get; set; } = string.Empty;
    public int IdEmpresa { get; set; }
    public int IdTipoDocumento { get; set; }
    public string NombreCliente { get; set; } = string.Empty;
    public bool Cancelada { get; set; }
    public bool Pagada { get; set; }
    public decimal TotalPagar { get; set; }
    public decimal Abonos { get; set; }
    public decimal TotalFacturado { get; set; }
}

/// Constantes: son las mismas dos variables globales que Mac31 lee al arrancar
/// (Variables.funcionalidad y Variables.EsCentroServicio) mas la fecha de
/// operacion, con la que se arma la contrasena de supervisor.
public class ConstantesCancelacion
{
    public string Funcionalidad { get; set; } = string.Empty;
    public int EsCentroServicio { get; set; }
    public DateTime FechaOperacion { get; set; }
}

/// El concentrado abierto al que sigue colgada una remision, si lo hay.
public class ConcentradoDeVenta
{
    public int IdConcentrado { get; set; }
    public int FolioConcentrado { get; set; }
}

public interface IVentasCancelacionRepository
{
    Task<ConstantesCancelacion> ConsultarConstantesAsync(CancellationToken ct);
    Task<List<VentaParaCancelar>> ConsultarVentasAsync(IReadOnlyCollection<int> idsVenta, CancellationToken ct);

    /// null cuando no esta en ningun concentrado, o el que lo tiene ya esta cancelado.
    Task<ConcentradoDeVenta?> ConsultarConcentradoAbiertoAsync(int folioVenta, CancellationToken ct);

    /// La contrasena de autorizacion propia del usuario, cuando la tiene dada de alta.
    Task<string?> ConsultarContrasenaAutorizacionAsync(int idUsuario, CancellationToken ct);

    /// Deja constancia del intento de contrasena, acierte o no. Igual que Mac31.
    Task RegistrarIntentoContrasenaAsync(string forma, bool correcto, string contrasena, int idUsuario, string equipo, CancellationToken ct);

    /// Saca el folio del concentrado para que la cancelacion no lo deje colgado.
    Task QuitarDelConcentradoAsync(VentaParaCancelar venta, ConcentradoDeVenta concentrado, int idUsuario, string equipo, CancellationToken ct);

    Task<VentasCancelacionResultado> CancelarAsync(VentaParaCancelar venta, int idUsuario, string equipo, CancellationToken ct);
}
