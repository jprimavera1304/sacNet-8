using System;

using ISL_Service.Application.DTOs.Empresa;

namespace ISL_Service.Application.DTOs.Responses;

public class MeResponse
{
    public Guid UserId { get; set; }
    public int IdUsuario { get; set; }
    public string RolLegacy { get; set; } = string.Empty;
    public bool PermissionsEnabled { get; set; }
    public List<string> Permissions { get; set; } = new();
    public string PermissionsVersion { get; set; } = string.Empty;

    public Guid Id { get; set; }
    public string Usuario { get; set; } = string.Empty;
    public string Rol { get; set; } = string.Empty;
    public int EmpresaId { get; set; }
    public string CompanyKey { get; set; } = string.Empty;
    public bool DebeCambiarContrasena { get; set; }

    /// <summary>
    /// LAS CAPACIDADES DE LA EMPRESA EN LA QUE CORRE ESTA INSTALACION.
    ///
    /// Viaja aqui, y no en un endpoint aparte, porque /api/me es la unica llamada
    /// que los dos fronts ya hacen justo despues de autenticar y la unica que ya
    /// cachean. Un bloque propio habria agregado un tercer round-trip al arranque y
    /// un tercer sitio donde preguntar lo mismo.
    ///
    /// Para RE-LEERLAS sin pedir la sesion entera -tras mover la fecha de
    /// operacion, por ejemplo- esta GET /api/empresa/capacidades, que devuelve este
    /// mismo objeto.
    ///
    /// Es null solo si la lectura de dbo.Constantes falla; el front debe tratar ese
    /// caso como "no se pueden dibujar reglas por empresa todavia", no como false.
    /// </summary>
    public CapacidadesEmpresaResponse? Empresa { get; set; }
}
