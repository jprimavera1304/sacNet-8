using ISL_Service.Application.DTOs.RecepcionCascos;
using ISL_Service.Application.Services;
using ISL_Service.Infrastructure.Reports;
using ISL_Service.Infrastructure.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Xunit.Abstractions;

namespace ISL_Service.Tests;

/// <summary>
/// Recepcion de cascos usados, contra la base LOCAL de Tauro.
///
/// POR QUE CONTRA LA BASE Y NO CON DOBLES
/// Porque lo que hay que probar vive en la base: los totales los suma el
/// procedimiento, la bitacora la escribe el procedimiento, la atomicidad de la
/// cancelacion multiple es una transaccion de SQL Server y los CHECK son
/// restricciones de tabla. Con un repositorio falso se probaria que mi codigo
/// llama a mi codigo.
///
/// LO QUE ESCRIBE Y COMO LO LIMPIA
/// Solo en las tres tablas de este modulo, que son nuestras y nacieron vacias.
/// Cada prueba marca sus renglones en Observaciones con una firma propia y los
/// borra en un finally, pase lo que pase. No toca ninguna tabla de legacy: de
/// Agentes y [Catalogo TiposUsados] solo lee.
///
/// Y SOLO EN Produccion_svr. Este modulo es de Tauro; la ultima prueba
/// comprueba justamente que en MacZ (Zaragoza) no existe nada de esto.
/// </summary>
public class RecepcionCascosContraLaBaseTests
{
    private const string BaseTauro = "Produccion_svr";
    private const string BaseZaragoza = "MacZ";

    /* Tipos reales del catalogo de Tauro, verificados: 6 MINI CHICO (Orden 1),
       1 CHICO (2), 8 MOTO (8). Se usan por ID y nunca por nombre, porque el
       nombre del mismo ID cambia entre empresas. */
    private const int TipoMiniChico = 6;
    private const int TipoChico = 1;
    private const int TipoMoto = 8;

    private const int OrigenAgente = 1;
    private const int OrigenTienda = 2;
    private const int OrigenGallero = 3;

    private const int UsuarioDePrueba = 3;

    private readonly ITestOutputHelper _out;
    public RecepcionCascosContraLaBaseTests(ITestOutputHelper salida) => _out = salida;

    private static string Cadena(string baseDatos, string firma) =>
        $"Server=LAP-JUAND;Database={baseDatos};User Id=sa;Password=123;Encrypt=False;TrustServerCertificate=True;Application Name={firma};";

    private static RecepcionCascosRepository Repositorio(string baseDatos, string firma)
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Main"] = Cadena(baseDatos, firma)
            })
            .Build();
        return new RecepcionCascosRepository(configuracion);
    }

    private static RecepcionCascosService Servicio(string firma)
        => new(Repositorio(BaseTauro, firma));

    // ==================================================================
    // 1. EL CASO BUENO
    // ==================================================================

    /// <summary>
    /// Se captura una recepcion de tres tamanos y queda con su detalle, su
    /// total sumado por la base y una entrada de bitacora por tamano.
    /// </summary>
    [Fact]
    public async Task Una_Recepcion_Se_Guarda_Con_Su_Total_Y_Su_Bitacora()
    {
        var firma = Firma();
        var servicio = Servicio(firma);
        var agente = await UnAgenteActivoAsync(firma);

        try
        {
            var alta = await servicio.CrearAsync(new CrearRecepcionCascoRequest
            {
                Fecha = DateTime.Today,
                IdOrigen = OrigenAgente,
                IdAgente = agente,
                Observaciones = firma,
                Piezas = new()
                {
                    new() { IdTipoUsado = TipoMiniChico, Piezas = 5 },
                    new() { IdTipoUsado = TipoChico, Piezas = 3 },
                    new() { IdTipoUsado = TipoMoto, Piezas = 10 }
                }
            }, UsuarioDePrueba, "PRUEBA", "WEB");

            Assert.True(alta.ok, alta.mensaje);
            Assert.True(alta.idRecepcion > 0);

            var lista = await servicio.ConsultarAsync(
                DateTime.Today, DateTime.Today, null, agente, null, false);
            var mia = lista.Single(r => r.idRecepcion == alta.idRecepcion);

            /* 5 + 3 + 10. El total NO se mando: lo sumo el procedimiento. */
            Assert.Equal(18, mia.totalPiezas);
            Assert.Equal(3, mia.tipos);
            Assert.Equal(1, mia.estatus);
            Assert.Equal("AGENTE", mia.origen);
            Assert.False(string.IsNullOrWhiteSpace(mia.agente));
            Assert.Equal("WEB", mia.canal);

            /* El detalle trae los 8 tamanos del catalogo, con cero en los que
               no se capturaron: es lo que necesita la rejilla de captura. */
            var detalle = await servicio.ConsultarDetalleAsync(alta.idRecepcion);
            Assert.Equal(8, detalle.Count);
            Assert.Equal(5, detalle.Single(d => d.idTipoUsado == TipoMiniChico).piezas);
            Assert.Equal(0, detalle.Single(d => d.idTipoUsado == 7).piezas);
            /* Y en el orden de la hoja, no alfabetico. */
            Assert.Equal(detalle.OrderBy(d => d.orden).Select(d => d.idTipoUsado),
                         detalle.Select(d => d.idTipoUsado));

            var bitacora = await servicio.ConsultarBitacoraAsync(alta.idRecepcion);
            Assert.Equal(3, bitacora.Count);
            Assert.All(bitacora, b => Assert.Equal("ALTA", b.operacion));
            Assert.All(bitacora, b => Assert.Equal("PRUEBA", b.usuario));
            Assert.Equal(5, bitacora.Single(b => b.idTipoUsado == TipoMiniChico).piezasNueva);
        }
        finally
        {
            await LimpiarAsync(firma);
        }
    }

    /// <summary>
    /// El mismo tamano mandado dos veces en la misma peticion se SUMA, no
    /// truena. Pasa cuando el front manda la rejilla completa y un renglon
    /// viene repetido; sin esto, la llave primaria del parametro de tabla falla
    /// con un error que no le dice nada a nadie.
    /// </summary>
    [Fact]
    public async Task Un_Tamano_Repetido_En_La_Peticion_Se_Suma()
    {
        var firma = Firma();
        var servicio = Servicio(firma);
        var agente = await UnAgenteActivoAsync(firma);

        try
        {
            var alta = await servicio.CrearAsync(new CrearRecepcionCascoRequest
            {
                Fecha = DateTime.Today,
                IdOrigen = OrigenAgente,
                IdAgente = agente,
                Observaciones = firma,
                Piezas = new()
                {
                    new() { IdTipoUsado = TipoChico, Piezas = 2 },
                    new() { IdTipoUsado = TipoChico, Piezas = 3 },
                    /* Los ceros se tiran, no se guardan como renglon. */
                    new() { IdTipoUsado = TipoMoto, Piezas = 0 }
                }
            }, UsuarioDePrueba, "PRUEBA", "WEB");

            Assert.True(alta.ok, alta.mensaje);

            var detalle = await servicio.ConsultarDetalleAsync(alta.idRecepcion);
            Assert.Equal(5, detalle.Single(d => d.idTipoUsado == TipoChico).piezas);
            Assert.Equal(0, detalle.Single(d => d.idTipoUsado == TipoMoto).piezas);
        }
        finally
        {
            await LimpiarAsync(firma);
        }
    }

    /// <summary>
    /// Dos recepciones del mismo agente el mismo dia SE GUARDAN y la segunda
    /// avisa. No se prohibe porque no hay ninguna regla en el sistema que lo
    /// prohiba; se avisa porque capturar dos veces la misma es el error que
    /// descuadra la hoja.
    /// </summary>
    [Fact]
    public async Task Dos_Recepciones_Del_Mismo_Agente_El_Mismo_Dia_Avisan_Pero_Se_Guardan()
    {
        var firma = Firma();
        var servicio = Servicio(firma);
        var agente = await UnAgenteActivoAsync(firma);

        /*
            UN DIA PROPIO, Y NO HOY.

            La prueba afirma que la PRIMERA recepcion no avisa, y eso solo es
            cierto si ese agente no tiene ya otra ese dia. Con hoy fallaba en
            cuanto la base local tenia datos de ejemplo: el aviso salia desde
            la primera y la prueba acusaba al codigo de algo que hacia bien.
            Un dia de hace mes y medio es de esta prueba y de nadie mas.
        */
        var dia = DateTime.Today.AddDays(-45);

        CrearRecepcionCascoRequest DeEseDia() => new()
        {
            Fecha = dia,
            IdOrigen = OrigenAgente,
            IdAgente = agente,
            Observaciones = firma,
            Piezas = new() { new() { IdTipoUsado = TipoChico, Piezas = 1 } }
        };

        try
        {
            var primera = await servicio.CrearAsync(DeEseDia(), UsuarioDePrueba, "PRUEBA", "WEB");
            Assert.True(primera.ok, primera.mensaje);
            Assert.True(string.IsNullOrWhiteSpace(primera.advertencia),
                $"La primera no deberia avisar y aviso: {primera.advertencia}");

            var segunda = await servicio.CrearAsync(DeEseDia(), UsuarioDePrueba, "PRUEBA", "WEB");
            Assert.True(segunda.ok, segunda.mensaje);
            Assert.Contains("ya tiene una recepcion", segunda.advertencia ?? "");
            Assert.True(segunda.idRecepcion > 0);
        }
        finally
        {
            await LimpiarAsync(firma);
        }
    }

    // ==================================================================
    // 2. LO QUE NO SE DEJA GUARDAR
    // ==================================================================

    /// <summary>
    /// Las cuatro formas de mandar una recepcion que no se puede guardar. Se
    /// prueban juntas porque todas comparten la misma consecuencia: no se
    /// escribe nada y el mensaje es para el empleado.
    /// </summary>
    [Theory]
    [InlineData("sin fecha")]
    [InlineData("fecha futura")]
    [InlineData("sin piezas")]
    [InlineData("tienda sin referencia")]
    [InlineData("origen que no existe")]
    [InlineData("agente dado de baja")]
    [InlineData("tamano que no existe")]
    public async Task Lo_Que_No_Cuadra_No_Se_Guarda(string caso)
    {
        var firma = Firma();
        var servicio = Servicio(firma);
        var agente = await UnAgenteActivoAsync(firma);

        var peticion = Basica(agente, firma);

        switch (caso)
        {
            case "sin fecha":
                peticion.Fecha = null; break;
            case "fecha futura":
                peticion.Fecha = DateTime.Today.AddDays(1); break;
            case "sin piezas":
                peticion.Piezas = new(); break;
            case "tienda sin referencia":
                peticion.IdOrigen = OrigenTienda; peticion.IdAgente = null; peticion.Referencia = "   "; break;
            case "origen que no existe":
                peticion.IdOrigen = 9; break;
            case "agente dado de baja":
                peticion.IdAgente = await UnAgenteInactivoAsync(firma); break;
            case "tamano que no existe":
                peticion.Piezas = new() { new() { IdTipoUsado = 999999, Piezas = 4 } }; break;
        }

        try
        {
            var resultado = await servicio.CrearAsync(peticion, UsuarioDePrueba, "PRUEBA", "WEB");

            Assert.False(resultado.ok, $"'{caso}' se guardo y no debia.");
            Assert.Equal(0, resultado.idRecepcion);
            Assert.False(string.IsNullOrWhiteSpace(resultado.mensaje));
            /* El mensaje es para el empleado: no puede nombrar una tabla, un
               procedimiento ni una clave de permiso. */
            Assert.DoesNotContain("sp_", resultado.mensaje!, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("WRecepcion", resultado.mensaje!, StringComparison.OrdinalIgnoreCase);
            _out.WriteLine($"{caso} -> {resultado.mensaje}");

            Assert.Equal(0, await CuantasAsync(firma));
        }
        finally
        {
            await LimpiarAsync(firma);
        }
    }

    /// <summary>
    /// Un usuario que el token no identifica no puede capturar. Es la ultima
    /// red: el backend ya saca el usuario del token, pero si llegara en cero el
    /// procedimiento no deja la recepcion sin dueno.
    /// </summary>
    [Fact]
    public async Task Sin_Usuario_Identificado_No_Se_Guarda()
    {
        var firma = Firma();
        var repositorio = Repositorio(BaseTauro, firma);
        var agente = await UnAgenteActivoAsync(firma);

        try
        {
            var resultado = await repositorio.InsertarAsync(
                Basica(agente, firma), 0, "", "WEB");

            Assert.False(resultado.ok);
            Assert.Contains("usuario", resultado.mensaje ?? "", StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, await CuantasAsync(firma));
        }
        finally
        {
            await LimpiarAsync(firma);
        }
    }

    /// <summary>
    /// La tabla no se deja llenar a mano con un origen y un agente que se
    /// contradicen, ni con piezas en cero. Son CHECK de tabla: valen aunque
    /// alguien escriba por fuera del procedimiento.
    /// </summary>
    [Fact]
    public async Task Los_Candados_De_La_Tabla_Aguantan_Una_Escritura_Directa()
    {
        var firma = Firma();

        await using var conexion = new SqlConnection(Cadena(BaseTauro, firma));
        await conexion.OpenAsync();

        try
        {
            /* Origen TIENDA con agente pegado: saldria contado dos veces. */
            await Assert.ThrowsAsync<SqlException>(async () =>
            {
                await using var cmd = conexion.CreateCommand();
                cmd.CommandText = @"
INSERT INTO dbo.WRecepcionCasco
      (Fecha, IdOrigen, IDAgente, Referencia, Observaciones, IDUsuarioCreacion, UsuarioCreacion)
VALUES (GETDATE(), 2, 1, N'X', @firma, 3, N'PRUEBA');";
                cmd.Parameters.AddWithValue("@firma", firma);
                await cmd.ExecuteNonQueryAsync();
            });

            /* Origen AGENTE sin agente. */
            await Assert.ThrowsAsync<SqlException>(async () =>
            {
                await using var cmd = conexion.CreateCommand();
                cmd.CommandText = @"
INSERT INTO dbo.WRecepcionCasco
      (Fecha, IdOrigen, IDAgente, Observaciones, IDUsuarioCreacion, UsuarioCreacion)
VALUES (GETDATE(), 1, NULL, @firma, 3, N'PRUEBA');";
                cmd.Parameters.AddWithValue("@firma", firma);
                await cmd.ExecuteNonQueryAsync();
            });

            /* Gallero sin nombre: no hay catalogo del que sacarlo, asi que sin
               texto el renglon no identifica a nadie. */
            await Assert.ThrowsAsync<SqlException>(async () =>
            {
                await using var cmd = conexion.CreateCommand();
                cmd.CommandText = @"
INSERT INTO dbo.WRecepcionCasco
      (Fecha, IdOrigen, Referencia, Observaciones, IDUsuarioCreacion, UsuarioCreacion)
VALUES (GETDATE(), 3, NULL, @firma, 3, N'PRUEBA');";
                cmd.Parameters.AddWithValue("@firma", firma);
                await cmd.ExecuteNonQueryAsync();
            });

            Assert.Equal(0, await CuantasAsync(firma));
        }
        finally
        {
            await LimpiarAsync(firma);
        }
    }

    // ==================================================================
    // 3. MODIFICAR
    // ==================================================================

    /// <summary>
    /// La pregunta del negocio: "aqui habia 5 chicos y ahora dice 3, quien y
    /// cuando". La bitacora tiene que contestarla con el antes y el despues.
    /// </summary>
    [Fact]
    public async Task Al_Modificar_La_Bitacora_Guarda_El_Antes_Y_El_Despues()
    {
        var firma = Firma();
        var servicio = Servicio(firma);
        var agente = await UnAgenteActivoAsync(firma);

        try
        {
            var alta = await servicio.CrearAsync(new CrearRecepcionCascoRequest
            {
                Fecha = DateTime.Today,
                IdOrigen = OrigenAgente,
                IdAgente = agente,
                Observaciones = firma,
                Piezas = new()
                {
                    new() { IdTipoUsado = TipoMiniChico, Piezas = 5 },
                    new() { IdTipoUsado = TipoMoto, Piezas = 10 }
                }
            }, UsuarioDePrueba, "QUIEN CAPTURO", "WEB");
            Assert.True(alta.ok, alta.mensaje);

            var cambio = await servicio.ActualizarAsync(new ActualizarRecepcionCascoRequest
            {
                IdRecepcion = alta.idRecepcion,
                Fecha = DateTime.Today,
                IdOrigen = OrigenAgente,
                IdAgente = agente,
                Observaciones = firma,
                Piezas = new()
                {
                    /* 5 -> 3, se quita MOTO y se agrega CHICO. */
                    new() { IdTipoUsado = TipoMiniChico, Piezas = 3 },
                    new() { IdTipoUsado = TipoChico, Piezas = 4 }
                }
            }, 7, "QUIEN CORRIGIO", "MOVIL");

            Assert.True(cambio.ok, cambio.mensaje);

            var bitacora = await servicio.ConsultarBitacoraAsync(alta.idRecepcion);
            var cambios = bitacora.Where(b => b.operacion == "MODIFICACION").ToList();

            var bajo = cambios.Single(b => b.idTipoUsado == TipoMiniChico);
            Assert.Equal(5, bajo.piezasAnterior);
            Assert.Equal(3, bajo.piezasNueva);
            Assert.Equal("QUIEN CORRIGIO", bajo.usuario);
            Assert.Equal("MOVIL", bajo.canal);

            var quitado = cambios.Single(b => b.idTipoUsado == TipoMoto);
            Assert.Equal(10, quitado.piezasAnterior);
            Assert.Null(quitado.piezasNueva);

            var agregado = cambios.Single(b => b.idTipoUsado == TipoChico);
            Assert.Null(agregado.piezasAnterior);
            Assert.Equal(4, agregado.piezasNueva);

            /* Y el alta sigue ahi: modificar no borra el rastro de quien
               capturo. */
            Assert.Equal(2, bitacora.Count(b => b.operacion == "ALTA"));

            /* El total se recalculo: 3 + 4. */
            var lista = await servicio.ConsultarAsync(DateTime.Today, DateTime.Today, null, agente, null, false);
            Assert.Equal(7, lista.Single(r => r.idRecepcion == alta.idRecepcion).totalPiezas);
        }
        finally
        {
            await LimpiarAsync(firma);
        }
    }

    /// <summary>Una recepcion cancelada ya no se modifica.</summary>
    [Fact]
    public async Task Una_Cancelada_No_Se_Modifica()
    {
        var firma = Firma();
        var servicio = Servicio(firma);
        var agente = await UnAgenteActivoAsync(firma);

        try
        {
            var alta = await servicio.CrearAsync(Basica(agente, firma), UsuarioDePrueba, "PRUEBA", "WEB");
            Assert.True(alta.ok, alta.mensaje);

            var cancelada = await servicio.CancelarAsync(new CancelarRecepcionesCascoRequest
            {
                Ids = new() { alta.idRecepcion },
                Motivo = "Prueba"
            }, UsuarioDePrueba, "PRUEBA", "WEB");
            Assert.True(cancelada.ok, cancelada.mensaje);

            var intento = await servicio.ActualizarAsync(new ActualizarRecepcionCascoRequest
            {
                IdRecepcion = alta.idRecepcion,
                Fecha = DateTime.Today,
                IdOrigen = OrigenAgente,
                IdAgente = agente,
                Observaciones = firma,
                Piezas = new() { new() { IdTipoUsado = TipoChico, Piezas = 1 } }
            }, UsuarioDePrueba, "PRUEBA", "WEB");

            Assert.False(intento.ok);
            Assert.Contains("cancelada", intento.mensaje ?? "", StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await LimpiarAsync(firma);
        }
    }

    // ==================================================================
    // 4. CANCELAR, UNA Y VARIAS
    // ==================================================================

    /// <summary>
    /// Tres marcadas, una sola accion: las tres quedan canceladas con su
    /// motivo, y cada una con su propia entrada de bitacora. La trazabilidad es
    /// individual aunque la accion sea una.
    /// </summary>
    [Fact]
    public async Task Cancelar_Varias_Deja_Rastro_De_Cada_Una()
    {
        var firma = Firma();
        var servicio = Servicio(firma);
        var agente = await UnAgenteActivoAsync(firma);

        try
        {
            var ids = new List<int>();
            for (var i = 0; i < 3; i++)
            {
                var alta = await servicio.CrearAsync(Basica(agente, firma), UsuarioDePrueba, "PRUEBA", "WEB");
                Assert.True(alta.ok, alta.mensaje);
                ids.Add(alta.idRecepcion);
            }

            var resultado = await servicio.CancelarAsync(new CancelarRecepcionesCascoRequest
            {
                Ids = ids,
                Motivo = "Se capturaron de mas"
            }, 7, "QUIEN CANCELO", "WEB");

            Assert.True(resultado.ok, resultado.mensaje);
            Assert.Equal(3, resultado.canceladas);

            var todas = await servicio.ConsultarAsync(
                DateTime.Today, DateTime.Today, null, agente, null, true);

            foreach (var id in ids)
            {
                var r = todas.Single(x => x.idRecepcion == id);
                Assert.Equal(2, r.estatus);
                Assert.Equal("CANCELADA", r.estatusNombre);
                Assert.Equal("Se capturaron de mas", r.motivoCancelacion);
                Assert.Equal("QUIEN CANCELO", r.usuarioCancelacion);
                Assert.NotNull(r.fechaCancelacion);

                var bitacora = await servicio.ConsultarBitacoraAsync(id);
                var cancelaciones = bitacora.Where(b => b.operacion == "CANCELACION").ToList();
                Assert.Equal(2, cancelaciones.Count);   // el estatus y el motivo
                Assert.All(cancelaciones, b => Assert.Equal("QUIEN CANCELO", b.usuario));
            }

            /* Y la lista de todos los dias ya no las trae. */
            var vigentes = await servicio.ConsultarAsync(
                DateTime.Today, DateTime.Today, null, agente, null, false);
            Assert.DoesNotContain(vigentes, r => ids.Contains(r.idRecepcion));
        }
        finally
        {
            await LimpiarAsync(firma);
        }
    }

    /// <summary>
    /// O todas o ninguna. Si una de las marcadas ya estaba cancelada, no se
    /// cancela ni una: quien marco diez tiene que saber que una no era
    /// cancelable, no enterarse despues contando.
    /// </summary>
    [Fact]
    public async Task Cancelar_Varias_Es_Todo_O_Nada()
    {
        var firma = Firma();
        var servicio = Servicio(firma);
        var agente = await UnAgenteActivoAsync(firma);

        try
        {
            var buena = await servicio.CrearAsync(Basica(agente, firma), UsuarioDePrueba, "PRUEBA", "WEB");
            var yaCancelada = await servicio.CrearAsync(Basica(agente, firma), UsuarioDePrueba, "PRUEBA", "WEB");

            await servicio.CancelarAsync(new CancelarRecepcionesCascoRequest
            {
                Ids = new() { yaCancelada.idRecepcion },
                Motivo = "La primera"
            }, UsuarioDePrueba, "PRUEBA", "WEB");

            var intento = await servicio.CancelarAsync(new CancelarRecepcionesCascoRequest
            {
                Ids = new() { buena.idRecepcion, yaCancelada.idRecepcion },
                Motivo = "Las dos"
            }, UsuarioDePrueba, "PRUEBA", "WEB");

            Assert.False(intento.ok);
            Assert.Equal(0, intento.canceladas);

            /* La que si se podia cancelar sigue viva: no se quedo a medias. */
            var todas = await servicio.ConsultarAsync(
                DateTime.Today, DateTime.Today, null, agente, null, true);
            Assert.Equal(1, todas.Single(r => r.idRecepcion == buena.idRecepcion).estatus);

            /* Una que no existe tampoco cancela el resto. */
            var conFantasma = await servicio.CancelarAsync(new CancelarRecepcionesCascoRequest
            {
                Ids = new() { buena.idRecepcion, 999_999_999 },
                Motivo = "Con una que no existe"
            }, UsuarioDePrueba, "PRUEBA", "WEB");

            Assert.False(conFantasma.ok);
            todas = await servicio.ConsultarAsync(DateTime.Today, DateTime.Today, null, agente, null, true);
            Assert.Equal(1, todas.Single(r => r.idRecepcion == buena.idRecepcion).estatus);
        }
        finally
        {
            await LimpiarAsync(firma);
        }
    }

    /// <summary>Sin motivo no se cancela: el motivo es el rastro.</summary>
    [Fact]
    public async Task Cancelar_Sin_Motivo_No_Procede()
    {
        var firma = Firma();
        var servicio = Servicio(firma);
        var agente = await UnAgenteActivoAsync(firma);

        try
        {
            var alta = await servicio.CrearAsync(Basica(agente, firma), UsuarioDePrueba, "PRUEBA", "WEB");

            var sinMotivo = await servicio.CancelarAsync(new CancelarRecepcionesCascoRequest
            {
                Ids = new() { alta.idRecepcion },
                Motivo = "   "
            }, UsuarioDePrueba, "PRUEBA", "WEB");

            Assert.False(sinMotivo.ok);

            var sinNada = await servicio.CancelarAsync(new CancelarRecepcionesCascoRequest
            {
                Ids = new(),
                Motivo = "Algo"
            }, UsuarioDePrueba, "PRUEBA", "WEB");

            Assert.False(sinNada.ok);

            var viva = await servicio.ConsultarAsync(DateTime.Today, DateTime.Today, null, agente, null, true);
            Assert.Equal(1, viva.Single(r => r.idRecepcion == alta.idRecepcion).estatus);
        }
        finally
        {
            await LimpiarAsync(firma);
        }
    }

    // ==================================================================
    // 5. EL PAPEL
    // ==================================================================

    /// <summary>
    /// PRUEBA DE REGRESION DE UN ERROR REAL.
    ///
    /// El bloque de totales del reporte sumaba las recepciones CANCELADAS
    /// mientras el bloque de renglones no las imprimia: el papel y sus totales
    /// decian cosas distintas en la misma hoja. La causa era una cadena de
    /// LEFT JOIN donde el filtro de la cabecera vivia en el ON del segundo, asi
    /// que el renglon de detalle de una cancelada sobrevivia con la cabecera en
    /// NULL y su SUM se sumaba igual.
    /// </summary>
    [Fact]
    public async Task El_Reporte_No_Suma_Las_Canceladas()
    {
        var firma = Firma();
        var servicio = Servicio(firma);
        var agente = await UnAgenteActivoAsync(firma);
        var dia = DateTime.Today.AddDays(-3);

        try
        {
            var vive = await servicio.CrearAsync(new CrearRecepcionCascoRequest
            {
                Fecha = dia, IdOrigen = OrigenAgente, IdAgente = agente, Observaciones = firma,
                Piezas = new() { new() { IdTipoUsado = TipoChico, Piezas = 7 } }
            }, UsuarioDePrueba, "PRUEBA", "WEB");

            var muere = await servicio.CrearAsync(new CrearRecepcionCascoRequest
            {
                Fecha = dia, IdOrigen = OrigenAgente, IdAgente = agente, Observaciones = firma,
                Piezas = new() { new() { IdTipoUsado = TipoChico, Piezas = 100 } }
            }, UsuarioDePrueba, "PRUEBA", "WEB");

            await servicio.CancelarAsync(new CancelarRecepcionesCascoRequest
            {
                Ids = new() { muere.idRecepcion }, Motivo = "Se cancela"
            }, UsuarioDePrueba, "PRUEBA", "WEB");

            var datos = await servicio.ConsultarDatosDeReporteAsync(dia, dia);

            var mios = datos.Renglones.Where(r => r.idAgente == agente).ToList();
            Assert.Contains(mios, r => r.idRecepcion == vive.idRecepcion);
            Assert.DoesNotContain(mios, r => r.idRecepcion == muere.idRecepcion);

            /* Lo que importa: el total del tipo NO incluye las 100 piezas de la
               cancelada. Se compara contra la suma de lo que de verdad se
               imprime, que es el invariante que se rompio. */
            var total = datos.Totales.Single(t => t.idTipoUsado == TipoChico).piezas;
            var sumaDeRenglones = datos.Renglones
                .Where(r => r.idTipoUsado == TipoChico)
                .Sum(r => r.piezas);
            Assert.Equal(sumaDeRenglones, total);

            /* Y los 8 tamanos salen, aunque vengan en cero: la hoja tiene las
               ocho columnas siempre. */
            Assert.Equal(8, datos.Totales.Count);
            Assert.Equal(datos.Totales.OrderBy(t => t.orden).Select(t => t.idTipoUsado),
                         datos.Totales.Select(t => t.idTipoUsado));
        }
        finally
        {
            await LimpiarAsync(firma);
        }
    }

    /// <summary>
    /// El HTML del papel sale con las tres secciones de la hoja, los subtotales
    /// y el total general, y escapa lo que teclea una persona.
    /// </summary>
    [Fact]
    public async Task El_Papel_Trae_Las_Tres_Secciones_Y_Sus_Totales()
    {
        var firma = Firma();
        var servicio = Servicio(firma);
        var agente = await UnAgenteActivoAsync(firma);
        var dia = DateTime.Today.AddDays(-4);

        try
        {
            await servicio.CrearAsync(new CrearRecepcionCascoRequest
            {
                Fecha = dia, IdOrigen = OrigenAgente, IdAgente = agente, Observaciones = firma,
                Piezas = new() { new() { IdTipoUsado = TipoChico, Piezas = 4 } }
            }, UsuarioDePrueba, "PRUEBA", "WEB");

            await servicio.CrearAsync(new CrearRecepcionCascoRequest
            {
                Fecha = dia, IdOrigen = OrigenTienda, Referencia = "TIENDA \"EL & CENTRO\"",
                Observaciones = firma,
                Piezas = new() { new() { IdTipoUsado = TipoMiniChico, Piezas = 6 } }
            }, UsuarioDePrueba, "PRUEBA", "WEB");

            await servicio.CrearAsync(new CrearRecepcionCascoRequest
            {
                Fecha = dia, IdOrigen = OrigenGallero, Referencia = "GALLERO DE PRUEBA",
                Observaciones = firma,
                Piezas = new() { new() { IdTipoUsado = TipoMoto, Piezas = 2 } }
            }, UsuarioDePrueba, "PRUEBA", "WEB");

            var datos = await servicio.ConsultarDatosDeReporteAsync(dia, dia);
            var html = RecepcionCascosHtmlBuilder.Construir(datos);

            Assert.Contains("AGENTES", html);
            Assert.Contains("CASCOS RECIBIDOS DE TIENDAS", html);
            Assert.Contains("GALLEROS", html);
            Assert.Contains("TOTAL DE CASCOS RECIBIDOS", html);

            /* Lo que teclea una persona va escapado: un & suelto rompe el
               documento y wkhtmltopdf no dice nada. */
            Assert.Contains("TIENDA &quot;EL &amp; CENTRO&quot;", html);
            Assert.DoesNotContain("EL & CENTRO", html);

            /* Nada de variables CSS: el WebKit de wkhtmltopdf 0.12.4 las
               descarta en silencio y el color cae a negro. */
            Assert.DoesNotContain("var(--", html);

            /* Y el encabezado no promete un numero de pagina que no sabe
               sustituir. */
            var encabezado = RecepcionCascosHtmlBuilder.Encabezado(
                "EMPRESA", "", dia, dia, "", "", "QUIEN LO PIDIO");
            Assert.Contains("class=\"page\"", encabezado);
            Assert.DoesNotContain("[page]", encabezado);
            Assert.Contains("QUIEN LO PIDIO", encabezado);
        }
        finally
        {
            await LimpiarAsync(firma);
        }
    }

    /// <summary>
    /// Un periodo sin cascos tiene que DECIR que esta vacio. Una hoja con solo
    /// los rotulos de columna se lee como un reporte que no se imprimio bien, y
    /// alguien lo vuelve a sacar.
    /// </summary>
    [Fact]
    public async Task Un_Periodo_Vacio_Lo_Dice_En_El_Papel()
    {
        var firma = Firma();
        var servicio = Servicio(firma);

        /* Un dia de hace diez anos: la base local de Tauro llega a 2026 y ese
           dia no tiene ninguna recepcion de este modulo, que nacio vacio. */
        var dia = new DateTime(2016, 1, 2);
        var datos = await servicio.ConsultarDatosDeReporteAsync(dia, dia);

        Assert.Empty(datos.Renglones);

        var html = RecepcionCascosHtmlBuilder.Construir(datos);
        Assert.Contains("No hubo cascos recibidos en este periodo.", html);
        /* Las columnas se pintan igual: el papel vacio se parece al lleno. */
        Assert.Contains("QUIÉN ENTREGÓ", html);
    }

    /// <summary>
    /// EL PDF DE VERDAD. No basta con que el HTML diga lo que debe: el que
    /// imprime es wkhtmltopdf, y es el que se traga las variables CSS, el
    /// flexbox y los margenes que no caben.
    ///
    /// Esta prueba renderiza el papel de un periodo completo de la base local y
    /// comprueba que sale un PDF con varias hojas. Deja el archivo en
    /// artifacts/ para poder mirarlo: un PDF que "se genera" y sale con las
    /// columnas cortadas pasa cualquier prueba que solo mire los bytes.
    /// </summary>
    [Fact]
    public async Task El_Reporte_Sale_Como_PDF_De_Varias_Hojas()
    {
        var servicio = Servicio("zzQaRecepcionPdf");

        var hasta = DateTime.Today;
        var desde = hasta.AddDays(-4);

        var datos = await servicio.ConsultarDatosDeReporteAsync(desde, hasta);
        Assert.NotEmpty(datos.Renglones);

        var html = RecepcionCascosHtmlBuilder.Construir(datos);
        var encabezado = RecepcionCascosHtmlBuilder.Encabezado(
            "TAURO", "", desde, hasta, "", "", "PRUEBA");

        var pdf = await WkhtmltopdfHtmlPdfRenderer.RenderAsync(
            html, "Landscape", "Cascos usados recibidos", CancellationToken.None,
            new WkhtmltopdfHtmlPdfRenderer.Opciones
            {
                MargenSuperior = 24,
                EspacioEncabezado = 4,
                MargenInferior = 12,
                MargenIzquierdo = 10,
                MargenDerecho = 10,
                ImagenDpi = 900,
                PieIzquierdo = "TAURO",
                PieConLinea = false,
                PieFuente = "Tahoma",
                EncabezadoHtml = encabezado
            });

        /* Es un PDF de verdad, no una pagina de error. */
        Assert.True(pdf.Length > 5000, $"El PDF salio de {pdf.Length} bytes.");
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdf, 0, 4));

        /* Y tiene mas de una hoja: con cinco dias de recepciones, un PDF de una
           sola pagina significaria que se quedo a medias. */
        var crudo = System.Text.Encoding.Latin1.GetString(pdf);
        var hojas = System.Text.RegularExpressions.Regex.Matches(crudo, @"/Type\s*/Page[^s]").Count;
        Assert.True(hojas >= 2, $"El PDF trae {hojas} hoja(s) y se esperaban al menos 2.");

        var carpeta = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "artifacts");
        Directory.CreateDirectory(carpeta);
        var archivo = Path.GetFullPath(Path.Combine(carpeta, "recepcion-cascos.pdf"));
        await File.WriteAllBytesAsync(archivo, pdf);
        _out.WriteLine($"{hojas} hojas, {pdf.Length} bytes -> {archivo}");

        /*
          Y el papel de UN DIA, que es el caso de todos los dias: la hoja se
          llena por jornada. Tiene que caber en una sola pagina y traer el
          total general al final.
        */
        var deUnDia = await servicio.ConsultarDatosDeReporteAsync(hasta, hasta);
        var pdfDia = await WkhtmltopdfHtmlPdfRenderer.RenderAsync(
            RecepcionCascosHtmlBuilder.Construir(deUnDia), "Landscape",
            "Cascos usados recibidos", CancellationToken.None,
            new WkhtmltopdfHtmlPdfRenderer.Opciones
            {
                MargenSuperior = 24,
                EspacioEncabezado = 4,
                MargenInferior = 12,
                MargenIzquierdo = 10,
                MargenDerecho = 10,
                PieIzquierdo = "TAURO",
                PieConLinea = false,
                PieFuente = "Tahoma",
                EncabezadoHtml = RecepcionCascosHtmlBuilder.Encabezado(
                    "TAURO", "", hasta, hasta, "", "", "PRUEBA")
            });

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(pdfDia, 0, 4));
        var archivoDia = Path.GetFullPath(Path.Combine(carpeta, "recepcion-cascos-un-dia.pdf"));
        await File.WriteAllBytesAsync(archivoDia, pdfDia);
        _out.WriteLine($"un dia: {pdfDia.Length} bytes -> {archivoDia}");
    }

    // ==================================================================
    // 6. FILTROS
    // ==================================================================

    /// <summary>
    /// Los filtros de la pantalla: por dia, por rango, por origen y por agente.
    /// Y el rango invertido se endereza en vez de devolver vacio, porque un
    /// listado vacio se lee como "no hubo nada", que es una respuesta falsa.
    /// </summary>
    [Fact]
    public async Task Los_Filtros_Recortan_Lo_Que_Deben()
    {
        var firma = Firma();
        var servicio = Servicio(firma);
        var agente = await UnAgenteActivoAsync(firma);
        var ayer = DateTime.Today.AddDays(-1);

        try
        {
            var deAgente = await servicio.CrearAsync(new CrearRecepcionCascoRequest
            {
                Fecha = ayer, IdOrigen = OrigenAgente, IdAgente = agente, Observaciones = firma,
                Piezas = new() { new() { IdTipoUsado = TipoChico, Piezas = 1 } }
            }, UsuarioDePrueba, "PRUEBA", "WEB");

            var deTienda = await servicio.CrearAsync(new CrearRecepcionCascoRequest
            {
                Fecha = ayer, IdOrigen = OrigenTienda, Referencia = "UNA TIENDA", Observaciones = firma,
                Piezas = new() { new() { IdTipoUsado = TipoChico, Piezas = 1 } }
            }, UsuarioDePrueba, "PRUEBA", "WEB");

            var soloTiendas = await servicio.ConsultarAsync(ayer, ayer, OrigenTienda, null, null, false);
            Assert.Contains(soloTiendas, r => r.idRecepcion == deTienda.idRecepcion);
            Assert.DoesNotContain(soloTiendas, r => r.idRecepcion == deAgente.idRecepcion);

            var soloAgente = await servicio.ConsultarAsync(ayer, ayer, null, agente, null, false);
            Assert.Contains(soloAgente, r => r.idRecepcion == deAgente.idRecepcion);
            Assert.DoesNotContain(soloAgente, r => r.idRecepcion == deTienda.idRecepcion);

            /* Otro dia: ninguna de las dos. */
            var otroDia = await servicio.ConsultarAsync(
                DateTime.Today.AddDays(-9), DateTime.Today.AddDays(-9), null, null, null, false);
            Assert.DoesNotContain(otroDia, r => r.idRecepcion == deAgente.idRecepcion);

            /* Rango al reves: se endereza. */
            var invertido = await servicio.ConsultarAsync(DateTime.Today, ayer, null, agente, null, false);
            Assert.Contains(invertido, r => r.idRecepcion == deAgente.idRecepcion);
        }
        finally
        {
            await LimpiarAsync(firma);
        }
    }

    // ==================================================================
    // 7. AISLAMIENTO: ESTO ES DE TAURO
    // ==================================================================

    /// <summary>
    /// El modulo esta prendido para 'tauro' y APAGADO para las demas empresas
    /// de la base, y sus siete claves de web y cinco de movil existen. Es lo
    /// que hace que la tarjeta salga en el Inicio de quien debe y no en el de
    /// la otra empresa.
    /// </summary>
    [Fact]
    public async Task El_Modulo_Esta_Prendido_Solo_Para_Tauro()
    {
        await using var conexion = new SqlConnection(Cadena(BaseTauro, "zzQaRecepcionModulo"));
        await conexion.OpenAsync();

        await using (var cmd = conexion.CreateCommand())
        {
            cmd.CommandText = @"
SELECT IdStatus FROM dbo.WModulo
WHERE EmpresaId = 1 AND ModuloClave = N'recepcion_cascos';";
            var estatus = await cmd.ExecuteScalarAsync();
            Assert.NotNull(estatus);
            Assert.Equal(1, Convert.ToInt32(estatus));
        }

        await using (var cmd = conexion.CreateCommand())
        {
            cmd.CommandText = @"
SELECT LOWER(EmpresaClave) AS clave, Activo FROM dbo.WModuloEmpresa
WHERE EmpresaId = 1 AND ModuloClave = N'recepcion_cascos';";
            await using var reader = await cmd.ExecuteReaderAsync();

            var prendidas = new List<string>();
            var apagadas = new List<string>();
            while (await reader.ReadAsync())
            {
                var clave = reader.GetString(0);
                if (Convert.ToBoolean(reader.GetValue(1))) prendidas.Add(clave);
                else apagadas.Add(clave);
            }

            Assert.Equal(new[] { "tauro" }, prendidas);
            Assert.DoesNotContain("zaragoza", prendidas);
            _out.WriteLine($"prendidas: {string.Join(",", prendidas)} / apagadas: {string.Join(",", apagadas)}");
        }

        await using (var cmd = conexion.CreateCommand())
        {
            cmd.CommandText = @"
SELECT Clave FROM dbo.WPermiso
WHERE EmpresaId = 1
  AND (Clave LIKE N'recepcion_cascos.%' OR Clave LIKE N'app_movil.recepcion_cascos.%')
ORDER BY Clave;";
            await using var reader = await cmd.ExecuteReaderAsync();

            var claves = new List<string>();
            while (await reader.ReadAsync()) claves.Add(reader.GetString(0));

            Assert.Contains("recepcion_cascos.ver_modulo", claves);
            Assert.Contains("recepcion_cascos.ver", claves);
            Assert.Contains("recepcion_cascos.crear", claves);
            Assert.Contains("recepcion_cascos.editar", claves);
            Assert.Contains("recepcion_cascos.cancelar", claves);
            Assert.Contains("recepcion_cascos.imprimir", claves);
            Assert.Contains("recepcion_cascos.historial", claves);
            Assert.Contains("app_movil.recepcion_cascos.ver", claves);
            Assert.Contains("app_movil.recepcion_cascos.crear", claves);
            Assert.Contains("app_movil.recepcion_cascos.editar", claves);
            Assert.Contains("app_movil.recepcion_cascos.cancelar", claves);
            Assert.Contains("app_movil.recepcion_cascos.imprimir", claves);

            /* El historial NO tiene pareja movil a proposito: el telefono no
               abre esa pantalla, y una clave que no abre nada promete algo que
               no ocurre. */
            Assert.DoesNotContain("app_movil.recepcion_cascos.historial", claves);
        }
    }

    /// <summary>
    /// Zaragoza no queda afectada.
    ///
    /// QUE SE COMPRUEBA Y QUE NO, QUE AQUI ESTA EL MATIZ
    ///
    /// La primera version de esta prueba exigia que en MacZ no existiera NI UNA
    /// clave `recepcion_cascos`, y fallo. Investigado: el catalogo de claves lo
    /// siembra el backend al arrancar (CatalogoDePermisosHostedService ->
    /// PermissionCatalogSeeds), y ese catalogo es del BACKEND, no de una
    /// empresa: las claves aparecen en cualquier base a la que ese backend se
    /// conecte. En local las sembro la propia serie de pruebas, que corre
    /// contra las dos bases.
    ///
    /// No es una fuga, es como funciona el sistema, y se comprobo contra el
    /// modulo equivalente: `cascos_cambio` es de Tauro y en MacZ tiene sus 7
    /// claves y su renglon de WModulo desde septiembre, con CERO tablas y sin
    /// que nadie en Zaragoza lo vea. Lo que decide de quien es un modulo es
    /// WModuloEmpresa.Activo y a quien se le concede la clave, no la existencia
    /// de la clave en el catalogo.
    ///
    /// Asi que lo que se exige es lo que de verdad significa "no afecta a
    /// Zaragoza": alla no hay DATOS (ni tablas ni procedimientos), el modulo no
    /// esta prendido para ninguna empresa de esa base, y NADIE tiene concedida
    /// ninguna de las claves.
    /// </summary>
    [Fact]
    public async Task En_Zaragoza_No_Hay_Datos_Ni_Nadie_Con_Permiso()
    {
        await using var conexion = new SqlConnection(Cadena(BaseZaragoza, "zzQaRecepcionAislamiento"));
        await conexion.OpenAsync();

        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = @"
SELECT
  (SELECT COUNT(*) FROM sys.tables     WHERE name LIKE 'WRecepcionCasco%')      AS tablas,
  (SELECT COUNT(*) FROM sys.procedures WHERE name LIKE 'sp_w_%RecepcionCasco%') AS procedimientos,
  (SELECT COUNT(*) FROM sys.table_types WHERE name LIKE 'WRecepcionCasco%')     AS tipos,
  (SELECT COUNT(*) FROM dbo.WModuloEmpresa
    WHERE ModuloClave = 'recepcion_cascos' AND Activo = 1)                      AS prendido,
  (SELECT COUNT(*) FROM dbo.WRolPermiso rp
     INNER JOIN dbo.WPermiso p ON p.Id = rp.WPermisoId
    WHERE p.Clave LIKE '%recepcion_cascos%')                                    AS concedidoARoles,
  (SELECT COUNT(*) FROM dbo.WUsuarioPermiso up
     INNER JOIN dbo.WPermiso p ON p.Id = up.WPermisoId
    WHERE p.Clave LIKE '%recepcion_cascos%')                                    AS concedidoAUsuarios;";

        await using var reader = await cmd.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());

        Assert.Equal(0, reader.GetInt32(0));   // ninguna tabla
        Assert.Equal(0, reader.GetInt32(1));   // ningun procedimiento
        Assert.Equal(0, reader.GetInt32(2));   // ningun tipo de tabla
        Assert.Equal(0, reader.GetInt32(3));   // apagado para todas sus empresas
        Assert.Equal(0, reader.GetInt32(4));   // ningun rol lo trae
        Assert.Equal(0, reader.GetInt32(5));   // ningun usuario lo trae
    }

    // ==================================================================
    // Apoyo
    // ==================================================================

    /*
      Cada prueba marca sus renglones con una firma propia. Asi se limpian sin
      tocar nada mas, y dos pruebas que corran a la vez no se estorban.
    */
    private static string Firma() => $"ZZQA-RECEP-{Guid.NewGuid():N}";

    private static CrearRecepcionCascoRequest Basica(int agente, string firma) => new()
    {
        Fecha = DateTime.Today,
        IdOrigen = OrigenAgente,
        IdAgente = agente,
        Observaciones = firma,
        Piezas = new() { new() { IdTipoUsado = TipoChico, Piezas = 1 } }
    };

    private static async Task<int> UnAgenteActivoAsync(string firma)
    {
        await using var conexion = new SqlConnection(Cadena(BaseTauro, firma));
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "SELECT MIN(IDAgente) FROM dbo.Agentes WHERE IDStatus = 1;";
        var valor = await cmd.ExecuteScalarAsync();
        Assert.True(valor != null && valor != DBNull.Value, "La base de Tauro no tiene agentes activos.");
        return Convert.ToInt32(valor);
    }

    private static async Task<int> UnAgenteInactivoAsync(string firma)
    {
        await using var conexion = new SqlConnection(Cadena(BaseTauro, firma));
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "SELECT MIN(IDAgente) FROM dbo.Agentes WHERE IDStatus <> 1;";
        var valor = await cmd.ExecuteScalarAsync();
        Assert.True(valor != null && valor != DBNull.Value, "La base de Tauro no tiene agentes inactivos.");
        return Convert.ToInt32(valor);
    }

    private static async Task<int> CuantasAsync(string firma)
    {
        await using var conexion = new SqlConnection(Cadena(BaseTauro, firma));
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM dbo.WRecepcionCasco WHERE Observaciones = @firma;";
        cmd.Parameters.AddWithValue("@firma", firma);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    /*
      Borra en el orden de las llaves: bitacora y detalle antes que la cabecera.
      Va en un finally siempre, porque una prueba que se cae a la mitad y deja
      renglones hace que la siguiente mienta.
    */
    private static async Task LimpiarAsync(string firma)
    {
        await using var conexion = new SqlConnection(Cadena(BaseTauro, firma));
        await conexion.OpenAsync();
        await using var cmd = conexion.CreateCommand();
        cmd.CommandText = @"
DELETE b FROM dbo.WRecepcionCascoBitacora b
  INNER JOIN dbo.WRecepcionCasco c ON c.IdRecepcion = b.IdRecepcion
 WHERE c.Observaciones = @firma;

DELETE d FROM dbo.WRecepcionCascoDetalle d
  INNER JOIN dbo.WRecepcionCasco c ON c.IdRecepcion = d.IdRecepcion
 WHERE c.Observaciones = @firma;

DELETE FROM dbo.WRecepcionCasco WHERE Observaciones = @firma;";
        cmd.Parameters.AddWithValue("@firma", firma);
        await cmd.ExecuteNonQueryAsync();
    }
}
