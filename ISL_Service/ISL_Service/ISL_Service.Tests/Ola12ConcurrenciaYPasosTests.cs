using System.Reflection;
using ISL_Service.Application.DTOs.VentasCancelacion;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Services;
using ISL_Service.Infrastructure.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ISL_Service.Tests;

/*
  OLA 12: LOS DOS USUARIOS QUE SE PISAN, Y LAS ESCRITURAS DE VARIOS PASOS.

  Hallazgos 3 y 4 de la auditoria de cierre:

    - 3. Varios sitios recuperaban el id de lo que acababan de insertar con un
         `SELECT MAX(...)`. En reportes, la llave del MAX() era la pareja
         (NombreEquipo, IDUsuario) y el backend mandaba "WEB" para todos: dos
         personas pidiendo un reporte a la vez y una abria el PDF con los filtros
         de la otra. Comprobado en Produccion_svr: TODOS los renglones de
         Parametros dicen NombreEquipo='WEB', IDUsuario=0.
    - 4. La cancelacion sacaba la remision del concentrado y luego la cancelaba,
         sin transaccion y sin mirar el resultado: si el segundo paso fallaba, la
         remision quedaba viva, por cobrar y FUERA de la lista del cobrador. Y el
         bucle seguia con las demas.
*/
public class Ola12ConcurrenciaYPasosTests
{
    // ==================================================================
    // 3. La llave del MAX() ya no es la misma para todos
    // ==================================================================

    [Fact]
    public void Dos_Llamadas_Nunca_Comparten_El_Nombre_De_Equipo()
    {
        var uno = EquipoUnico("WEB");
        var otro = EquipoUnico("WEB");

        Assert.NotEqual(uno, otro);
    }

    /// <summary>
    /// Mil llamadas, mil nombres distintos: es lo que hace que el MAX() del
    /// procedimiento se calcule sobre un conjunto de un solo renglon.
    /// </summary>
    [Fact]
    public void Mil_Llamadas_Dan_Mil_Nombres_Distintos()
    {
        var nombres = Enumerable.Range(0, 1000).Select(_ => EquipoUnico("WEB")).ToHashSet();

        Assert.Equal(1000, nombres.Count);
    }

    /// <summary>
    /// Se conserva el nombre original al principio, para que el renglon siga
    /// siendo reconocible en la tabla Parametros al depurar.
    /// </summary>
    [Fact]
    public void El_Nombre_Original_Sigue_Delante()
    {
        Assert.StartsWith("WEB-", EquipoUnico("WEB"));
        Assert.StartsWith("JUAN-", EquipoUnico("JUAN"));
    }

    /// <summary>
    /// Tiene que caber: Parametros.NombreEquipo es varchar(1000) y el #ParamIds
    /// de los reportes la declara varchar(400). Un nombre largo se recorta.
    /// </summary>
    [Fact]
    public void Nunca_Se_Pasa_De_Lo_Que_Cabe_En_El_Reporte()
    {
        var largo = EquipoUnico(new string('X', 500));

        Assert.True(largo.Length <= 400, $"NombreEquipo de {largo.Length} caracteres no cabe en varchar(400).");
    }

    [Fact]
    public void Sin_Nombre_Se_Usa_WEB()
    {
        Assert.StartsWith("WEB-", EquipoUnico(""));
        Assert.StartsWith("WEB-", EquipoUnico("   "));
    }

    // ==================================================================
    // 4. La cancelacion para y avisa en vez de dejar dinero suelto
    // ==================================================================

    [Fact]
    public async Task Si_Sale_Del_Concentrado_Y_No_Se_Cancela_Se_Para_Y_Se_Avisa()
    {
        var repo = new CancelacionFalsa(cancelaDeVerdad: false, enConcentrado: true);
        var servicio = new VentasCancelacionService(repo, NullLogger<VentasCancelacionService>.Instance);

        var error = await Assert.ThrowsAsync<ConflictException>(() => servicio.CancelarAsync(
            new VentasCancelacionRequest { IdsVenta = new List<int> { 1, 2, 3 }, Contrasena = "LA-BUENA" },
            idUsuario: 7, equipo: "JUAN", CancellationToken.None));

        Assert.Contains("SALIÓ del concentrado", error.Message);
        Assert.Contains("sigue viva y por cobrar", error.Message);

        // Y NO siguio con las otras dos: una sola salida del concentrado, un solo
        // intento de cancelar.
        Assert.Equal(1, repo.VecesQuitadoDelConcentrado);
        Assert.Equal(1, repo.VecesCancelado);
    }

    /// <summary>
    /// Si la venta NO estaba en ningun concentrado, una cancelacion que no
    /// prospera no deja nada colgando: se reporta como fallida y se sigue, igual
    /// que antes. El corte es solo para el caso que deja dinero suelto.
    /// </summary>
    [Fact]
    public async Task Sin_Concentrado_Una_Cancelacion_Fallida_No_Corta_El_Lote()
    {
        var repo = new CancelacionFalsa(cancelaDeVerdad: false, enConcentrado: false);
        var servicio = new VentasCancelacionService(repo, NullLogger<VentasCancelacionService>.Instance);

        var respuesta = await servicio.CancelarAsync(
            new VentasCancelacionRequest { IdsVenta = new List<int> { 1, 2, 3 } },
            idUsuario: 7, equipo: "JUAN", CancellationToken.None);

        Assert.Equal(3, respuesta.Fallidas);
        Assert.Equal(0, respuesta.Canceladas);
        Assert.Equal(3, repo.VecesCancelado);
        Assert.Equal(0, repo.VecesQuitadoDelConcentrado);
    }

    [Fact]
    public async Task Cuando_Todo_Sale_Bien_Se_Cancela_Todo_El_Lote()
    {
        var repo = new CancelacionFalsa(cancelaDeVerdad: true, enConcentrado: true);
        var servicio = new VentasCancelacionService(repo, NullLogger<VentasCancelacionService>.Instance);

        var respuesta = await servicio.CancelarAsync(
            new VentasCancelacionRequest { IdsVenta = new List<int> { 1, 2, 3 }, Contrasena = "LA-BUENA" },
            idUsuario: 7, equipo: "JUAN", CancellationToken.None);

        Assert.Equal(3, respuesta.Canceladas);
        Assert.Equal(0, respuesta.Fallidas);
        Assert.Equal(3, repo.VecesQuitadoDelConcentrado);
    }

    // ==================================================================
    // Andamiaje
    // ==================================================================

    private static string EquipoUnico(string nombre)
    {
        var metodo = typeof(ReportesVentasRepository)
            .GetMethod("EquipoUnicoPorLlamada", BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Falta EquipoUnicoPorLlamada en ReportesVentasRepository.");

        return (string)metodo.Invoke(null, new object?[] { nombre })!;
    }

    private sealed class CancelacionFalsa : IVentasCancelacionRepository
    {
        private readonly bool _cancelaDeVerdad;
        private readonly bool _enConcentrado;

        public CancelacionFalsa(bool cancelaDeVerdad, bool enConcentrado)
        {
            _cancelaDeVerdad = cancelaDeVerdad;
            _enConcentrado = enConcentrado;
        }

        public int VecesQuitadoDelConcentrado { get; private set; }
        public int VecesCancelado { get; private set; }

        public Task<ConstantesCancelacion> ConsultarConstantesAsync(CancellationToken ct)
            => Task.FromResult(new ConstantesCancelacion
            {
                Funcionalidad = "TAU",  // lo del concentrado solo lo mira Mac31 en TAU
                EsCentroServicio = 0,
                FechaOperacion = new DateTime(2026, 10, 7)
            });

        public Task<List<VentaParaCancelar>> ConsultarVentasAsync(IReadOnlyCollection<int> idsVenta, CancellationToken ct)
            => Task.FromResult(idsVenta.Select(id => new VentaParaCancelar
            {
                IdVenta = id,
                Folio = 1000 + id,
                FolioFtm = $"R-{1000 + id}",
                IdEmpresa = 1,
                IdTipoDocumento = 1,
                NombreCliente = "CLIENTE",
                Cancelada = false,
                Pagada = false,
                TotalPagar = 100m,
                Abonos = 0m,
                TotalFacturado = 0m
            }).ToList());

        public Task<ConcentradoDeVenta?> ConsultarConcentradoAbiertoAsync(int folioVenta, CancellationToken ct)
            => Task.FromResult(_enConcentrado
                ? new ConcentradoDeVenta { IdConcentrado = 55, FolioConcentrado = 900 }
                : null);

        /// La contrasena propia del usuario: asi la prueba no depende del reloj.
        public Task<string?> ConsultarContrasenaAutorizacionAsync(int idUsuario, CancellationToken ct)
            => Task.FromResult<string?>("LA-BUENA");

        public Task RegistrarIntentoContrasenaAsync(string forma, bool correcto, string contrasena, int idUsuario, string equipo, CancellationToken ct)
            => Task.CompletedTask;

        public Task QuitarDelConcentradoAsync(VentaParaCancelar venta, ConcentradoDeVenta concentrado, int idUsuario, string equipo, CancellationToken ct)
        {
            VecesQuitadoDelConcentrado++;
            return Task.CompletedTask;
        }

        public Task<VentasCancelacionResultado> CancelarAsync(VentaParaCancelar venta, int idUsuario, string equipo, CancellationToken ct)
        {
            VecesCancelado++;
            return Task.FromResult(new VentasCancelacionResultado
            {
                IdVenta = venta.IdVenta,
                Folio = venta.FolioFtm,
                Cancelada = _cancelaDeVerdad,
                Mensaje = _cancelaDeVerdad ? string.Empty : "NO HAY USADOS EN INVENTARIO."
            });
        }
    }
}
