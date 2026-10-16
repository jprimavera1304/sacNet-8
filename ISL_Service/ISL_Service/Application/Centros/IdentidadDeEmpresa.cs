namespace ISL_Service.Application.Centros;

/// <summary>
/// DE QUIEN ES UNA BASE. Lo que la base dice de si misma, no lo que su nombre
/// sugiere.
/// </summary>
/// <remarks>
/// Sale de dbo.Constantes de ESA base, con un SELECT TOP 1 que no escribe nada
/// (no se usa sp_n_ConsultaConstantes: ese procedimiento CIERRA EL DIA cuando
/// la fecha de operacion esta atrasada — ver
/// migracion/CUIDADO-consultas-que-escriben.md. Comprobar de quien es una base
/// no puede rodar la fecha de operacion de un negocio).
///
/// Medido el 2026-10-15 en las copias locales:
///
///   MacZ            Func=ZARA  EsCS=0  FacturasCorreoNombre="Distribuidora de Acumuladores Zaragoza"
///   MacZCS1..CS12   Func=ZARA  EsCS=1  FacturasCorreoNombre="Distribuidora de Acumuladores Zaragoza"
///   Produccion_svr  Func=TAU   EsCS=0  FacturasCorreoNombre="Refacciones Automotrices TAURO"
///   MacL            Func=ZARA  EsCS=0  FacturasCorreoNombre="Distribuidora de Acumuladores Zaragoza"
/// </remarks>
/// <param name="NombreComercial">
/// Constantes.FacturasCorreoNombre: el nombre con el que la empresa se presenta
/// hacia afuera. Es el mismo en la matriz y en sus doce centros, y distinto
/// entre empresas. Vacio = la base no dice quien es.
/// </param>
/// <param name="Funcionalidad">
/// Constantes.Funcionalidad, en mayusculas: ZARA, TAU... la variante del
/// producto. Un centro corre la misma que su matriz.
/// </param>
/// <param name="EsCentroDeServicio">
/// Constantes.EsCentroServicio = 1. Lo pone la propia base de un centro; una
/// base de matriz trae 0.
/// </param>
public sealed record IdentidadDeEmpresa(
    string NombreComercial,
    string Funcionalidad,
    bool EsCentroDeServicio)
{
    /// <summary>Una base que no dijo quien es. Fuera de aqui no se distingue de un error.</summary>
    public static readonly IdentidadDeEmpresa Desconocida = new("", "", false);

    /// <summary>Si trae lo suficiente para poder comparar con algo.</summary>
    public bool EstaCompleta =>
        Normalizar(NombreComercial).Length > 0 && Normalizar(Funcionalidad).Length > 0;

    /// <summary>
    /// Deja solo letras y numeros en minuscula, para que un espacio de mas, un
    /// acento o un cambio de mayusculas en la captura no conviertan la misma
    /// empresa en dos.
    /// </summary>
    public static string Normalizar(string? valor)
    {
        if (string.IsNullOrEmpty(valor)) return string.Empty;
        var sb = new System.Text.StringBuilder(valor.Length);
        foreach (var c in valor)
        {
            if (char.IsLetterOrDigit(c)) sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    /// <summary>Si las dos bases dicen pertenecer a la misma empresa.</summary>
    public bool EsLaMismaEmpresaQue(IdentidadDeEmpresa otra) =>
        Normalizar(NombreComercial) == Normalizar(otra.NombreComercial)
        && Normalizar(Funcionalidad) == Normalizar(otra.Funcionalidad);
}

/// <summary>El veredicto, con el motivo que va a leer la persona.</summary>
/// <param name="Pertenece">true = esa base es un centro de esta empresa.</param>
/// <param name="Motivo">Vacio cuando pertenece. En espanol, legible, sin nombres de bases.</param>
public sealed record VeredictoDePertenencia(bool Pertenece, string Motivo)
{
    public static readonly VeredictoDePertenencia Si = new(true, "");
    public static VeredictoDePertenencia No(string motivo) => new(false, motivo);
}

/// <summary>
/// LA REGLA QUE PROTEGE A TAURO. Codigo puro; se prueba sin base.
/// </summary>
/// <remarks>
/// EL PROBLEMA, MEDIDO
/// -------------------
/// dbo.CentrosServicio de Produccion_svr (Tauro) tiene diez filas activas: las
/// seis primeras con BaseCentro VACIO y las cuatro ultimas apuntando a
/// MacZCS7..MacZCS10, que son bases DE ZARAGOZA. Esta comprobado igual en la
/// copia local. Un usuario de Tauro que pidiera el centro 2007 recibiria, sin
/// un solo error, las ventas de otra empresa.
///
/// NO SE ARREGLA TOCANDO PRODUCCION. Esas filas llevan anos ahi, legacy las lee
/// y nadie sabe que mas depende de ellas. Se protege AQUI, que ademas es lo
/// unico que sigue protegiendo el dia que alguien capture una fila nueva mal.
///
/// POR QUE NO SE JUZGA POR EL NOMBRE DE LA BASE
/// --------------------------------------------
/// "MacZCS7 no empieza por Produccion_svr" taparia este caso, y es un criterio
/// que no se sostiene:
///
///   * Falla al reves. MacZ y MacL comparten el prefijo "Mac" y son empresas
///     distintas; una base que se llamara "Produccion_svr_viejo" pasaria el
///     filtro de Tauro sin ser suya.
///   * Depende de una convencion de nombres que nadie firmo. El dia que se
///     renombre una base, o que un centro nuevo se llame distinto, el criterio
///     deja de decir la verdad — y lo hace en silencio, en la direccion mala.
///   * Un nombre no es una prueba de propiedad. Es una pista.
///
/// LO QUE SE HACE: SE LE PREGUNTA A LA BASE DE QUIEN ES. Se abre esa base con
/// la MISMA cadena (mismo servidor, mismo usuario: solo cambia el catalogo) y
/// se leen tres cosas de su propia dbo.Constantes. Tienen que cumplirse LAS
/// TRES:
///
///   1. Dice el MISMO nombre comercial que la matriz (FacturasCorreoNombre).
///      Es el nombre con el que la empresa factura: no es una convencion de
///      nombres de archivo, es la identidad del negocio, y esta capturada en
///      las doce bases de Zaragoza igual que en MacZ.
///   2. Dice la MISMA Funcionalidad (ZARA/TAU). Un centro corre la misma
///      variante del producto que su matriz; si no coincide, no es su centro
///      aunque se llame parecido.
///   3. Dice de si misma que ES un centro de servicio (EsCentroServicio = 1).
///      Esto es lo que impide que una fila mal capturada apunte a la base de
///      OTRA MATRIZ de la misma empresa (MacL trae EsCS=0 y el mismo nombre
///      comercial que MacZ: sin esta tercera condicion, MacL pasaria).
///
/// Y FALLA CERRADO. Si la base no se puede abrir, no tiene Constantes, o
/// cualquiera de los dos lados viene incompleto, el veredicto es NO. Una
/// comprobacion de propiedad que no se pudo hacer no es un permiso.
/// </remarks>
public static class PertenenciaDeCentro
{
    public static VeredictoDePertenencia Juzgar(
        IdentidadDeEmpresa matriz,
        IdentidadDeEmpresa candidata)
    {
        /* Si la MATRIZ no dice quien es, no hay con que comparar. Se niegan
           todos los centros: es preferible quedarse sin selector a ofrecer uno
           que no se puede verificar. */
        if (!matriz.EstaCompleta)
            return VeredictoDePertenencia.No(
                "No se pudo verificar a que empresa pertenece este centro porque la base principal no tiene configurada la identidad de la empresa.");

        if (!candidata.EstaCompleta)
            return VeredictoDePertenencia.No(
                "No se pudo verificar a que empresa pertenece ese centro: su base no esta disponible o no dice de quien es.");

        if (!candidata.EsLaMismaEmpresaQue(matriz))
            return VeredictoDePertenencia.No(
                "Ese centro esta capturado con la base de OTRA empresa. No se muestran sus datos.");

        if (!candidata.EsCentroDeServicio)
            return VeredictoDePertenencia.No(
                "Esa base no es la de un centro de servicio.");

        return VeredictoDePertenencia.Si;
    }
}
