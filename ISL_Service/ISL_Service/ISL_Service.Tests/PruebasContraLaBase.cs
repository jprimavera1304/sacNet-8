namespace ISL_Service.Tests;

/// <summary>
/// Las pruebas que escriben en la base real no corren a la vez.
/// </summary>
/// <remarks>
/// EL SINTOMA QUE LA OBLIGO A EXISTIR
/// ----------------------------------
/// <see cref="SincronizacionBidireccionalDeTodasLasFamiliasTests"/> pasaba sola
/// y fallaba dentro de la suite completa. No era el codigo de produccion: xUnit
/// corre las clases en PARALELO por defecto, y estas pruebas comparten mas que
/// el servidor —comparten las dos bases—. Una apaga el modulo 'compras' entre
/// dos aserciones (<see cref="EscenariosPermisosContraLaBase"/>) mientras otra
/// esta midiendo si ese modulo deja escribir en Mac31, y la segunda acusa un
/// fallo que no existe.
///
/// Una prueba que pasa sola y falla acompanada no sirve para nada: no se puede
/// distinguir una regresion de un choque. Poniendolas en una sola coleccion,
/// xUnit las serializa.
///
/// NO SE ARREGLA CON REINTENTOS NI BAJANDO LA CONCURRENCIA GLOBAL. Reintentar
/// esconderia justo lo que estas pruebas buscan, y apagar el paralelismo de
/// toda la suite alargaria las 650 que no tocan la base.
/// </remarks>
[CollectionDefinition(Nombre)]
public sealed class PruebasContraLaBase
{
    public const string Nombre = "contra la base de verdad";
}
