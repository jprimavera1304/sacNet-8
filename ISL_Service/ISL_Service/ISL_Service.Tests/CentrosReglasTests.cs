using ISL_Service.Application.Centros;
using ISL_Service.Application.Security;
using Xunit;

namespace ISL_Service.Tests;

/// <summary>
/// LAS REGLAS DE LOS CENTROS, SIN BASE Y SIN DI.
/// </summary>
/// <remarks>
/// Aqui esta lo que de verdad protege: de quien es una base, quien puede que, y
/// que pasa con un modulo que no se declaro. Son decisiones puras a proposito —
/// una regla que solo se puede comprobar levantando SQL Server no se comprueba,
/// y estas son las que no pueden fallar nunca.
///
/// Las mismas reglas, contra las bases de verdad, en
/// <see cref="CentrosContraLaBaseTests"/>.
/// </remarks>
public class CentrosReglasTests
{
    // ------------------------------------------------------------- X-Centro

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Sin_Cabecera_Es_La_Matriz(string? crudo)
    {
        Assert.True(CentroPedido.Interpretar(crudo, out var id));
        Assert.Equal(CentroPedido.Matriz, id);
        Assert.Equal(0, id);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("7", 7)]
    [InlineData(" 12 ", 12)]
    public void Un_Entero_Se_Lee_Tal_Cual(string crudo, int esperado)
    {
        Assert.True(CentroPedido.Interpretar(crudo, out var id));
        Assert.Equal(esperado, id);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-1")]
    [InlineData("3.5")]
    [InlineData("4; DROP")]
    [InlineData("MacZCS7")]
    public void Un_Valor_Basura_No_Se_Redondea_A_Matriz(string crudo)
    {
        /* Si "abc" se interpretara como 0, un cliente roto recibiria datos de la
           matriz creyendo que son de su sucursal. Se rechaza. */
        Assert.False(CentroPedido.Interpretar(crudo, out _));
    }

    // ------------------------------------------- el cuerpo de reportes manda

    [Fact]
    public void El_IdCentro_Del_Cuerpo_Le_Gana_A_La_Cabecera()
    {
        /* Es lo que ya funciona: los POST de generar reportes mandan idCentro
           en el cuerpo desde el web y desde los APK ya instalados. */
        var (gana, desacuerdo) = CentroPedido.Conciliar(idCentroDelCuerpo: 4, idCentroDeLaCabecera: 9);
        Assert.Equal(4, gana);
        Assert.True(desacuerdo);   // y se registra
    }

    [Fact]
    public void Si_Coinciden_No_Hay_Desacuerdo_Que_Registrar()
    {
        var (gana, desacuerdo) = CentroPedido.Conciliar(4, 4);
        Assert.Equal(4, gana);
        Assert.False(desacuerdo);
    }

    [Fact]
    public void Un_APK_Viejo_Manda_Cuerpo_Y_Ninguna_Cabecera_Y_Sigue_Yendo_A_Su_Centro()
    {
        var (gana, desacuerdo) = CentroPedido.Conciliar(idCentroDelCuerpo: 4, idCentroDeLaCabecera: 0);
        Assert.Equal(4, gana);
        Assert.False(desacuerdo);   // no hay con quien discrepar
    }

    [Fact]
    public void Cuerpo_En_Cero_Respeta_La_Cabecera()
    {
        /* El cuerpo en 0 no se distingue de "no lo mandaron", asi que lo unico
           que dijo algo es la cabecera. */
        var (gana, _) = CentroPedido.Conciliar(idCentroDelCuerpo: 0, idCentroDeLaCabecera: 9);
        Assert.Equal(9, gana);
    }

    // --------------------------------------------------- modulos declarados

    [Fact]
    public void Reportes_Esta_Declarado_Y_Es_Solo_Lectura()
    {
        Assert.True(ModulosConCentros.SoportaCentros("reportes"));
        /* Un reporte solo lee. Que no aparezca aqui ninguna operacion de
           escritura no es un olvido. */
        Assert.False(ModulosConCentros.PuedeEscribirFueraDeLaMatriz("reportes", "generar"));
    }

    [Theory]
    [InlineData("cortes")]
    [InlineData("facturacion")]
    [InlineData("inventario.ajustes")]
    [InlineData("inventario.transito")]
    public void Los_Cuatro_Que_Entraron_Leen_Pero_No_Escriben(string modulo)
    {
        /*  Entraron el 19-nov-2026 y se midio antes de declararlos: en
            produccion de Zaragoza hay 2,163 cortes, 6,799 facturas, 1,501
            ajustes y 27,066 remisiones DENTRO de las trece bases de centro, y
            la tabla de cortes de la matriz esta en CERO. Sin esto, cortes esta
            condenado a salir vacio para siempre.

            Y NINGUNO ESCRIBE FUERA DE LA MATRIZ. Fue lo que se pidio —"asi como
            en reportes"— y reportes es de solo lectura. Un ajuste mueve
            existencias y una cancelacion de factura toca un CFDI: aplicarlos a
            la base equivocada no se deshace con un boton. Cuando se decida, se
            nombra la operacion y esta prueba se cae, que es lo correcto.  */
        Assert.True(ModulosConCentros.SoportaCentros(modulo));

        foreach (var operacion in new[] { "crear", "guardar", "cancelar", "timbrar", "procesar" })
            Assert.False(ModulosConCentros.PuedeEscribirFueraDeLaMatriz(modulo, operacion));
    }

    [Fact]
    public void De_Los_Catalogos_Solo_Almacenes_Sigue_Al_Centro()
    {
        /*  POR QUE ALMACENES SI. La pantalla de ajustes manda `idAlmacen` al
            servidor para filtrar, y ese id se resuelve DENTRO de la base que
            se consulta. Con los ajustes saliendo de un centro y el catalogo
            saliendo de la matriz, el filtro buscaria un numero que alla
            significa otra cosa, o nada: lista vacia sin motivo a la vista, o
            los renglones de otro almacen. Nada falla; solo miente.  */
        Assert.True(ModulosConCentros.SoportaCentros("catalogos.almacenes"));
        Assert.False(
            ModulosConCentros.PuedeEscribirFueraDeLaMatriz("catalogos.almacenes", "guardar"));

        /*  Y POR QUE NO EL MODULO ENTERO. `catalogos` arrastraria los otros
            veintitantos —agentes, tipos de gasto, dias festivos— que NO
            cambian de significado por centro, y los arrastraria en las veinte
            pantallas que los piden. Un catalogo, una linea.  */
        Assert.False(ModulosConCentros.SoportaCentros("catalogos"));
    }

    [Fact]
    public void Almacen_De_Cascos_Lee_Los_Centros_Pero_No_Escribe_En_Ellos()
    {
        /*  ESTA PRUEBA NACIO DICIENDO LO CONTRARIO, y la correccion vale mas
            que la regla.

            Primero se dejo el modulo fuera del registro porque `WMovimientoCasco`
            no existe en ninguna de las trece bases. El dato era cierto y la
            conclusion falsa: esa tabla es NUEVA, se hizo para la app y solo
            vive en la matriz. Los movimientos de cascos de verdad estan en
            `UsadosMovimientos`, que es lo que lee Mac31, y ahi el reparto es
            el de cortes. Medido en produccion el 19-nov-2026:

              UsadosMovimientos  ->  MacZ: 0    los trece centros: 7,818
              WMovimientoCasco   ->  MacZ: 2    los trece centros: no existe

            Asi que el modulo SI sabe de centros, leyendo otra tabla alla.

            Y NO ESCRIBE FUERA DE LA MATRIZ, que aqui pesa mas que en los otros
            cuatro: las tablas de la app existen solo en la matriz, asi que una
            captura en un centro o se perderia o crearia una segunda lista al
            lado de la de legacy, sin forma de saber cual es la buena. La
            captura de un centro se hace en Mac31.  */
        Assert.True(ModulosConCentros.SoportaCentros("almacen.cascos"));

        foreach (var operacion in new[] { "crear", "aceptar", "editar", "kilos", "cancelar" })
            Assert.False(ModulosConCentros.PuedeEscribirFueraDeLaMatriz("almacen.cascos", operacion));
    }

    [Theory]
    [InlineData("ventas")]
    [InlineData("inventario")]
    [InlineData("cobranzas")]
    [InlineData("")]
    [InlineData(null)]
    public void Un_Modulo_No_Declarado_No_Soporta_Centros(string? modulo)
    {
        /* LA OMISION ES EL 409. Si manana alguien anade uno de estos a
           ModulosConCentros, esta prueba se cae y eso es correcto: anadirlo es
           una decision, no un efecto secundario. */
        Assert.False(ModulosConCentros.SoportaCentros(modulo));
        Assert.Null(ModulosConCentros.Buscar(modulo));
    }

    [Fact]
    public void Anadir_Un_Modulo_Es_Una_Linea_Y_Las_Escrituras_Se_Nombran_Una_Por_Una()
    {
        var soloLee = ModuloConCentros.SoloLectura("mi_modulo");
        Assert.True(soloLee.SoportaCentros);
        Assert.False(soloLee.Escribe("guardar"));

        var escribe = ModuloConCentros.ConEscritura("otro", "guardar");
        Assert.True(escribe.Escribe("guardar"));
        Assert.True(escribe.Escribe("GUARDAR"));          // no distingue mayusculas
        Assert.False(escribe.Escribe("cancelar"));        // no declarada = no escribe
        Assert.False(escribe.Escribe(null));

        var revisadoYNo = ModuloConCentros.No("legacy_puro");
        Assert.False(revisadoYNo.SoportaCentros);
        Assert.False(revisadoYNo.Escribe("lo_que_sea"));
    }

    // ------------------------------------------------ de quien es una base

    private static readonly IdentidadDeEmpresa Zaragoza =
        new("Distribuidora de Acumuladores Zaragoza", "ZARA", EsCentroDeServicio: false);

    private static readonly IdentidadDeEmpresa CentroDeZaragoza =
        new("Distribuidora de Acumuladores Zaragoza", "ZARA", EsCentroDeServicio: true);

    private static readonly IdentidadDeEmpresa Tauro =
        new("Refacciones Automotrices TAURO", "TAU", EsCentroDeServicio: false);

    /// <summary>MacL: mismo nombre comercial que MacZ, pero NO es un centro.</summary>
    private static readonly IdentidadDeEmpresa OtraMatrizDelGrupo =
        new("Distribuidora de Acumuladores Zaragoza", "ZARA", EsCentroDeServicio: false);

    [Fact]
    public void Un_Centro_De_La_Misma_Empresa_Pasa()
    {
        Assert.True(PertenenciaDeCentro.Juzgar(Zaragoza, CentroDeZaragoza).Pertenece);
    }

    [Fact]
    public void Un_Centro_De_Otra_Empresa_Se_Rechaza_Y_Es_Lo_Que_Protege_A_Tauro()
    {
        /* dbo.CentrosServicio de Produccion_svr tiene cuatro filas apuntando a
           MacZCS7..MacZCS10, que son bases de Zaragoza. Esto es lo que impide
           que un usuario de Tauro vea las ventas de otra empresa. */
        var veredicto = PertenenciaDeCentro.Juzgar(Tauro, CentroDeZaragoza);

        Assert.False(veredicto.Pertenece);
        Assert.Contains("OTRA empresa", veredicto.Motivo);
        /* Y el motivo no delata el nombre de la base: eso se queda en el log. */
        Assert.DoesNotContain("MacZ", veredicto.Motivo);
    }

    [Fact]
    public void Otra_Matriz_Con_El_Mismo_Nombre_Comercial_Tampoco_Pasa()
    {
        /* MacL trae el mismo FacturasCorreoNombre y la misma Funcionalidad que
           MacZ, y no es un centro de nadie: trae EsCentroServicio = 0. Sin la
           tercera condicion pasaria, y una fila mal capturada apuntaria a la
           base de OTRA instalacion completa. */
        var veredicto = PertenenciaDeCentro.Juzgar(Zaragoza, OtraMatrizDelGrupo);

        Assert.False(veredicto.Pertenece);
        Assert.Contains("no es la de un centro de servicio", veredicto.Motivo);
    }

    [Fact]
    public void Si_No_Se_Pudo_Abrir_La_Base_Del_Centro_Falla_CERRADO()
    {
        /* IdentidadDeEmpresa.Desconocida es lo que devuelve el repositorio
           cuando la base no esta, no hay permiso o no tiene Constantes. Una
           comprobacion de propiedad que no se pudo hacer no es un permiso. */
        var veredicto = PertenenciaDeCentro.Juzgar(Zaragoza, IdentidadDeEmpresa.Desconocida);
        Assert.False(veredicto.Pertenece);
    }

    [Fact]
    public void Si_La_MATRIZ_No_Dice_Quien_Es_No_Se_Ofrece_Ningun_Centro()
    {
        var veredicto = PertenenciaDeCentro.Juzgar(IdentidadDeEmpresa.Desconocida, CentroDeZaragoza);
        Assert.False(veredicto.Pertenece);
    }

    [Theory]
    [InlineData("Distribuidora de Acumuladores Zaragoza", "DISTRIBUIDORA DE ACUMULADORES ZARAGOZA")]
    [InlineData("  Refacciones Automotrices TAURO ", "Refacciones Automotrices TAURO")]
    public void Un_Espacio_O_Una_Mayuscula_No_Convierten_La_Misma_Empresa_En_Dos(string a, string b)
    {
        var uno = new IdentidadDeEmpresa(a, "ZARA", false);
        var otro = new IdentidadDeEmpresa(b, "zara", true);
        Assert.True(uno.EsLaMismaEmpresaQue(otro));
    }

    [Fact]
    public void El_Nombre_De_La_Base_No_Es_Lo_Que_Decide()
    {
        /* Se deja escrito como prueba porque es la tentacion obvia: "MacZCS7
           empieza por MacZ, luego es de MacZ". El criterio es el CONTENIDO de la
           base, asi que dos nombres que no se parecen en nada pueden pertenecer
           a la misma empresa y dos que se parecen mucho pueden no hacerlo. */
        var conNombreRaro = new IdentidadDeEmpresa("Distribuidora de Acumuladores Zaragoza", "ZARA", true);
        Assert.True(PertenenciaDeCentro.Juzgar(Zaragoza, conNombreRaro).Pertenece);
    }

    // ------------------------------------------------------------ permisos

    private static CapacidadesDeCentros Con(params string[] permisos)
    {
        var snapshot = new SnapshotDeLaSesion.Resultado(true, false, permisos);
        return new CapacidadesDeCentros(false, permisos, c => snapshot.Tiene(c));
    }

    [Fact]
    public void La_Clave_Nueva_Abre()
    {
        var puede = Con(ClavesDeCentros.Cambiar, ClavesDeCentros.Ver(2004));
        Assert.True(puede.PuedeCambiar);
        Assert.True(puede.PuedeVer(2004));
    }

    [Fact]
    public void Quien_Hoy_Puede_Manana_Puede_Sin_Repartir_Nada()
    {
        /* COMPATIBILIDAD, y es obligatoria: las claves reportes.centros.* YA
           estan repartidas en las dos bases. `reportes.ver_modulo` va incluido
           porque esa familia tiene puerta de modulo, igual que hoy. */
        var puede = Con("reportes.ver_modulo", PermisosDeCentros.Cambiar, PermisosDeCentros.Ver(2004));

        Assert.True(puede.PuedeCambiar);
        Assert.True(puede.PuedeVer(2004));
    }

    [Fact]
    public void Operar_NO_Se_Deduce_De_Ver()
    {
        var puede = Con(ClavesDeCentros.Cambiar, ClavesDeCentros.Ver(2004));

        Assert.True(puede.PuedeVer(2004));
        Assert.False(puede.PuedeOperar(2004));   // se concede a mano
    }

    [Fact]
    public void La_Clave_Vieja_Nunca_Concede_Operar()
    {
        /* No existe ninguna clave vieja que signifique "puede escribir en ese
           centro", asi que no hay nada que heredar. Si esto dejara pasar,
           doce mil permisos de consulta se convertirian en permisos de
           escritura que nadie concedio. */
        var puede = Con("reportes.ver_modulo", PermisosDeCentros.Cambiar, PermisosDeCentros.Ver(2004));
        Assert.False(puede.PuedeOperar(2004));
    }

    [Fact]
    public void Sin_El_Interruptor_De_Arriba_No_Se_Cambia_De_Centro()
    {
        var puede = Con(ClavesDeCentros.Ver(2004));
        Assert.False(puede.PuedeCambiar);
    }

    [Fact]
    public void Un_Centro_No_Concedido_No_Se_Ve_Aunque_Otro_Si()
    {
        var puede = Con(ClavesDeCentros.Cambiar, ClavesDeCentros.Ver(2004));
        Assert.True(puede.PuedeVer(2004));
        Assert.False(puede.PuedeVer(2005));
    }

    [Fact]
    public void La_Matriz_Sigue_Funcionando_Para_Quien_No_Tiene_Ninguna_Clave_De_Centros()
    {
        /* Nadie tiene hoy claves centros.matriz.*. Si la matriz empezara a
           exigirlas, el dia del despliegue la aplicacion se quedaria en blanco
           para todos. */
        var puede = Con();
        Assert.True(puede.PuedeVerLaMatriz);
        Assert.True(puede.PuedeOperarEnLaMatriz);
    }

    [Fact]
    public void El_Dia_Que_Se_Configura_La_Clave_De_Matriz_Empieza_A_Gobernar()
    {
        var soloConsulta = Con(ClavesDeCentros.MatrizVer);
        Assert.True(soloConsulta.PuedeVerLaMatriz);
        Assert.False(soloConsulta.PuedeOperarEnLaMatriz);

        var opera = Con(ClavesDeCentros.MatrizOperar);
        Assert.True(opera.PuedeOperarEnLaMatriz);
    }

    [Fact]
    public void El_SuperAdmin_Y_La_Instalacion_Sin_Modelo_De_Permisos_Pasan()
    {
        var puede = new CapacidadesDeCentros(true, Array.Empty<string>(), _ => false);

        Assert.True(puede.PuedeCambiar);
        Assert.True(puede.PuedeVer(2004));
        Assert.True(puede.PuedeOperar(2004));
        Assert.True(puede.PuedeOperarEnLaMatriz);
    }

    [Fact]
    public void Las_Claves_Se_Llaman_Como_Dice_El_Contrato()
    {
        Assert.Equal("centros.cambiar", ClavesDeCentros.Cambiar);
        Assert.Equal("centros.2002.ver", ClavesDeCentros.Ver(2002));
        Assert.Equal("centros.2002.operar", ClavesDeCentros.Operar(2002));
        Assert.Equal("centros.matriz.ver", ClavesDeCentros.MatrizVer);
        Assert.Equal("centros.matriz.operar", ClavesDeCentros.MatrizOperar);
    }
}
