using ISL_Service.Application.DTOs.Recaudacion;
using ISL_Service.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace ISL_Service.Tests;

/// <summary>
/// El endpoint publico del QR de recaudaciones.
///
/// Es el UNICO metodo anonimo del controlador y entrega datos sin que nadie se
/// identifique, asi que las dos cosas que lo sostienen —que el resto del
/// controlador este cerrado, y que lo que sale sea lo minimo— se prueban aqui en
/// vez de confiar en que nadie las deshaga sin darse cuenta.
/// </summary>
public class RecaudacionQrPublicoTests
{
    /// <summary>
    /// La clase lleva [Authorize]: cualquier metodo que se agregue manana nace
    /// cerrado. Si alguien lo quita, esta prueba lo dice.
    /// </summary>
    [Fact]
    public void ElControladorEstaCerradoPorClase()
    {
        var authorize = typeof(RecaudacionesController)
            .GetCustomAttributes<AuthorizeAttribute>(inherit: false)
            .ToList();

        Assert.Single(authorize);
    }

    /// <summary>
    /// Y solo UN metodo esta abierto: el del QR. Si aparece un segundo
    /// [AllowAnonymous] es que se abrio algo mas.
    /// </summary>
    [Fact]
    public void SoloElDelQrEsAnonimo()
    {
        var anonimos = typeof(RecaudacionesController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<AllowAnonymousAttribute>() is not null)
            .Select(m => m.Name)
            .ToList();

        Assert.Equal(new[] { "Get" }, anonimos);
    }

    /// <summary>
    /// Los cuatro metodos de plantilla de Visual Studio (Get sin argumentos,
    /// Post, Put y Delete, que devolvian 200 sin hacer nada y estaban publicados
    /// sin candado) no vuelven.
    /// </summary>
    [Fact]
    public void NoHayMetodosDePlantilla()
    {
        var acciones = typeof(RecaudacionesController)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName)
            .ToList();

        var accion = Assert.Single(acciones);
        Assert.Equal("Get", accion.Name);
        Assert.Equal(typeof(IActionResult), accion.ReturnType);

        var parametro = Assert.Single(accion.GetParameters());
        Assert.Equal("qr", parametro.Name);

        Assert.Empty(typeof(RecaudacionesController).GetCustomAttributes<HttpPostAttribute>(false));
        Assert.Empty(typeof(RecaudacionesController).GetCustomAttributes<HttpPutAttribute>(false));
        Assert.Empty(typeof(RecaudacionesController).GetCustomAttributes<HttpDeleteAttribute>(false));
    }

    /// <summary>
    /// LO QUE NUNCA DEBE SALIR POR UN ENDPOINT ANONIMO.
    ///
    /// DV1 y DV2 son los digitos verificadores con los que se arma el propio
    /// codigo QR: devolverlos a quien solo escaneo un ticket es regalarle el
    /// material para fabricar otros. Lo demas son ids internos, el plan de
    /// cuentas y datos de pago que el papel no lleva.
    /// </summary>
    [Fact]
    public void ElDtoPublicoNoLlevaNadaSensible()
    {
        var campos = typeof(RecaudacionPublicaDTO)
            .GetProperties()
            .Select(x => x.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var prohibido in new[]
        {
            "DV1", "DV2",
            "IDRecaudacion", "IDCaja", "IDUsuario", "IDOperador", "IDCuenta",
            "IDConcepto", "IDEconomico", "IDPermisionario", "IDEconomicoAuxiliar",
            "NumCuenta", "NumCuentaFtm", "NombreCuenta", "ImporteCuenta",
            "Observaciones", "TipoPago", "MontoTransferencia", "FolioLLanta",
            "html", "ImportePagado", "IDRecaudacionCorte", "FechaCorte",
            "IDUsuarioCancelacion"
        })
        {
            Assert.False(campos.Contains(prohibido), $"El DTO publico del QR no debe llevar '{prohibido}'.");
        }
    }

    /// <summary>
    /// Y lo que la pantalla publica SI pinta tiene que seguir saliendo, o el
    /// ticket se ve en blanco. La lista son los campos que lee
    /// Web/QR_Web/docs/modules/custom/aviacion/qr/js/main.js.
    /// </summary>
    [Fact]
    public void ElDtoPublicoLlevaLoQuePintaLaPantalla()
    {
        var origen = new RecaudacionDTO
        {
            IDStatus = 2,
            FolioFtm = "A-000123",
            FechaFtm = "01/02/2026 10:00",
            Importe = 100m,
            ImporteTotal = 116m,
            NombreCuentaDetalle = "DERECHO DE PISO",
            NombreEconomico = "UNIDAD 7",
            NumEconomicoFtm = "0007",
            NombrePermisionario = "PERMISIONARIO X",
            Usuario = "CAJERA1",
            Equipo = "CAJA-01",
            NumNomina = 55,
            NumNominaFtm = "0055",
            OperadorNombreCompleto = "JUAN PEREZ LOPEZ",
            Cancelada = "01/02/2026 - 11:00 - SUPERVISOR - CAJA-01",

            // Esto NO debe aparecer en la salida.
            DV1 = "9999",
            DV2 = "8888",
            IDRecaudacion = 4242,
            Observaciones = "nota interna"
        };

        var publico = RecaudacionPublicaDTO.Desde(origen);

        Assert.Equal(2, publico.IDStatus);
        Assert.Equal("A-000123", publico.FolioFtm);
        Assert.Equal("01/02/2026 10:00", publico.FechaFtm);
        Assert.Equal(116m, publico.ImporteTotal);
        Assert.Equal("DERECHO DE PISO", publico.NombreCuentaDetalle);
        Assert.Equal("UNIDAD 7", publico.NombreEconomico);
        Assert.Equal("0007", publico.NumEconomicoFtm);
        Assert.Equal("PERMISIONARIO X", publico.NombrePermisionario);
        Assert.Equal("CAJERA1", publico.Usuario);
        Assert.Equal("0055", publico.NumNominaFtm);
        Assert.Equal("JUAN PEREZ LOPEZ", publico.OperadorNombreCompleto);
        Assert.Equal("01/02/2026 - 11:00 - SUPERVISOR - CAJA-01", publico.Cancelada);
    }
}
