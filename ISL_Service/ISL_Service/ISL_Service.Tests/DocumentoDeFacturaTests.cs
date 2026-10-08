using ISL_Service.Application.DTOs.VentasEscrituras;
using ISL_Service.Application.DTOs.VentasFacturacion;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Services;
using ISL_Service.Infrastructure.Facturacion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace ISL_Service.Tests;

/*
  BAJAR EL PDF O EL XML DE UN COMPROBANTE

  Son los dos botones de Mac31 (IMPRIMIR PDF F7 y DESCARGAR XML F8) que no tenian
  endpoint: lo UNICO que servia un comprobante timbrado era el correo, asi que
  para releer una factura habia que mandarsela a alguien.

  QUE SE PRUEBA Y QUE NO
  ----------------------
  Lo que decide a que archivo se va —validar la entrada y elegir la extension—
  es logica pura y se prueba aqui. El caso feliz NO se puede probar contra esta
  base: el `Archivo` que devuelve `sp_n_rptFacturaElectronica` se arma con el
  folio de la REMISION en el momento de leerlo, y en la copia local ese folio ya
  no coincide con el de los archivos que hay en C:\macZ\Facturas. Medido con tres
  facturas (31819, 48767, 55519): el procedimiento pide
  "Factura 98987 31819 .pdf" y en disco esta "Factura 98809 31819 .pdf".

  Por eso la prueba que mas importa aqui es la del archivo que NO esta: que
  conteste diciendo QUE ruta busco, en vez de dormir cuatro segundos como legacy
  y despues fallar igual sin decir donde.
*/
public class DocumentoDeFacturaTests
{
    private static VentasFacturacionService Servicio(CorreoFactura? correo)
    {
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var facturapi = new FacturapiCliente(new FabricaDeClientes(), configuracion);
        return new VentasFacturacionService(
            new RepositorioDeMentira(correo), facturapi, NullLogger<VentasFacturacionService>.Instance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Sin_factura_no_hay_documento(int idVentaFactura)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => Servicio(null).LeerDocumentoAsync(idVentaFactura, "pdf", CancellationToken.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData("zip")]
    [InlineData("docx")]
    public async Task Solo_pdf_y_xml(string formato)
    {
        /*
          El .zip existe en disco —es lo que guarda el reporte— y aun asi no se
          sirve: dentro van el PDF y el XML juntos y eso no es lo que pidio
          ninguno de los dos botones. Aceptar cualquier extension devolveria un
          archivo que no es el que se pidio.
        */
        await Assert.ThrowsAsync<ArgumentException>(
            () => Servicio(null).LeerDocumentoAsync(1, formato, CancellationToken.None));
    }

    [Fact]
    public async Task Si_la_factura_no_existe_se_dice_que_no_existe()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => Servicio(null).LeerDocumentoAsync(99, "xml", CancellationToken.None));
    }

    [Fact]
    public async Task El_formato_no_distingue_mayusculas_ni_espacios()
    {
        /*
          " PDF " viene de una direccion escrita a mano y no es motivo de error.
          Con el archivo vacio el fallo tiene que ser de CONFLICTO —no hay
          archivo— y no de ARGUMENTO: asi se comprueba que el formato se acepto.
        */
        await Assert.ThrowsAsync<ConflictException>(
            () => Servicio(new CorreoFactura { Archivo = "" })
                .LeerDocumentoAsync(1, " PDF ", CancellationToken.None));
    }

    [Fact]
    public async Task El_archivo_que_falta_se_contesta_CON_SU_RUTA()
    {
        const string ruta = @"C:\macZ\Facturas\03-2025\Factura 98987 31819 .pdf";

        var fallo = await Assert.ThrowsAsync<ConflictException>(
            () => Servicio(new CorreoFactura { Archivo = ruta })
                .LeerDocumentoAsync(31819, "pdf", CancellationToken.None));

        Assert.Contains(ruta, fallo.Message);
    }

    [Fact]
    public async Task El_xml_es_el_mismo_nombre_con_otra_extension()
    {
        /*
          El repositorio ya entrega el `Archivo` como .pdf (cambia la extension
          del .zip que guarda el reporte). El XML no se pide aparte: es el mismo
          nombre. Si esa regla se escribiera en dos sitios, el correo y la
          descarga acabarian mirando archivos distintos.
        */
        var fallo = await Assert.ThrowsAsync<ConflictException>(
            () => Servicio(new CorreoFactura { Archivo = @"C:\macZ\Facturas\03-2025\Factura 98987 31819 .pdf" })
                .LeerDocumentoAsync(31819, "xml", CancellationToken.None));

        Assert.Contains(".xml", fallo.Message);
        Assert.DoesNotContain(".pdf", fallo.Message);
    }

    /// <summary>Lo unico que contesta es la lectura del reporte.</summary>
    private sealed class RepositorioDeMentira : IVentasFacturacionRepository
    {
        private readonly CorreoFactura? _correo;
        public RepositorioDeMentira(CorreoFactura? correo) => _correo = correo;

        public Task<CorreoFactura?> LeerDatosCorreoAsync(int idVentaFactura, CancellationToken ct)
            => Task.FromResult(_correo);

        public Task<ConstantesFacturacion> LeerConstantesAsync(CancellationToken ct)
            => throw new NotSupportedException();

        public Task<int> GuardarParametrosAsync(
            int idUsuario, string equipo, IReadOnlyList<string> parametros, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<ResultadoLegacyDto> InsertarFacturaAsync(
            FacturaTimbrarRequest request, FacturaTimbradaDto timbrada, int idParametros, int idVenta,
            int folio, string serie, decimal totalFacturadoVenta, int tipo, DateTime? fechaPago,
            string fechaPagoSat, int numComplementosPago, int idVentaFacturaRelacionada,
            int idUsuario, string equipo, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<ResultadoLegacyDto> CancelarFacturaAsync(
            string idFacturapi, int idStatus, string? motivoSat, string? motivo, int statusCancelacion,
            string? status, string? cancellationStatus, DateTime? fechaCancelacionSat,
            int idUsuario, string equipo, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<FilasLegacyDto> ConsultarAsync(FacturaConsultaRequest request, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<FacturaGuardada?> LeerFacturaAsync(string idFacturapi, CancellationToken ct)
            => throw new NotSupportedException();
    }

    /// <summary>No se llega a usar: ninguna de estas pruebas timbra nada.</summary>
    private sealed class FabricaDeClientes : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
