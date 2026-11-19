namespace ISL_Service.Application.DTOs.AlmacenCascos;

/// <summary>
/// Cabecera de movimiento (salida o entrada). TipoMovimiento: 1=SALIDA, 2=ENTRADA. Estatus: 1=REGISTRADA, 2=ACEPTADA, 3=CANCELADA.
/// </summary>
public class MovimientoCascoDto
{
    public int IdMovimiento { get; set; }

    /// <summary>
    /// El folio COMO LO LEE LA GENTE: `ZARA-SAL-CAM-565`.
    /// </summary>
    /// <remarks>
    /// Solo viene de un centro de servicio, donde los movimientos salen de la
    /// tabla de legacy y ese folio es el que esta impreso en el papel y el que
    /// ensena Mac31. En la matriz es null y la pantalla cae al numero interno,
    /// que es lo que ensenaba antes de que existiera este campo.
    /// </remarks>
    public string? Folio { get; set; }

    public int TipoMovimiento { get; set; }
    public int? IdMovimientoSalida { get; set; }
    public int Estatus { get; set; }
    public int? IdRepartidorEntrega { get; set; }
    public int? IdRepartidorRecibe { get; set; }
    public int? IdTarima { get; set; }
    public int TotalTarimas { get; set; }
    public int TotalPiezas { get; set; }
    public decimal TotalKilos { get; set; }
    public int TotalTarimasConKilos { get; set; }
    public string? NombreTarima { get; set; }
    public string? Observaciones { get; set; }
    public string? MotivoCancelacion { get; set; }
    public string? UsuarioCreacion { get; set; }
    public DateTime? FechaCreacion { get; set; }
    public string? UsuarioCancelacion { get; set; }
    public DateTime? FechaCancelacion { get; set; }
    /// <summary>Nombre del repartidor que entrega (desde catálogo).</summary>
    public string? EntregaNombre { get; set; }
    /// <summary>Nombre del repartidor que recibe (desde catálogo).</summary>
    public string? RecibeNombre { get; set; }
}
