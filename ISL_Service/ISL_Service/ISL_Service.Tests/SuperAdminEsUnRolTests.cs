using System.Security.Claims;
using ISL_Service.Application.Security;
using Xunit;

namespace ISL_Service.Tests;

/*
  Ser SuperAdmin tiene que depender del ROL y de nada mas.

  Hubo un `|| GetEmpresaId(user) == 5` que convertia en administrador a
  cualquiera cuya empresa fuera la 5. Estaba inerte porque ninguna base tiene
  esa empresa, asi que no habia forma de notarlo hasta el dia en que una la
  tuviera. Esta prueba lo fija para que no vuelva.
*/
public class SuperAdminEsUnRolTests
{
    private static ClaimsPrincipal Usuario(string rol, int empresaId)
        => new(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Role, rol),
            new Claim("empresaId", empresaId.ToString())
        }, "pruebas"));

    [Theory]
    [InlineData(1)]
    [InlineData(5)]   // la que abria el portillo
    [InlineData(99)]
    public void El_Rol_SuperAdmin_Manda_Sea_Cual_Sea_La_Empresa(int empresaId)
    {
        Assert.True(CurrentUser.IsSuperAdmin(Usuario("SuperAdmin", empresaId)));
    }

    [Theory]
    [InlineData("User")]
    [InlineData("Admin")]
    [InlineData("EMPLEADOS")]
    [InlineData("")]
    public void Sin_El_Rol_No_Se_Es_SuperAdmin_Aunque_La_Empresa_Sea_La_5(string rol)
    {
        Assert.False(CurrentUser.IsSuperAdmin(Usuario(rol, 5)));
    }
}
