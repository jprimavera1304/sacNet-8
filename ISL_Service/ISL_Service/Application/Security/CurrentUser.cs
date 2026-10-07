using System.Security.Claims;

namespace ISL_Service.Application.Security;

public static class CurrentUser
{
    public static Guid GetUserId(ClaimsPrincipal user)
    {
        var id = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? user.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(id))
            throw new UnauthorizedAccessException("Token inválido: falta sub/nameid.");

        return Guid.Parse(id);
    }

    public static string GetRol(ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.Role) ?? user.FindFirstValue("rol") ?? "";

    public static int GetEmpresaId(ClaimsPrincipal user)
    {
        var val = user.FindFirstValue("empresaId") ?? user.FindFirstValue("companyId");
        // TryParse y no Parse: un claim no numerico (token de otra emision, o
        // manipulado) tiraba la peticion entera con una FormatException que
        // caia al catch generico del middleware como "Error interno.". Un
        // empresaId que no es un numero es "no se sabe de que empresa", o sea 0,
        // que es exactamente lo que ya se devuelve cuando el claim no viene.
        return int.TryParse(val, out var empresaId) ? empresaId : 0;
    }

    /*
      SER SUPERADMIN ES UN ROL, NO UN NUMERO DE EMPRESA.

      Aqui decia ademas `|| GetEmpresaId(user) == 5`: cualquiera cuya empresa
      fuera la 5 pasaba por SuperAdmin, sin importar su rol. Y no es un detalle
      cosmetico, porque de esto cuelgan la administracion de usuarios, la de
      empresas y el atajo que salta la comprobacion de permisos en
      PermisoControllerBase.

      Hoy esta inerte -ninguna de las dos bases tiene una empresa con Id 5- y
      por eso nadie lo habia notado. Pero es una mina: el dia que una empresa
      reciba ese id, todos sus usuarios se vuelven administradores del sistema
      sin que nadie lo decida ni lo vea en ninguna pantalla.

      Viene del commit del login, sin comentario que lo explique. No se le
      encontro ningun uso legitimo: lo encontro la auditoria de cierre.
    */
    public static bool IsSuperAdmin(ClaimsPrincipal user)
        => string.Equals(GetRol(user), "SuperAdmin", StringComparison.OrdinalIgnoreCase);
}
