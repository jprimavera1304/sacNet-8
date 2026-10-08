namespace ISL_Service.Application.DTOs.Catalogos;

/*
  UN USUARIO DE MAC31 PARA UN DESPLEGABLE. TRES CAMPOS, Y NI UNO MAS.

  POR QUE ESTE DTO ES TAN CORTO, A PROPOSITO
  ------------------------------------------
  sp_n_ConsultaUsuarios con @Formato = 0 devuelve, leido del procedimiento en la
  base el 07-10-2026:

      C.IDUsuario, C.IDStatus, C.IDPerfil, C.Usuario, C.Contrasena, C.Nombre,
      P.Perfil, S.Status, ContrasenaDesc, Huellas, NumUsuario,
      AplicaContrasenaAutorizacion, AplicaContrasenaAutorizacionFtm,
      ContrasenaAutorizacion, Multicajas

  Tres de esas columnas son CONTRASENAS EN CLARO: `Contrasena`,
  `ContrasenaDesc` y `ContrasenaAutorizacion`. Mac31 las necesita porque alla la
  misma pantalla es el ALTA de usuarios; aqui lo unico que se esta resolviendo es
  "quien recibe" en un cambio de vigencia, que es elegir una persona de una lista.

  Por eso no se devuelven renglones crudos (el patron de
  List&lt;Dictionary&lt;string, object?&gt;&gt; que usan las otras consultas de
  legacy en este backend): ahi la columna que el procedimiento agregue manana
  sale publicada sola. Con un DTO de tres campos, lo que no esta declarado NO
  PUEDE salir, y agregar un campo sensible tendria que ser una decision escrita.

  GET /api/usuarios es OTRA COSA y no sirve para esto: devuelve `dbo.UsuarioWeb`,
  los usuarios del web, con otro identificador. `IDUsuarioRecibe` de
  CambiosVigencia es un `IDUsuario` de `dbo.Usuarios`, la tabla de Mac31.
*/
public class UsuarioMacItemDto
{
    /// <summary>`dbo.Usuarios.IDUsuario` de Mac31. Es el que se guarda en `IDUsuarioRecibe`.</summary>
    public int IdUsuario { get; set; }

    /// <summary>La clave con la que entra a Mac31 ("JUAN"). Es lo que Mac31 pinta en el desplegable.</summary>
    public string Usuario { get; set; } = string.Empty;

    /// <summary>
    /// El nombre de la persona. Va porque el desplegable se ordena por el
    /// (`ORDER BY Nombre` del procedimiento) y porque dos claves parecidas se
    /// distinguen por el nombre y no por la clave.
    /// </summary>
    public string Nombre { get; set; } = string.Empty;
}
