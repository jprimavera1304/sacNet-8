using System.Text.RegularExpressions;

namespace ISL_Service.Tests;

/// <summary>
/// LAS LECTURAS QUE DEJABAN PASAR A CUALQUIERA CON SESION.
///
/// COMO SE ENCONTRARON, Y POR QUE NO LEYENDO EL CODIGO
/// ---------------------------------------------------
/// Se intento primero con un barrido del codigo fuente: buscar los
/// <c>[HttpGet]</c> sin <c>perm:</c> cerca. Dio 199 resultados y casi todos
/// eran falsos — el permiso se exige muchas veces DENTRO del metodo, con
/// <c>ExigirPermisoAsync</c>, decenas de lineas mas abajo, o en un arreglo
/// declarado al principio de la clase. Un numero asi no se puede ni reportar.
///
/// Lo que si contesto fue medirlo: el 2026-10-12 se creo una cuenta de
/// auditoria sin UN SOLO permiso (ni rol, ni override, ni usuario en Mac31) y
/// se llamo con su token a los 220 GET del servicio, uno por uno. 192
/// contestaron 401/403, 12 no fueron concluyentes (400/500) y <b>16
/// contestaron 200</b>.
///
/// De esos 16, once son a proposito y estan razonados en su sitio: los cinco
/// del arranque de la sesion (/api/me, /api/app/version, /api/capacidades,
/// /api/empresa/capacidades, /api/modulos/disponibles), que solo devuelven lo
/// que al que pregunta le corresponde; los cuatro catalogos de combos y
/// /api/ventas/consulta/catalogos, que son el diccionario que llena los
/// desplegables de media aplicacion (ver la nota de <c>catalogos.*</c> en
/// EntradasDeModulo.cs); y /api/checador/configuracion, que es el horario que
/// pinta la pantalla de checar.
///
/// Los otros CINCO entregaban datos del negocio sin preguntar nada, y son los
/// que esta prueba fija. Mas dos de la liga deportiva que no se pudieron medir
/// aqui -sus SP no estan desplegados en Zaragoza ni en Tauro- y que en su base
/// serian una lista abierta.
///
/// POR QUE UNA PRUEBA DE TEXTO Y NO UNA QUE LLAME AL SERVICIO
/// ----------------------------------------------------------
/// Porque el barrido de verdad necesita el servidor levantado y una cuenta
/// creada, y eso no cabe en una prueba que tiene que correr en cualquier
/// maquina en milisegundos. Esto es la red: fija que el candado sigue escrito
/// donde se puso. Es la misma forma que <see cref="Ola10FallarCerradoTests"/>,
/// y por la misma razon.
/// </summary>
public class LecturasQueNoPidenPermisoTests
{
    private static readonly string Controladores =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "Controllers"));

    private static string Fuente(string archivo)
    {
        var ruta = Path.Combine(Controladores, archivo);
        Assert.True(File.Exists(ruta), $"No se encontro {ruta}");
        return File.ReadAllText(ruta);
    }

    /// Cuantas veces aparece el atributo con esa clave, para no confundir el
    /// candado de una lectura con el de una escritura que ya lo tenia.
    ///
    /// SE ACEPTA QUE LA POLITICA LLEVE UNA SEGUNDA CLAVE despues de `|`. Un
    /// endpoint puede servir a dos canales -el web con `almacen_cascos.ver` y
    /// el telefono con `app_movil.almacen_cascos.ver`- y entonces la politica
    /// es "perm:a|b". Lo que esta prueba vigila es que el candado SIGA PUESTO
    /// con esta clave, no que sea la unica aceptada; con `|` basta tener una,
    /// asi que agregar la del otro canal no abre nada a quien no tenia ninguna.
    ///
    /// Lo que sigue cayendo aqui -y es para lo que existe- es que alguien BORRE
    /// el atributo o le cambie la clave por otra que el front no pide.
    private static int Candados(string fuente, string clave) =>
        Regex.Matches(fuente,
            Regex.Escape($"[Authorize(Policy = \"perm:{clave}") + "(\"|\\|)").Count;

    [Fact]
    public void Los_Movimientos_De_Cascos_No_Se_Leen_Solo_Con_Sesion()
    {
        var f = Fuente("AlmacenCascosController.cs");

        // Los cuatro GET (movimientos, detalle, tarimas, detalle-agrupado) mas
        // el de existencias, que ya lo tenia: cinco lecturas con la clave.
        Assert.True(Candados(f, "almacen_cascos.ver") >= 5,
            "Alguna lectura de almacen de cascos se quedo sin su permiso.");

        // Y que el atributo este pegado a cada GET, no suelto en el archivo.
        foreach (var get in new[]
                 {
                     "[HttpGet(\"movimientos\")]",
                     "[HttpGet(\"movimientos/{idMovimiento:int}/detalle\")]",
                     "[HttpGet(\"movimientos/{idMovimiento:int}/tarimas\")]",
                     "[HttpGet(\"movimientos/{idMovimiento:int}/detalle-agrupado\")]"
                 })
        {
            var i = f.IndexOf(get, StringComparison.Ordinal);
            Assert.True(i > 0, $"Ya no existe {get}");
            var antes = f.Substring(Math.Max(0, i - 400), Math.Min(400, i));
            Assert.Contains("perm:almacen_cascos.ver", antes);
        }
    }

    [Fact]
    public void El_Saldo_De_Un_Cliente_No_Se_Le_Ensena_A_Cualquiera()
    {
        var f = Fuente("VentasSaldosController.cs");

        // Las cinco claves son las de sus cuatro llamadores (dos del web y dos
        // del movil) y estan razonadas en el propio controlador. Si alguien
        // quita una, aqui se nota.
        foreach (var clave in new[]
                 {
                     "ventas.consultar", "ventas.ver_modulo", "clientes.ver_modulo",
                     "app_movil.ventas.consulta.ver", "app_movil.clientes"
                 })
        {
            Assert.Contains(clave, f);
        }

        Assert.Contains("[Authorize(Policy = \"perm:", f);
        Assert.Contains("[HttpGet(\"cliente/{idCliente:int}\")]", f);
    }

    [Theory]
    [InlineData("TorneosController.cs", "torneos.ver")]
    [InlineData("TemporadasController.cs", "temporadas.ver")]
    public void Las_Lecturas_De_La_Liga_Tambien_Piden_Permiso(string archivo, string clave)
    {
        var f = Fuente(archivo);

        // Las dos lecturas de cada uno: la lista y el detalle por id.
        Assert.True(Candados(f, clave) >= 2,
            $"{archivo}: alguna lectura se quedo con solo [Authorize].");
    }
}
