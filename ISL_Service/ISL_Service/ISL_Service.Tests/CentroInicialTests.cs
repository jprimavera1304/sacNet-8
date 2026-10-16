using ISL_Service.Application.Security;

namespace ISL_Service.Tests;

/// <summary>
/// CON QUE CENTRO SE ABRE LA SESION, Y QUE LA PREFERENCIA GUARDADA NO SE CREA A
/// CIEGAS.
/// </summary>
/// <remarks>
/// Las cuatro reglas del contrato, una prueba cada una, mas las dos formas en
/// que una preferencia deja de valer. La que de verdad importa es
/// <see cref="Una_Preferencia_Que_Ya_No_Esta_Autorizada_No_Se_Respeta"/>: una
/// preferencia se queda guardada y lo que cambia es el permiso, asi que el dia
/// que a alguien le quitan un centro su preferencia sigue apuntando ahi. Creersela
/// seria abrir la sesion contra una base que esa persona ya no tiene autorizada.
/// </remarks>
public class CentroInicialTests
{
    private static readonly int[] Catalogo = { 7, 2, 9 };  // IDCentro, en orden del catalogo

    /// <summary>
    /// REGLA 1. La preferencia manda cuando sigue valiendo.
    ///
    /// ROTA A PROPOSITO: quitando el bloque de la preferencia en
    /// CentroInicial.Elegir, esta prueba falla con 0 (la matriz) en vez de 9.
    /// </summary>
    [Fact]
    public void La_Preferencia_Valida_Manda()
    {
        Assert.Equal(9, CentroInicial.Elegir(matrizAutorizada: true, Catalogo, preferencia: 9));
    }

    /// <summary>
    /// Y la matriz tambien es una preferencia valida: quien eligio la matriz a
    /// proposito no se encuentra un centro al entrar.
    /// </summary>
    [Fact]
    public void La_Matriz_Tambien_Se_Puede_Preferir()
    {
        Assert.Equal(
            CentroInicial.Matriz,
            CentroInicial.Elegir(matrizAutorizada: true, Catalogo, preferencia: CentroInicial.Matriz));
    }

    /// <summary>
    /// LA MITAD IMPORTANTE DE LA REGLA 1: "y sigue siendo valida".
    ///
    /// ROTA A PROPOSITO: quitando el `&amp;&amp; EsValida(...)` de la condicion,
    /// esta prueba falla devolviendo 4444 —un centro que esa persona ya no tiene
    /// autorizado—, que es exactamente el fallo que se queria evitar.
    /// </summary>
    [Fact]
    public void Una_Preferencia_Que_Ya_No_Esta_Autorizada_No_Se_Respeta()
    {
        var elegido = CentroInicial.Elegir(matrizAutorizada: true, Catalogo, preferencia: 4444);

        Assert.NotEqual(4444, elegido);
        Assert.Equal(CentroInicial.Matriz, elegido);
    }

    /// <summary>
    /// Y la matriz preferida tampoco vale si no esta autorizada: cae al primer
    /// centro del catalogo, no a la matriz.
    /// </summary>
    [Fact]
    public void La_Matriz_Preferida_No_Vale_Si_No_Esta_Autorizada()
    {
        Assert.Equal(7, CentroInicial.Elegir(matrizAutorizada: false, Catalogo, preferencia: CentroInicial.Matriz));
    }

    /// <summary>
    /// REGLA 2. Con uno solo autorizado, ese; sin preferencia que decida.
    /// </summary>
    [Fact]
    public void Con_Uno_Solo_Autorizado_Se_Abre_En_Ese()
    {
        Assert.Equal(2, CentroInicial.Elegir(matrizAutorizada: false, new[] { 2 }, preferencia: null));
    }

    /// <summary>
    /// REGLA 3. Con varios, el PRIMERO DEL CATALOGO, y la matriz va primera.
    ///
    /// Que la matriz gane es lo que hace que el dia del despliegue no cambie
    /// nada: hoy todo sale de la matriz hasta que alguien elige otra cosa. Si
    /// aqui saliera 7, a doce personas de Zaragoza les cambiaria la base de la
    /// que ven los datos sin haberlo pedido.
    /// </summary>
    [Fact]
    public void Con_Varios_Se_Abre_En_El_Primero_Del_Catalogo()
    {
        Assert.Equal(CentroInicial.Matriz, CentroInicial.Elegir(matrizAutorizada: true, Catalogo, preferencia: null));
        Assert.Equal(7, CentroInicial.Elegir(matrizAutorizada: false, Catalogo, preferencia: null));
    }

    /// <summary>
    /// EL ORDEN ES EL DEL CATALOGO Y NO EL DEL NUMERO. Ordenar aqui por lo bajo
    /// haria que "el primero" dejara de ser el que el catalogo enseña primero, y
    /// el selector y la sesion arrancarian en sitios distintos.
    /// </summary>
    [Fact]
    public void El_Primero_Es_El_Del_Catalogo_No_El_Mas_Bajo()
    {
        Assert.Equal(7, CentroInicial.Elegir(matrizAutorizada: false, new[] { 7, 2, 9 }, preferencia: null));
        Assert.Equal(9, CentroInicial.Elegir(matrizAutorizada: false, new[] { 9, 2, 7 }, preferencia: null));
    }

    /// <summary>
    /// REGLA 4. Sin nada autorizado, la matriz. No se inventa un centro: mandar a
    /// alguien a una base que no tiene autorizada es el fallo mas silencioso que
    /// hay aqui.
    /// </summary>
    [Fact]
    public void Sin_Nada_Autorizado_Se_Abre_En_La_Matriz()
    {
        Assert.Equal(CentroInicial.Matriz,
            CentroInicial.Elegir(matrizAutorizada: false, Array.Empty<int>(), preferencia: 9));
        Assert.Equal(CentroInicial.Matriz,
            CentroInicial.Elegir(matrizAutorizada: false, null, preferencia: null));
    }

    /// <summary>
    /// EL DIA DEL DESPLIEGUE, EN LA CUENTA REAL. Alguien de Zaragoza con sus doce
    /// centros viejos y sin preferencia guardada abre en la matriz, igual que
    /// ayer. Es la prueba de que las reglas de arriba, juntas y con la
    /// compatibilidad puesta, no le cambian el sitio a nadie.
    /// </summary>
    [Fact]
    public void El_Dia_Del_Despliegue_Nadie_Cambia_De_Sitio()
    {
        var permisos = new List<string> { PermisosDeCentros.CambiarLegado };
        permisos.AddRange(Enumerable.Range(2001, 12).Select(PermisosDeCentros.VerLegado));
        CompatibilidadDeCentros.Aplicar(permisos);

        var matriz = GuardiaDeCentros.PuedeVer(GuardiaDeCentros.Matriz, permisos.Contains);
        // IDCentro 1..12 para los numeros 2001..2012 en las dos bases.
        var centros = Enumerable.Range(1, 12)
            .Where(id => GuardiaDeCentros.PuedeVer(2000 + id, permisos.Contains))
            .ToList();

        Assert.True(matriz, "se quedaria sin matriz");
        Assert.Equal(12, centros.Count);
        Assert.Equal(CentroInicial.Matriz, CentroInicial.Elegir(matriz, centros, preferencia: null));
    }

    /// <summary>
    /// Y <see cref="CentroInicial.EsValida"/> contesta lo mismo que la eleccion,
    /// porque la usa quien GUARDA la preferencia: guardar una que ya no vale es
    /// dejar la trampa para el siguiente arranque.
    /// </summary>
    [Theory]
    [InlineData(0, true, true)]
    [InlineData(0, false, false)]
    [InlineData(9, true, true)]
    [InlineData(4444, true, false)]
    [InlineData(-1, true, true)]    // cualquier negativo es la matriz
    public void Se_Sabe_Si_Una_Preferencia_Todavia_Vale(int centro, bool matrizAutorizada, bool esperado)
    {
        Assert.Equal(esperado, CentroInicial.EsValida(centro, matrizAutorizada, Catalogo));
    }
}
