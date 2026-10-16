using ISL_Service.Application.Centros;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// DE QUIEN ES CADA BASE. Lo pregunta a la base, no al nombre.
/// </summary>
/// <remarks>
/// DE DONDE SALE EL NOMBRE COMERCIAL DE LA EMPRESA, Y POR QUE DE AHI
/// -----------------------------------------------------------------
/// De `dbo.Constantes.FacturasCorreoNombre`. Medido el 2026-10-15 en las copias
/// locales: "Distribuidora de Acumuladores Zaragoza" en MacZ y en sus doce
/// centros, "Refacciones Automotrices TAURO" en Produccion_svr. Es el nombre
/// con el que la empresa se presenta hacia afuera, que es lo que tiene que
/// decir un selector de sucursales.
///
/// LO QUE SE DESCARTO, Y POR QUE
/// -----------------------------
///   * `Constantes.NombreCentroServicio` — esta VACIO en las dos bases de
///     matriz (comprobado). Solo lo llenan las bases de los centros
///     ("CS1 - ZARAGOZA", "CS7 - CIRCUITO"), asi que sirve para nombrar un
///     centro pero NO para nombrar la matriz, que es justo lo que hace falta.
///   * `Constantes.NombrePDF` — dice "Mac21Fact". Es un nombre de archivo.
///   * `DB_NAME()` — "MacZ" no es un nombre comercial; es la pista que este
///     contrato dice expresamente que no debe viajar al cliente. Se usa SOLO
///     como ultimo recurso para no contestar vacio.
///   * La palabra "Principal" que esta hoy en duro en
///     ReportesVentasController.cs:450 — no es el nombre de nadie.
///
/// Y SE DEJA UN SITIO DONDE CONFIGURARLO. `FacturasCorreoNombre` es el nombre
/// FISCAL y puede ser largo para un selector ("Distribuidora de Acumuladores
/// Zaragoza" cuando la gente dice "Zaragoza"). Si alguien quiere el corto, se
/// pone en appsettings `Centros:NombreDeLaMatriz` y manda. No se invento una
/// columna nueva en una tabla de legacy para eso.
/// </remarks>
public interface IIdentidadDeEmpresaRepository
{
    /// <summary>
    /// Quien es la empresa de la sesion, leido de la base PRINCIPAL.
    /// </summary>
    Task<IdentidadDeEmpresa> DeLaMatrizAsync(CancellationToken ct = default);

    /// <summary>
    /// El nombre de la base principal (DB_NAME()). Solo para el servidor: para
    /// la clave de la cache y para los mensajes del log.
    /// </summary>
    Task<string> BaseDeLaMatrizAsync(CancellationToken ct = default);

    /// <summary>
    /// Quien dice ser esa base. <see cref="IdentidadDeEmpresa.Desconocida"/> si
    /// no se pudo abrir o no dice nada: falla cerrado.
    /// </summary>
    Task<IdentidadDeEmpresa> DeLaBaseAsync(string nombreDeLaBase, CancellationToken ct = default);
}
