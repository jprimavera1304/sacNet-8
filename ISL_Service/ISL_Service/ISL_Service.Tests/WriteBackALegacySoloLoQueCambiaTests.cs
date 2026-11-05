using ISL_Service.Infrastructure.Security;
using Xunit;

namespace ISL_Service.Tests;

/*
  ══════════════════════════════════════════════════════════════════════════
  EL WRITE-BACK A MAC31 ESCRIBE SOLO LO QUE CAMBIA
  ══════════════════════════════════════════════════════════════════════════

  EL PROBLEMA QUE SE ARREGLO, MEDIDO
  ----------------------------------
  Guardar permisos de UN usuario llamaba a
  `sp_n_ActualizarUsuarioFormaProceso` una vez por cada entrada del CATALOGO,
  no por cada cambio: ~330 viajes por guardado (hay 330 procesos activos en
  MacZ y 332 en Produccion_svr), cada uno con su propia transaccion dentro del
  procedimiento. Mover UN permiso costaba lo mismo que mover un modulo entero.

  Medido con `sys.dm_exec_procedure_stats` corriendo
  `SincronizacionBidireccionalDeTodasLasFamiliasTests` contra MacZ:
  33,343 ejecuciones del procedimiento antes, 155 despues. La misma prueba,
  el mismo resultado final, y de 41 s a 19 s.

  QUE SE PRUEBA AQUI Y POR QUE SIN BASE
  -------------------------------------
  La decision entera es "comparar lo que se quiere dejar contra lo que ya hay",
  y eso es una funcion sobre dos diccionarios. Probarla aqui deja los casos
  limite escritos y legibles; que el resultado FINAL en la base siga siendo el
  correcto lo mide `SincronizacionBidireccionalDeTodasLasFamiliasTests`, que
  corre las cuatro direcciones contra las dos empresas de verdad.

  Y DESDE EL 2026-11-04, UNA REGLA MAS QUE SE VE AQUI
  ---------------------------------------------------
  De este write-back ya no sale NINGUNA baja: guardar permisos desde el web
  solo puede OTORGAR en Mac31. Varias pruebas de este archivo afirmaban antes
  lo contrario y se cambiaron, porque lo que afirmaban es exactamente lo que
  apago 46 permisos a tres personas. El por que esta entero en
  GuardarEnElWebNuncaRevocaEnMac31Tests.

  LO QUE **NO** ES ESTE ARREGLO
  -----------------------------
  No es guardado optimista: cuando la pantalla diga "guardado", la escritura
  esta confirmada —se espera el lote—. No es una cache de permisos: el estado
  de Mac31 se recuerda durante UNA peticion y no participa en decidir si
  alguien tiene un permiso, solo en decidir si hace falta reescribir una fila
  que ya dice lo mismo. Y el esquema de autorizacion no se toca.
*/
public class WriteBackALegacySoloLoQueCambiaTests
{
    private static List<KeyValuePair<int, int>> Cambios(
        Dictionary<int, int> deseado, Dictionary<int, int> actual)
        => PermissionService.CambiosQueHacenFalta(deseado, actual);

    /// <summary>
    /// EL CASO NORMAL, QUE ES EL 97%: no cambia nada, no se escribe nada.
    /// Antes esto eran ~330 viajes para dejar la base exactamente igual.
    /// </summary>
    [Fact]
    public void Si_Nada_Cambia_No_Se_Escribe_Nada()
    {
        var actual = new Dictionary<int, int> { [10] = 1, [11] = 1, [12] = 2, [13] = 2 };
        var deseado = new Dictionary<int, int> { [10] = 1, [11] = 1, [12] = 2, [13] = 2 };

        Assert.Empty(Cambios(deseado, actual));
    }

    /// <summary>
    /// MOVER UN PERMISO CUESTA UN VIAJE, no el catalogo entero. Es la frase
    /// que resume el arreglo.
    /// </summary>
    [Fact]
    public void Mover_Un_Permiso_Es_Un_Solo_Cambio()
    {
        var actual = new Dictionary<int, int>();
        var deseado = new Dictionary<int, int>();
        for (var proceso = 1; proceso <= 330; proceso++)
        {
            actual[proceso] = 2;
            deseado[proceso] = 2;
        }

        // Se le concede UNO de los 330.
        deseado[200] = 1;

        var cambios = Cambios(deseado, actual);

        var unico = Assert.Single(cambios);
        Assert.Equal(200, unico.Key);
        Assert.Equal(1, unico.Value);
    }

    [Fact]
    public void Conceder_Lo_Que_Estaba_Dado_De_Baja_Si_Se_Escribe()
    {
        var cambios = Cambios(
            deseado: new Dictionary<int, int> { [10] = 1 },
            actual: new Dictionary<int, int> { [10] = 2 });

        Assert.Equal(new[] { new KeyValuePair<int, int>(10, 1) }, cambios);
    }

    /// <summary>
    /// QUITAR LO QUE ESTABA ACTIVO **YA NO SE ESCRIBE**, y es el cambio del
    /// 2026-11-04: el guardado del web no puede bajar un IDStatus por ningun
    /// camino. Antes esta prueba afirmaba lo contrario; lo que afirmaba es
    /// justo lo que apago 46 permisos de Mac31 a tres personas. Ver
    /// GuardarEnElWebNuncaRevocaEnMac31Tests para el porque completo.
    /// </summary>
    [Fact]
    public void Quitar_Lo_Que_Estaba_Activo_Ya_No_Se_Escribe()
    {
        Assert.Empty(Cambios(
            deseado: new Dictionary<int, int> { [10] = 2 },
            actual: new Dictionary<int, int> { [10] = 1 }));
    }

    /// <summary>
    /// CONCEDER ALGO QUE NO TENIA FILA SI SE ESCRIBE: es el alta, y el
    /// procedimiento la inserta.
    /// </summary>
    [Fact]
    public void Conceder_Sin_Fila_Previa_Si_Se_Escribe()
    {
        var cambios = Cambios(
            deseado: new Dictionary<int, int> { [99] = 1 },
            actual: new Dictionary<int, int>());

        Assert.Equal(new[] { new KeyValuePair<int, int>(99, 1) }, cambios);
    }

    /// <summary>
    /// Y QUITAR ALGO QUE NO TENIA FILA **NO** SE ESCRIBE, porque el
    /// procedimiento no insertaria nada: con IDStatus = 2 y sin fila previa no
    /// hace nada. El viaje tenia efecto cero.
    ///
    /// Esto es un ahorro grande y no un detalle: un repartidor tiene fila para
    /// una fraccion del catalogo, asi que la mayoria de las "bajas" que se
    /// calculaban eran bajas de filas que no existian.
    /// </summary>
    [Fact]
    public void Quitar_Sin_Fila_Previa_No_Se_Escribe()
    {
        Assert.Empty(Cambios(
            deseado: new Dictionary<int, int> { [99] = 2 },
            actual: new Dictionary<int, int>()));
    }

    /// <summary>
    /// UN ESTADO RARO SIN FILA TAMPOCO SE ESCRIBE. Solo el 1 concede, asi que
    /// cualquier otro valor es una baja y sin fila previa no hace nada.
    /// Esta fijado para que nadie "simplifique" la comparacion a `== 2`: con
    /// un `!= 2` un valor raro insertaria fila.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(-1)]
    public void Un_Estado_Que_No_Concede_Y_Sin_Fila_No_Se_Escribe(int status)
    {
        Assert.Empty(Cambios(
            deseado: new Dictionary<int, int> { [99] = status },
            actual: new Dictionary<int, int>()));
    }

    /// <summary>
    /// LA TRAMPA DE LAS CINCO PASADAS, FIJADA.
    ///
    /// A este write-back se entra CINCO veces en el mismo guardado, y un mismo
    /// IDProceso puede caer en dos catalogos: esta documentado en
    /// UpsertUserOverridesAsync ("si alguna clave cayera en dos catalogos,
    /// mande el calculo de la familia", que va ultimo).
    ///
    /// Si el estado recordado NO se pusiera al dia tras escribir, la quinta
    /// pasada compararia contra la foto de antes y podria decidir "no hace
    /// falta tocarlo" cuando si hace falta — se quedaria lo que escribio la
    /// primera y ganaria la pasada que no debe. Aqui se reproduce esa
    /// secuencia: con el estado al dia, la ultima sigue mandando.
    /// </summary>
    [Fact]
    public void Con_El_Estado_Al_Dia_No_Se_Reescribe_Lo_Ya_Escrito()
    {
        // En la base el proceso 7 esta DE BAJA.
        var estado = new Dictionary<int, int> { [7] = 2 };

        // Primera pasada: un catalogo lo concede.
        var primera = Cambios(new Dictionary<int, int> { [7] = 1 }, estado);
        Assert.Equal(new[] { new KeyValuePair<int, int>(7, 1) }, primera);

        // Se escribe, y el estado recordado se pone al dia (es lo que hace
        // EscribirCambiosLegacyAsync al final).
        foreach (var c in primera) estado[c.Key] = c.Value;

        // Las otras cuatro pasadas del mismo guardado ya no tienen nada que
        // hacer: la fila dice lo que tiene que decir. Sin poner al dia el
        // estado recordado, cada una habria vuelto a escribirla.
        Assert.Empty(Cambios(new Dictionary<int, int> { [7] = 1 }, estado));
    }

    /// <summary>
    /// Y AL CONTRARIO, para que quede claro que lo que se quito son viajes y
    /// no decisiones: si la ultima pasada pide lo MISMO que ya quedo escrito,
    /// no se vuelve a escribir. Antes esa era una de las 330 reescrituras.
    /// </summary>
    [Fact]
    public void Si_La_Ultima_Pasada_Pide_Lo_Mismo_No_Se_Reescribe()
    {
        var estado = new Dictionary<int, int> { [7] = 1 };

        Assert.Empty(Cambios(new Dictionary<int, int> { [7] = 1 }, estado));
    }

    /// <summary>
    /// Un proceso que el catalogo NO menciona no se toca. Es el candado de
    /// siempre visto desde aqui: lo que la pantalla no enseno, no se cambia.
    /// </summary>
    [Fact]
    public void Lo_Que_No_Esta_En_Lo_Deseado_No_Se_Toca()
    {
        var actual = new Dictionary<int, int> { [10] = 1, [11] = 2, [12] = 1 };

        var cambios = Cambios(new Dictionary<int, int> { [11] = 1 }, actual);

        var unico = Assert.Single(cambios);
        Assert.Equal(11, unico.Key);
        Assert.DoesNotContain(cambios, c => c.Key is 10 or 12);
    }
}
