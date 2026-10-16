using System.Reflection;
using ISL_Service.Application.Centros;
using ISL_Service.Controllers;
using Xunit;

namespace ISL_Service.Tests;

/*
  LOS DOS CAMBIOS QUE EL MODULO DE COBRANZA DEL MOVIL NECESITO DEL BACKEND.

  Las dos cosas se midieron contra las copias locales de MacZ y de
  Produccion_svr, y las dos tienen el mismo riesgo si alguien las deshace sin
  darse cuenta: no revientan, dejan de proteger.
*/
public class CobranzaEnElMovilTests
{
    private static string[] Leer(string campo)
    {
        var f = typeof(CobranzasController).GetField(
            campo,
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string[])f.GetValue(null)!;
    }

    // ─────────────── 1. LA LISTA Y EL DETALLE NO PIDEN LO MISMO ───────────────

    [Fact]
    public void La_lista_acepta_la_clave_que_deriva_de_btnBuscar()
    {
        /*
          EL DESAJUSTE, MEDIDO.

          El puente deriva `cobranzas.ver` de `btnVer` de la forma CONSULTA DE
          COBRANZAS de Mac31, y `btnVer` alla es EL DETALLE. La LISTA es
          `btnBuscar`, que deriva `cobranzas.consultar` —una clave que este
          controlador no pedia—. Medido en las dos bases:

              btnBuscar sin btnVer sobre ConsultarCobranzas
                MacZ           -> KARLA   (1 persona)
                Produccion_svr -> ninguna

          O sea: una persona que en Mac31 puede BUSCAR cobranzas recibia un 403
          en la lista de la pantalla que si tiene.
        */
        Assert.Contains("cobranzas.consultar", Leer("Ver"));
    }

    [Fact]
    public void El_detalle_NO_acepta_la_clave_de_la_lista()
    {
        /*
          Y NO AL CONTRARIO, que es la otra mitad del arreglo. El detalle SI es
          `btnVer`: aceptar ahi `cobranzas.consultar` le daria el detalle a quien
          en Mac31 solo tiene BUSCAR, que es un permiso que esa persona no tiene.
          Arreglar un desajuste abriendo el otro lado no es arreglarlo.
        */
        Assert.DoesNotContain("cobranzas.consultar", Leer("VerDetalle"));
        Assert.Contains("cobranzas.ver", Leer("VerDetalle"));
    }

    [Theory]
    [InlineData("Ver", "app_movil.cobranzas.ver")]
    [InlineData("VerDetalle", "app_movil.cobranzas.ver")]
    [InlineData("Crear", "app_movil.cobranzas.crear")]
    [InlineData("Editar", "app_movil.cobranzas.editar")]
    [InlineData("Cancelar", "app_movil.cobranzas.cancelar")]
    [InlineData("VerPendientes", "app_movil.cobranzas.pendientes.ver")]
    public void Cada_permiso_acepta_tambien_la_clave_del_canal_movil(
        string campo, string claveDelMovil)
    {
        // Un endpoint sirve a los dos canales y cada canal tiene su propia
        // clave. `CanalDeAcceso.Filtrar` tira las `app_movil.*` cuando el token
        // es de canal web, asi que esto no abre nada en el navegador; y hoy no
        // las tiene nadie, porque todavia no estan sembradas.
        Assert.Contains(claveDelMovil, Leer(campo));
    }

    [Fact]
    public void Las_claves_del_web_siguen_valiendo()
    {
        // NADIE PIERDE EL ACCESO QUE YA TENIA. Es la regla de compatibilidad de
        // todo este encargo y aqui se comprueba endpoint por endpoint.
        Assert.Contains("cobranzas.ver", Leer("Ver"));
        Assert.Contains("cobranzas.crear", Leer("Crear"));
        Assert.Contains("cobranzas.editar", Leer("Editar"));
        Assert.Contains("cobranzas.cancelar", Leer("Cancelar"));
        Assert.Contains("cobranzas.pendientes.ver", Leer("VerPendientes"));
    }

    // ───────────────── 2. EL MODULO NO SABE DE CENTROS ─────────────────

    [Fact]
    public void Cobranzas_NO_esta_declarado_como_compatible_con_centros()
    {
        /*
          Y ES A PROPOSITO, NO UN PENDIENTE.

          `CobranzasRepository.GetConnection()` arma su cadena a mano desde
          `ConnectionString("Main")`: no pasa por `IConexionDelCentro`.
          Declararlo aqui sin que el repositorio lo adopte haria que una
          peticion con `X-Centro != 0` se atendiera contra la MATRIZ y
          contestara 200 — datos de la matriz diciendo que son del centro, que
          es justo lo que el punto 5 del contrato prohibe. Y cobrar en la base
          equivocada no se deshace con un boton.

          Si alguien lo declara, esta prueba se cae y hay que leer esto antes de
          borrarla.
        */
        Assert.Null(ModulosConCentros.Buscar("cobranzas"));
    }

    [Fact]
    public void El_guardia_de_centro_existe_en_el_controlador()
    {
        /*
          EL CANDADO ES DEL SERVIDOR Y NO SOLO DEL CLIENTE.

          El movil ya no pide nada fuera de la matriz, pero `ApiClient` pone
          `X-Centro` en TODAS las peticiones: un APK viejo, o cualquier modulo
          futuro que comparta ese cliente, si las mandaria. Sin este metodo la
          peticion se atendia contra la matriz y contestaba 200.
        */
        var guardia = typeof(CobranzasController).GetMethod(
            "RechazarSiEsOtroCentro",
            BindingFlags.NonPublic | BindingFlags.Instance);

        Assert.NotNull(guardia);
    }

    [Fact]
    public void El_motivo_del_409_dice_que_hacer_y_no_solo_que_no_se_pudo()
    {
        /*
          EL `detail` ES DONDE VIAJA LA FRASE ACCIONABLE.

          El sobre de error de esta casa es `{code, message, detail}` y los
          clientes pintan el `detail`. Un motivo vacio deja a la persona con un
          "no se pudo" y nada mas; este dice por que no se puede y cual es la
          salida.
        */
        var campo = typeof(CobranzasController).GetField(
            "MotivoSinCentros",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var motivo = (string)campo.GetRawConstantValue()!;

        Assert.Contains("matriz", motivo, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Vuelve", motivo, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Un_centro_basura_no_se_interpreta_como_matriz()
    {
        // Lo mismo que ya cuida `CentrosReglasTests`, repetido aqui porque este
        // modulo depende de ello: si "abc" se redondeara a 0, un cliente con un
        // bug mandaria a cobrar a la matriz creyendo que esta en el centro.
        Assert.False(CentroPedido.Interpretar("abc", out _));
        Assert.False(CentroPedido.Interpretar("-3", out _));
        Assert.True(CentroPedido.Interpretar("", out var vacio));
        Assert.Equal(CentroPedido.Matriz, vacio);
    }
}
