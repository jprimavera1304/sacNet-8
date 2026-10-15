using ISL_Service.Application.DTOs.VentasEscrituras;
using ISL_Service.Application.DTOs.VentasFacturacion;

namespace ISL_Service.Application.Interfaces;

/// <summary>Las constantes de facturacion de la empresa.</summary>
public class ConstantesFacturacion
{
    /// <summary>El nombre del ejecutable de Mac31 de esta instalacion.</summary>
    public string AppMac { get; set; } = string.Empty;

    /// <summary>El key de Facturapi CON el nombre del ejecutable embebido.</summary>
    public string FacturapiSecretKey { get; set; } = string.Empty;

    /// <summary>"ZARA" o lo que sea: decide la variante de facturacion.</summary>
    public string Funcionalidad { get; set; } = string.Empty;

    public string UrlVerificaSat { get; set; } = string.Empty;
    public string RfcEmisor { get; set; } = string.Empty;

    /// <summary>El IVA como fraccion (0.16), no como porcentaje.</summary>
    public decimal PorcentajeIva { get; set; }
}

/// <summary>El renglon de una factura ya timbrada, para el complemento y el correo.</summary>
public class FacturaGuardada
{
    public int IdVentaFactura { get; set; }
    public string? IdFacturapi { get; set; }
    public string? Uuid { get; set; }

    /// <summary>Cuantos complementos de pago lleva ya.</summary>
    public int NumeroPagos { get; set; }

    /// <summary>El saldo antes de este pago.</summary>
    public decimal Saldo { get; set; }
}

/// <summary>Los datos del correo y del archivo, del reporte de factura electronica.</summary>
public class CorreoFactura
{
    public string Archivo { get; set; } = string.Empty;
    public string IdFacturapi { get; set; } = string.Empty;
    public string Remitente { get; set; } = string.Empty;
    public string RemitenteNombre { get; set; } = string.Empty;
    public string Smtp { get; set; } = string.Empty;
    public int Puerto { get; set; }
    public string Usuario { get; set; } = string.Empty;
    public string Contrasena { get; set; } = string.Empty;
    public string Asunto { get; set; } = string.Empty;
    public string Cuerpo { get; set; } = string.Empty;
    public string ConCopia { get; set; } = string.Empty;
}

/// <summary>Los procedimientos de facturacion de legacy.</summary>
public interface IVentasFacturacionRepository
{
    /// <summary>
    /// Las constantes de facturacion, leidas de dbo.Constantes con un SELECT y NO
    /// con sp_n_ConsultaConstantes, que es quien hace el cierre del dia.
    /// </summary>
    Task<ConstantesFacturacion> LeerConstantesAsync(CancellationToken ct);

    /// <summary>La llave de Facturapi y el nombre de la aplicacion, nada mas.</summary>
    Task<(string? SecretKey, string? AppMac)> LeerLlaveFacturapiAsync(CancellationToken ct);

    /// <summary>
    /// Guarda las veinte columnas de parametros del CFDI y devuelve el id del
    /// renglon. Es sp_n_ActualizarParametro.
    /// </summary>
    Task<int> GuardarParametrosAsync(
        int idUsuario, string equipo, IReadOnlyList<string> parametros, CancellationToken ct);

    /// <summary>Un insert de factura. Es sp_n_InsertarVentaFactura.</summary>
    Task<ResultadoLegacyDto> InsertarFacturaAsync(
        FacturaTimbrarRequest request,
        FacturaTimbradaDto timbrada,
        int idParametros,
        int idVenta,
        int folio,
        string serie,
        decimal totalFacturadoVenta,
        int tipo,
        DateTime? fechaPago,
        string fechaPagoSat,
        int numComplementosPago,
        int idVentaFacturaRelacionada,
        int idUsuario,
        string equipo,
        CancellationToken ct);

    /// <summary>Registra la cancelacion. Es sp_n_CancelarVentaFactura.</summary>
    Task<ResultadoLegacyDto> CancelarFacturaAsync(
        string idFacturapi, int idStatus, string? motivoSat, string? motivo,
        int statusCancelacion, string? status, string? cancellationStatus,
        DateTime? fechaCancelacionSat, int idUsuario, string equipo, CancellationToken ct);

    /// <summary>Es sp_n_ConsultaVentasFacturas.</summary>
    Task<FilasLegacyDto> ConsultarAsync(FacturaConsultaRequest request, CancellationToken ct);

    /// <summary>
    /// Los datos de una factura timbrada que necesita el complemento de pago:
    /// su UUID, su parcialidad y su saldo. Sale de sp_n_ConsultaVentasFacturas.
    /// </summary>
    Task<FacturaGuardada?> LeerFacturaAsync(string idFacturapi, CancellationToken ct);

    /// <summary>
    /// Los datos del correo y la ruta del PDF. Es sp_n_rptFacturaElectronica, que
    /// recibe un solo parametro con el formato "@&lt;IDVentaFactura&gt;".
    /// </summary>
    Task<CorreoFactura?> LeerDatosCorreoAsync(int idVentaFactura, CancellationToken ct);
}

/// <summary>
/// El PDF o el XML de un comprobante ya timbrado, leido del disco donde lo dejo
/// el proceso de timbrado.
/// </summary>
public class DocumentoDeFactura
{
    public byte[] Contenido { get; set; } = Array.Empty<byte>();

    /// <summary>El nombre con el que se baja: el del archivo en disco.</summary>
    public string NombreArchivo { get; set; } = string.Empty;

    public string TipoContenido { get; set; } = string.Empty;
}

/// <summary>Las reglas de facturacion que no son del procedimiento.</summary>
public interface IVentasFacturacionService
{
    Task<FacturaTimbradaDto> TimbrarAsync(FacturaTimbrarRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<FacturaTimbradaDto> TimbrarComplementoAsync(
        ComplementoPagoTimbrarRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<ResultadoLegacyDto> CancelarAsync(FacturaCancelarRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<FilasLegacyDto> ConsultarAsync(FacturaConsultaRequest request, CancellationToken ct);

    /// <summary>Manda la factura por correo. Devuelve a quien se mando.</summary>
    Task<string> EnviarCorreoAsync(FacturaEnviarCorreoRequest request, CancellationToken ct);

    /// <summary>
    /// El PDF o el XML del comprobante. `formato` es "pdf" o "xml".
    ///
    /// Existe porque NO habia forma de bajar la factura: lo unico que servia el
    /// comprobante era el correo, asi que para releer una factura habia que
    /// mandarsela a alguien. En Mac31 son dos botones (IMPRIMIR PDF F7 y
    /// DESCARGAR XML F8) y los dos salen del mismo sitio: el `Archivo` que
    /// devuelve sp_n_rptFacturaElectronica.
    /// </summary>
    Task<DocumentoDeFactura> LeerDocumentoAsync(int idVentaFactura, string formato, CancellationToken ct);
}
