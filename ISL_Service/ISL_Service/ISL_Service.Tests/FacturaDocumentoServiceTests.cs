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
  EL REPORTE 1003: GENERAR EL PDF Y EL XML DEL CFDI

  QUE SE PRUEBA AQUI Y QUE SE PROBO CONTRA LA BASE
  ------------------------------------------------
  El camino feliz —bajar de Facturapi y dejar el archivo en disco— se midio con
  comprobantes REALES de las dos bases locales el 2026-10-15, porque simularlo
  seria probar a HttpClient:

    Tauro  (Produccion_svr) 41764 -> C:\mac21\Facturas\10-2026\Factura 141035 41764 .pdf
                                     (99 673 b) + .xml (5 048 b), UUID
                                     FB91806B-7B76-4F4C-A503-58DFC5887305
    Zara   (MacZ)           80660 -> C:\macZ\Facturas\10-2026\Factura 0 80660 .pdf
    Zara   (MacZ)           80575 -> ComplementoPago 0 80575 CANCELADA.pdf (cancelado)

  Lo que SI es logica pura y vive aqui son las decisiones que no se pueden
  provocar con esta base: que el procedimiento SIEMPRE devuelve renglon (asi que
  "no existe" y "no timbrada" hay que distinguirlos a mano), que no haya llave
  de Facturapi, y que no se pueda ESCRIBIR en la carpeta —que en produccion es lo
  normal, porque la ruta es un disco de la oficina y la API corre en el
  servidor—.
*/
public class FacturaDocumentoServiceTests
{
    private const string RutaPdf = @"C:\mac21\Facturas\10-2026\Factura 141035 41764 .pdf";
    private const string RutaCancelada = @"C:\macZ\Facturas\10-2026\ComplementoPago 0 80575 CANCELADA.pdf";

    private static FacturaDocumentoService Servicio(
        CorreoFactura? correo,
        AlmacenDeMentira? almacen = null,
        string? secretKey = null,
        string? appMac = null)
    {
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var facturapi = new FacturapiCliente(new FabricaDeClientes(), configuracion);

        return new FacturaDocumentoService(
            new RepositorioDeMentira(correo, secretKey, appMac),
            facturapi,
            almacen ?? new AlmacenDeMentira(),
            NullLogger<FacturaDocumentoService>.Instance);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task Sin_factura_no_hay_nada_que_generar(int id)
    {
        await Assert.ThrowsAsync<ArgumentException>(
            () => Servicio(null).GenerarAsync(id, false, CancellationToken.None));
    }

    [Fact]
    public async Task Un_id_que_no_existe_es_un_404_y_no_un_409()
    {
        /*
          `sp_n_rptFacturaElectronica` devuelve renglon SIEMPRE: los nueve campos
          del correo salen de `Constantes`, no de la factura. Medido con
          '@99999999' en Produccion_svr: IssueDate, FolioFtm, Archivo e
          IDFacturapi vienen NULL y el correo de la empresa viene completo.

          Sin esta distincion, pedir un id inventado contestaba "no esta
          timbrado", que manda a mirar el SAT cuando lo que pasa es que ese
          comprobante no existe.
        */
        var fallo = await Assert.ThrowsAsync<NotFoundException>(
            () => Servicio(new CorreoFactura { Archivo = "", IdFacturapi = "" })
                .GenerarAsync(99999999, false, CancellationToken.None));

        Assert.Contains("99999999", fallo.Message);
    }

    [Fact]
    public async Task Una_factura_sin_timbrar_lo_dice_con_esas_palabras()
    {
        // Hay ruta (la factura existe) pero no hay folio de Facturapi: nunca se
        // timbro, asi que no tiene UUID ni CFDI y no hay PDF que pedir.
        var fallo = await Assert.ThrowsAsync<ConflictException>(
            () => Servicio(new CorreoFactura { Archivo = RutaPdf, IdFacturapi = "" })
                .GenerarAsync(41764, false, CancellationToken.None));

        Assert.Contains("no esta timbrado", fallo.Message);
        Assert.Contains("UUID", fallo.Message);
    }

    [Fact]
    public async Task Timbrada_pero_sin_ruta_senala_a_Constantes()
    {
        /* El unico modo de quedarse sin ruta teniendo CFDI es que PathMac este
           vacio en esa base; decirlo ahorra buscar el archivo en un disco que
           nunca se nombro. */
        var fallo = await Assert.ThrowsAsync<ConflictException>(
            () => Servicio(new CorreoFactura { Archivo = "", IdFacturapi = "6ac1" })
                .GenerarAsync(41764, false, CancellationToken.None));

        Assert.Contains("PathMac", fallo.Message);
    }

    [Fact]
    public async Task Si_los_archivos_ya_estan_no_se_baja_nada_ni_se_lee_la_llave()
    {
        /*
          ES IDEMPOTENTE, y eso es lo que permite llamarlo antes de CADA correo
          sin pagar dos descargas por cada reenvio.

          QUE NO SE TOCA LA RED SE COMPRUEBA SIN MIRAR LA RED: el repositorio de
          esta prueba no tiene llave de Facturapi, asi que si el servicio
          intentara bajar algo esto reventaria con ConflictException. Que termine
          -y que el almacen no haya escrito nada- es la prueba.
        */
        var almacen = new AlmacenDeMentira(RutaPdf, RutaPdf.Replace(".pdf", ".xml"));

        var r = await Servicio(new CorreoFactura { Archivo = RutaPdf, IdFacturapi = "6ac1" }, almacen)
            .GenerarAsync(41764, false, CancellationToken.None);

        Assert.Equal(2, r.Archivos.Count);
        Assert.All(r.Archivos, a => Assert.Equal("disco", a.Origen));
        Assert.All(r.Archivos, a => Assert.True(a.EnDisco));
        Assert.Empty(almacen.Guardados);
    }

    [Fact]
    public async Task El_xml_es_el_mismo_nombre_con_otra_extension()
    {
        var almacen = new AlmacenDeMentira(RutaPdf, RutaPdf.Replace(".pdf", ".xml"));

        var r = await Servicio(new CorreoFactura { Archivo = RutaPdf, IdFacturapi = "6ac1" }, almacen)
            .GenerarAsync(41764, false, CancellationToken.None);

        Assert.Equal(RutaPdf, r.Archivos.Single(x => x.Formato == "pdf").Ruta);
        Assert.EndsWith(@"Factura 141035 41764 .xml", r.Archivos.Single(x => x.Formato == "xml").Ruta);
    }

    [Fact]
    public async Task Un_comprobante_cancelado_se_reconoce_por_el_nombre_del_archivo()
    {
        /*
          El procedimiento no devuelve el estado: lo mete en la RUTA
          (CASE WHEN IDStatus = 2 THEN 'CANCELADA'). Importa porque al cancelar
          cambia el nombre y el PDF anterior se queda huerfano —las dos parejas
          conviven en C:\mac21\Facturas\11-2025 de esta maquina—, asi que quien
          cancela tiene que volver a generar.
        */
        var almacen = new AlmacenDeMentira(RutaCancelada, RutaCancelada.Replace(".pdf", ".xml"));

        var r = await Servicio(new CorreoFactura { Archivo = RutaCancelada, IdFacturapi = "6ac0" }, almacen)
            .GenerarAsync(80575, false, CancellationToken.None);

        Assert.True(r.Cancelada);
    }

    [Fact]
    public async Task Un_comprobante_vigente_no_se_marca_cancelado()
    {
        var almacen = new AlmacenDeMentira(RutaPdf, RutaPdf.Replace(".pdf", ".xml"));

        var r = await Servicio(new CorreoFactura { Archivo = RutaPdf, IdFacturapi = "6ac1" }, almacen)
            .GenerarAsync(41764, false, CancellationToken.None);

        Assert.False(r.Cancelada);
    }

    [Fact]
    public async Task Sin_llave_de_Facturapi_y_sin_archivo_se_dice_que_falta_la_llave()
    {
        /*
          El mensaje tiene que hablar de la LLAVE y no del archivo: si dice solo
          "no se encontro el archivo", quien lo lea se va a revisar carpetas
          cuando lo que falta es la configuracion de la empresa.
        */
        var fallo = await Assert.ThrowsAsync<ConflictException>(
            () => Servicio(new CorreoFactura { Archivo = RutaPdf, IdFacturapi = "6ac1" })
                .GenerarAsync(41764, false, CancellationToken.None));

        Assert.Contains("llave de Facturapi", fallo.Message);
    }

    [Fact]
    public async Task La_llave_que_no_lleva_el_AppMac_no_sirve()
    {
        /* Mismo candado que al timbrar (ReglasFacturacion.SecretoReal): el key
           guardado tiene que contener el nombre de la aplicacion de la empresa.
           Una copia de la base de otra empresa no puede bajar sus comprobantes. */
        var fallo = await Assert.ThrowsAsync<ConflictException>(
            () => Servicio(
                    new CorreoFactura { Archivo = RutaPdf, IdFacturapi = "6ac1" },
                    secretKey: "sk_test_sinmarca", appMac: "MacT.exe")
                .GenerarAsync(41764, false, CancellationToken.None));

        Assert.Contains("llave de Facturapi", fallo.Message);
    }

    [Fact]
    public async Task Leer_valida_el_formato_igual_que_la_descarga()
    {
        foreach (var formato in new[] { "", "zip", "docx" })
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => Servicio(null).LeerAsync(1, formato, CancellationToken.None));
        }
    }

    [Fact]
    public async Task Leer_acepta_el_formato_con_mayusculas_y_espacios()
    {
        /* " PDF " viene de una direccion escrita a mano. El fallo tiene que ser
           de CONFLICTO (no hay llave) y no de ARGUMENTO: asi se ve que el
           formato se acepto. */
        await Assert.ThrowsAsync<ConflictException>(
            () => Servicio(new CorreoFactura { Archivo = RutaPdf, IdFacturapi = "6ac1" })
                .LeerAsync(41764, " PDF ", CancellationToken.None));
    }

    [Fact]
    public async Task Leer_del_disco_devuelve_el_nombre_del_archivo()
    {
        var almacen = new AlmacenDeMentira(RutaPdf);

        var doc = await Servicio(new CorreoFactura { Archivo = RutaPdf, IdFacturapi = "6ac1" }, almacen)
            .LeerAsync(41764, "pdf", CancellationToken.None);

        Assert.Equal("Factura 141035 41764 .pdf", doc.NombreArchivo);
        Assert.Equal("application/pdf", doc.TipoContenido);
        Assert.NotEmpty(doc.Contenido);
    }

    /* ---------------------------------------------------------------- */

    /// <summary>Un disco de mentira: sabe que archivos "existen" y apunta lo que se guarda.</summary>
    private sealed class AlmacenDeMentira : IFacturaDocumentoAlmacen
    {
        private readonly HashSet<string> _existentes;

        public AlmacenDeMentira(params string[] existentes)
            => _existentes = new HashSet<string>(existentes, StringComparer.OrdinalIgnoreCase);

        public List<string> Guardados { get; } = new();

        public bool Existe(string ruta) => _existentes.Contains(ruta);

        public Task<byte[]> LeerAsync(string ruta, CancellationToken ct)
            => Task.FromResult(new byte[] { 0x25, 0x50, 0x44, 0x46 }); // "%PDF"

        public Task<string?> GuardarAsync(string ruta, byte[] contenido, CancellationToken ct)
        {
            Guardados.Add(ruta);
            return Task.FromResult<string?>(null);
        }
    }

    /// <summary>Solo contesta la lectura del 1003 y la llave.</summary>
    private sealed class RepositorioDeMentira : IVentasFacturacionRepository
    {
        private readonly CorreoFactura? _correo;
        private readonly string? _secretKey;
        private readonly string? _appMac;

        public RepositorioDeMentira(CorreoFactura? correo, string? secretKey, string? appMac)
        {
            _correo = correo;
            _secretKey = secretKey;
            _appMac = appMac;
        }

        public Task<CorreoFactura?> LeerDatosCorreoAsync(int idVentaFactura, CancellationToken ct)
            => Task.FromResult(_correo);

        public Task<(string? SecretKey, string? AppMac)> LeerLlaveFacturapiAsync(CancellationToken ct)
            => Task.FromResult((_secretKey, _appMac));

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

    /// <summary>No se usa: ninguna de estas pruebas llega a la red.</summary>
    private sealed class FabricaDeClientes : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new();
    }
}
