using ISL_Service.Application.DTOs.Maestros;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using Microsoft.AspNetCore.Mvc;

namespace ISL_Service.Controllers;

/// <summary>
/// Lo que comparten los tres controladores de ficha maestra (cliente, producto,
/// proveedor): como se traduce a HTTP el par result/mensaje de legacy, y las dos
/// validaciones de forma que se repiten en todos los campos.
///
/// POR QUE UN 200 CON ok=false Y NO UN 409 CUANDO LEGACY RECHAZA
/// -----------------------------------------------------------
/// Porque el texto del rechazo es el mensaje de la pantalla: "YA EXISTE UN
/// CLIENTE CON ESE RFC" se muestra al lado del campo. Un 4xx lo manda al
/// manejador de errores global, donde ese texto se convierte en "ocurrio un
/// error" y la persona no sabe que corregir. El sobre { ok, message } ya dice si
/// se guardo o no.
/// </summary>
public abstract class MaestrosControllerBase : PermisoControllerBase
{
    protected const int StatusActivo = 1;
    protected const int StatusCancelado = 2;

    protected readonly IMaestrosService Service;

    protected MaestrosControllerBase(
        IMaestrosService service,
        ICurrentUserAccessor currentUser,
        IPermissionService permissionService)
        : base(currentUser, permissionService)
    {
        Service = service;
    }

    protected IActionResult Responder(MaestroEscrituraResponse r, string mensajeExito)
        => Ok(new
        {
            ok = r.Guardo,
            message = r.Guardo
                ? mensajeExito
                : (string.IsNullOrWhiteSpace(r.Mensaje) ? "La base rechazo la operacion." : r.Mensaje),
            data = r
        });

    protected IActionResult Responder(MaestroEscrituraResponse? r, string mensajeExito, string noExiste)
        => r is null
            ? NotFound(new { ok = false, message = noExiste })
            : Responder(r, mensajeExito);

    protected IActionResult RespondeAlta(MaestroEscrituraResponse r, string mensajeExito)
        => r.Guardo
            ? StatusCode(StatusCodes.Status201Created, new { ok = true, message = mensajeExito, data = r })
            : Responder(r, mensajeExito);

    /// <summary>
    /// Campo de texto requerido con tope. El tope es el del varchar del
    /// procedimiento: sin el, SQL Server recorta en silencio y la persona cree
    /// que guardo lo que escribio.
    /// </summary>
    protected static object? ValidaTexto(string? valor, int maximo, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return new { ok = false, message = $"El campo {campo} es requerido." };
        if (valor.Trim().Length > maximo)
            return new { ok = false, message = $"El campo {campo} no puede pasar de {maximo} caracteres." };
        return null;
    }

    /// <summary>Lo mismo, pero el campo es opcional: solo revisa el tope.</summary>
    protected static object? ValidaTopeOpcional(string? valor, int maximo, string campo)
        => valor != null && valor.Trim().Length > maximo
            ? new { ok = false, message = $"El campo {campo} no puede pasar de {maximo} caracteres." }
            : null;

    protected static object? ValidaId(int id, string campo)
        => id <= 0 ? new { ok = false, message = $"El campo {campo} es requerido." } : null;
}
