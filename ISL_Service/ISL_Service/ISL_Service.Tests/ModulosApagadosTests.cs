using ISL_Service.Infrastructure.Security;

namespace ISL_Service.Tests;

// Apagar un modulo para una empresa deja de dar sus permisos, sin borrarlos.
// Quien decide que permiso cae es esta regla, y tiene una trampa: comparar por
// "empieza con" haria que apagar un modulo se llevara a sus vecinos de nombre.
public class ModulosApagadosTests
{
    private static HashSet<string> Apagados(params string[] claves) =>
        new(claves, StringComparer.OrdinalIgnoreCase);

    [Fact]
    public void Un_permiso_del_modulo_apagado_no_cuenta()
    {
        Assert.True(PermissionService.EsDeModuloApagado("reportes.ver", Apagados("reportes")));
        Assert.True(PermissionService.EsDeModuloApagado("reportes.ventas.folios.ver", Apagados("reportes")));
    }

    [Fact]
    public void Apagar_un_modulo_no_se_lleva_a_los_que_empiezan_igual()
    {
        // Si esto se comparara con "empieza con", apagar `ventas` dejaria sin
        // permisos a `ventas_moviles`, que es otro modulo.
        Assert.False(PermissionService.EsDeModuloApagado("ventas_moviles.ver", Apagados("ventas")));
        Assert.False(PermissionService.EsDeModuloApagado("reportesx.ver", Apagados("reportes")));
    }

    [Fact]
    public void Los_modulos_prendidos_siguen_dando_sus_permisos()
    {
        Assert.False(PermissionService.EsDeModuloApagado("usuarios.crear", Apagados("reportes")));
        Assert.False(PermissionService.EsDeModuloApagado("usuarios.crear", Apagados()));
    }

    [Fact]
    public void Una_clave_sin_modulo_se_deja_pasar()
    {
        // No se sabe de que modulo es; quitarla seria adivinar.
        Assert.False(PermissionService.EsDeModuloApagado("inicio", Apagados("inicio")));
        Assert.False(PermissionService.EsDeModuloApagado("", Apagados("inicio")));
        Assert.False(PermissionService.EsDeModuloApagado(".ver", Apagados("inicio")));
    }

    [Fact]
    public void Las_mayusculas_no_cambian_nada()
    {
        Assert.True(PermissionService.EsDeModuloApagado("REPORTES.ver", Apagados("reportes")));
        Assert.True(PermissionService.EsDeModuloApagado("reportes.ver", Apagados("REPORTES")));
    }
}
