using ISL_Service.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace ISL_Service.Tests;

/// <summary>
/// Las once escrituras de Almacen de Cascos, Temporadas y Torneos estaban solo
/// con [Authorize] de clase: cualquier token, de cualquier rol, podia crear,
/// modificar y CANCELAR movimientos de casco, y abrir, cerrar y cancelar torneos
/// y temporadas.
///
/// Esta prueba fija cual es el permiso de cada una. Si alguien agrega una
/// escritura y se olvida del permiso, o le cambia la clave por una que el front
/// no pide, se cae aqui y no en produccion.
///
/// Las claves no se inventaron: son las que ya pide
/// Web/QR_Web/docs/shared/js/core/module-action-flags.js.
/// </summary>
public class PermisosFinosDeEscrituraTests
{
    [Theory]
    // AlmacenCascos: 6 escrituras
    [InlineData(typeof(AlmacenCascosController), "CrearSalida", "perm:almacen_cascos.crear")]
    [InlineData(typeof(AlmacenCascosController), "AceptarEntrada", "perm:almacen_cascos.crear")]
    [InlineData(typeof(AlmacenCascosController), "GuardarKilosTarima", "perm:almacen_cascos.editar")]
    [InlineData(typeof(AlmacenCascosController), "ActualizarSalida", "perm:almacen_cascos.editar")]
    [InlineData(typeof(AlmacenCascosController), "ActualizarEntrada", "perm:almacen_cascos.editar")]
    [InlineData(typeof(AlmacenCascosController), "Cancelar", "perm:almacen_cascos.cancelar")]
    // Temporadas: 4 escrituras + la puerta nueva del cierre
    [InlineData(typeof(TemporadasController), "Create", "perm:temporadas.crear")]
    [InlineData(typeof(TemporadasController), "Update", "perm:temporadas.editar")]
    [InlineData(typeof(TemporadasController), "Cancel", "perm:temporadas.cancelar")]
    [InlineData(typeof(TemporadasController), "Reactivar", "perm:temporadas.cancelar")]
    [InlineData(typeof(TemporadasController), "CerrarVencidas", "perm:temporadas.cancelar")]
    // Torneos: 7 escrituras
    [InlineData(typeof(TorneosController), "Create", "perm:torneos.crear")]
    [InlineData(typeof(TorneosController), "Update", "perm:torneos.editar")]
    [InlineData(typeof(TorneosController), "Cancel", "perm:torneos.cancelar")]
    [InlineData(typeof(TorneosController), "Activar", "perm:torneos.cancelar")]
    [InlineData(typeof(TorneosController), "Cerrar", "perm:torneos.cancelar")]
    [InlineData(typeof(TorneosController), "Reactivar", "perm:torneos.cancelar")]
    [InlineData(typeof(TorneosController), "CerrarVencidos", "perm:torneos.cancelar")]
    public void CadaEscrituraPideSuPermiso(Type controlador, string metodo, string politica)
    {
        var accion = controlador.GetMethod(metodo, BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(accion);

        var politicas = accion!
            .GetCustomAttributes<AuthorizeAttribute>(inherit: false)
            .Select(x => x.Policy)
            .ToList();

        Assert.Contains(politica, politicas);
    }

    /// <summary>
    /// Ninguna escritura de esos tres controladores se queda sin permiso. Cuenta
    /// los metodos con verbo de escritura y exige que TODOS lleven politica: asi
    /// una accion nueva no puede colarse sin candado.
    /// </summary>
    [Theory]
    [InlineData(typeof(AlmacenCascosController))]
    [InlineData(typeof(TemporadasController))]
    [InlineData(typeof(TorneosController))]
    public void NingunaEscrituraSeQuedaSinPermiso(Type controlador)
    {
        var sinPermiso = controlador
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<HttpPostAttribute>() is not null
                     || m.GetCustomAttribute<HttpPutAttribute>() is not null
                     || m.GetCustomAttribute<HttpDeleteAttribute>() is not null
                     || m.GetCustomAttribute<HttpPatchAttribute>() is not null)
            .Where(m => !m.GetCustomAttributes<AuthorizeAttribute>(inherit: false).Any())
            .Select(m => m.Name)
            .ToList();

        Assert.Empty(sinPermiso);
    }
}
