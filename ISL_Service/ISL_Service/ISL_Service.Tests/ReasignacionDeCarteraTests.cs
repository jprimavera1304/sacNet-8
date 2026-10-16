using ISL_Service.Application.DTOs.VentasEscrituras;
using ISL_Service.Application.DTOs.VentasPagosOperaciones;
using ISL_Service.Application.DTOs.VentasReasignacion;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using ISL_Service.Application.Services;
using ISL_Service.Controllers;

namespace ISL_Service.Tests;

/*
  REASIGNAR CARTERA: LAS REGLAS QUE DECIDEN DE QUIEN ES EL DINERO POR COBRAR.

  Lo que se prueba aqui no es si compila: es si una reasignacion puede dejar una
  cartera sin dueno, o decir que movio algo que no movio. Las dos cosas pasaban.

  Lo que NO se prueba aqui porque necesita base: que el catalogo de agentes de
  un centro no coincide con el de la matriz. Eso esta medido y escrito en
  `VentasReasignacionController` con los numeros de MacZ, y lo que SI se prueba
  es la consecuencia: que con X-Centro != 0 el endpoint contesta 409 con motivo.
*/
public class ReasignacionDeCarteraTests
{
    // ───────────────────────── LA ESCRITURA ─────────────────────────

    private static VentasPagosOperacionesService ServicioDeEscritura(
        out RepositorioDeEscrituraEspia espia)
    {
        espia = new RepositorioDeEscrituraEspia();
        return new VentasPagosOperacionesService(espia);
    }

    [Fact]
    public async Task Reasignar_sin_agente_se_rechaza_aunque_se_cambie_el_cliente()
    {
        /*
          EL AGUJERO QUE ESTO CIERRA.

          `sp_n_VentasReasignacion` escribe `IDAgente = @IDAgente` en las DOS
          ramas (las notas y el cliente), y @IDAgente por omision es 0. La
          validacion vieja, con CambiarCliente = 1, solo exigia el cliente: este
          cuerpo pasaba y dejaba las notas y el cliente apuntando al agente 0.
          `Ventas.IDAgente` no tiene llave ajena, asi que la base lo acepta y el
          procedimiento contesta exito. Cartera sin dueno, sin un solo error.
        */
        var servicio = ServicioDeEscritura(out var espia);

        var error = await Assert.ThrowsAsync<ArgumentException>(() =>
            servicio.ReasignarAsync(new VentasReasignarRequest
            {
                CambiarCliente = 1,
                IdCliente = 19470,
                IdAgente = 0,
                IdsVenta = new List<int> { 168565 },
                Motivo = "SE VA DE LA EMPRESA",
            }, 1, "PRUEBA", CancellationToken.None));

        Assert.Contains("agente", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(espia.Ultima);   // y NO llego al procedimiento
    }

    [Fact]
    public async Task Reasignar_solo_el_agente_del_cliente_SI_se_permite_con_la_lista_vacia()
    {
        /*
          EL MODO QUE ESTABA MUERTO, Y ES EL MAS USADO.

          "REASIGNAR AGENTE" de Mac31 manda la lista vacia a proposito:
          CambiarCliente = 1 e IDsVenta = ''. El procedimiento entonces no toca
          ninguna nota y solo hace `UPDATE Clientes SET IDAgente`. La validacion
          vieja exigia la lista SIEMPRE, asi que contestaba 400.

          Y no es un caso raro: 557 de las 793 filas de `[Ventas Reasignacion]`
          en MacZ tienen IDVenta = 0, que es la firma de este modo. El 70%.
        */
        var servicio = ServicioDeEscritura(out var espia);

        await servicio.ReasignarAsync(new VentasReasignarRequest
        {
            CambiarCliente = 1,
            IdCliente = 19470,
            IdAgente = 7,
            IdsVenta = new List<int>(),
            Motivo = "CAMBIO DE RUTA",
        }, 1, "PRUEBA", CancellationToken.None);

        Assert.NotNull(espia.Ultima);
        Assert.Empty(espia.Ultima!.IdsVenta);
        Assert.Equal(7, espia.Ultima.IdAgente);
    }

    [Fact]
    public async Task Reasignar_nada_de_nada_sigue_rechazandose()
    {
        /*
          La preocupacion original era buena y se conserva: con la lista vacia Y
          sin cambiar el agente del cliente, el procedimiento NO ESCRIBE NADA y
          contesta exito igual. La pantalla diria "reasignado" y nada cambio de
          dueno. Eso es lo unico que se rechaza por este camino.
        */
        var servicio = ServicioDeEscritura(out var espia);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            servicio.ReasignarAsync(new VentasReasignarRequest
            {
                CambiarCliente = 0,
                IdAgente = 7,
                IdsVenta = new List<int>(),
                Motivo = "NADA",
            }, 1, "PRUEBA", CancellationToken.None));

        Assert.Null(espia.Ultima);
    }

    [Fact]
    public async Task El_motivo_es_obligatorio_y_los_espacios_no_cuentan()
    {
        /*
          El motivo es lo UNICO que explica meses despues por que una nota cambio
          de agente: es la columna `Motivo` de `[Ventas Reasignacion]`. Legacy
          tambien lo pide, y es de las pocas cosas que valida bien.
        */
        var servicio = ServicioDeEscritura(out var espia);

        foreach (var motivo in new[] { null, "", "   ", "\t" })
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                servicio.ReasignarAsync(new VentasReasignarRequest
                {
                    CambiarCliente = 0,
                    IdAgente = 7,
                    IdsVenta = new List<int> { 168565 },
                    Motivo = motivo,
                }, 1, "PRUEBA", CancellationToken.None));
        }

        Assert.Null(espia.Ultima);
    }

    [Fact]
    public async Task Solo_notas_exige_la_lista_y_no_el_cliente()
    {
        /*
          "REASIGNAR SOLO NOTAS": CambiarCliente = 0. El procedimiento no mira
          @IDCliente mas que para la fila de auditoria, asi que exigirlo
          obligaria a mandar un numero de relleno en un campo que decide de quien
          es una cartera. No se exige.
        */
        var servicio = ServicioDeEscritura(out var espia);

        await servicio.ReasignarAsync(new VentasReasignarRequest
        {
            CambiarCliente = 0,
            IdCliente = 0,
            IdAgente = 7,
            IdsVenta = new List<int> { 168565, 168570 },
            Motivo = "SE LE PASA AL COBRADOR NUEVO",
        }, 1, "PRUEBA", CancellationToken.None);

        Assert.NotNull(espia.Ultima);
        Assert.Equal(new[] { 168565, 168570 }, espia.Ultima!.IdsVenta);
    }

    // ───────────────────────── LA LECTURA ─────────────────────────

    [Fact]
    public async Task Sin_cliente_no_se_piden_notas_de_toda_la_empresa()
    {
        /*
          Con @IDCliente = 0, `sp_n_ConsultaVentas` devuelve las notas vigentes
          de TODA la empresa: 93,311 filas en MacZ. Una pantalla que todavia no
          sabe de quien va a mover nada no tiene por que traerselas.
        */
        var espia = new RepositorioDeLecturaEspia();
        var servicio = new VentasReasignacionService(espia);

        var arranque = await servicio.ArranqueAsync(0, null, null, CancellationToken.None);

        Assert.Null(arranque.Cliente);
        Assert.Empty(arranque.Remisiones);
        Assert.NotEmpty(arranque.Agentes);
        Assert.Equal(0, espia.VecesQuePidioRemisiones);
    }

    [Fact]
    public async Task El_arranque_trae_el_agente_actual_aunque_el_cliente_no_tenga_notas()
    {
        /*
          ES EL MOTIVO PRINCIPAL DE QUE ESTE ENDPOINT EXISTA.

          Mac31 lee el agente actual de la PRIMERA FILA de la consulta de ventas
          (`VentasReasignacion.cs:407`), asi que con cero notas la etiqueta se
          esconde. Pero el modo "solo agente" es valido justamente para un
          cliente sin notas, y es el 70% del uso real. Una confirmacion que no
          puede decir a quien se le quita la cartera no es una confirmacion.
        */
        var espia = new RepositorioDeLecturaEspia { SinRemisiones = true };
        var servicio = new VentasReasignacionService(espia);

        var arranque = await servicio.ArranqueAsync(19470, null, null, CancellationToken.None);

        Assert.Empty(arranque.Remisiones);
        Assert.NotNull(arranque.Cliente);
        Assert.Equal(7, arranque.Cliente!.IdAgenteActual);
        Assert.Equal("MARIO ALMANZA ALFAYO", arranque.Cliente.NombreAgenteActual);
        Assert.True(arranque.AlcanceCompleto);
    }

    [Fact]
    public async Task El_catalogo_dice_que_agentes_estan_de_baja()
    {
        /*
          `sp_n_ConsultaAgentes` con @IDStatus = 0 devuelve TODOS los estatus, y
          asi lo llena el combo de Mac31. Resultado medido: 9 filas de
          `[Ventas Reasignacion]` en MacZ entregaron cartera a un agente con
          IDStatus distinto de 1. El catalogo se devuelve completo —un agente de
          baja conserva cartera vieja— pero MARCADO, para que el cliente pueda
          apagar la accion con su motivo.
        */
        var espia = new RepositorioDeLecturaEspia();
        var servicio = new VentasReasignacionService(espia);

        var arranque = await servicio.ArranqueAsync(0, null, null, CancellationToken.None);

        Assert.Contains(arranque.Agentes, a => a.Activo);
        Assert.Contains(arranque.Agentes, a => !a.Activo);
    }

    [Fact]
    public async Task Un_cliente_que_no_existe_no_devuelve_una_pantalla_a_medias()
    {
        var espia = new RepositorioDeLecturaEspia { ClienteExiste = false };
        var servicio = new VentasReasignacionService(espia);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            servicio.ArranqueAsync(99999, null, null, CancellationToken.None));

        Assert.Equal(0, espia.VecesQuePidioRemisiones);
    }

    [Fact]
    public void El_periodo_de_arranque_es_un_ano_y_no_hoy()
    {
        /*
          "Hoy" dejaria la pantalla vacia casi siempre y pareceria que el cliente
          no debe nada, que es la lectura mas peligrosa en cobranza. Mac31 no
          tiene filtro de fechas aqui: pide todas las vigentes.
        */
        var (desde, hasta) = VentasReasignacionService.Periodo(null, null);

        var d = DateTime.Parse(desde);
        var h = DateTime.Parse(hasta);
        Assert.Equal(DateTime.Today, h);
        Assert.Equal(VentasReasignacionService.DiasDeArranque, (h - d).Days);
    }

    [Fact]
    public void Medio_rango_se_completa_y_no_se_manda_a_medias()
    {
        var (desde, hasta) = VentasReasignacionService.Periodo(null, "2026-06-30");
        Assert.Equal("2026-06-30", hasta);
        Assert.Equal("2025-06-30", desde);

        var (desde2, hasta2) = VentasReasignacionService.Periodo("2026-01-01", null);
        Assert.Equal("2026-01-01", desde2);
        Assert.Equal(DateTime.Today, DateTime.Parse(hasta2));
    }

    [Fact]
    public void El_buscador_distingue_un_numero_de_un_nombre()
    {
        var espia = new RepositorioDeLecturaEspia();
        var servicio = new VentasReasignacionService(espia);

        servicio.BuscarClientesAsync("542", CancellationToken.None).GetAwaiter().GetResult();
        Assert.Equal(542, espia.UltimoNumeroBuscado);
        Assert.Null(espia.UltimoNombreBuscado);

        servicio.BuscarClientesAsync("ABARROTES", CancellationToken.None).GetAwaiter().GetResult();
        Assert.Equal(0, espia.UltimoNumeroBuscado);
        Assert.Equal("ABARROTES", espia.UltimoNombreBuscado);
    }

    [Fact]
    public void El_buscador_vacio_no_pide_nada_al_servidor()
    {
        var espia = new RepositorioDeLecturaEspia();
        var servicio = new VentasReasignacionService(espia);

        var resultado = servicio
            .BuscarClientesAsync("   ", CancellationToken.None)
            .GetAwaiter().GetResult();

        Assert.Empty(resultado.Clientes);
        Assert.Equal(0, espia.VecesQueBusco);
    }

    // ───────────────────── CENTROS Y PERMISOS ─────────────────────

    [Fact]
    public void La_entrada_al_modulo_de_reasignacion_se_deriva_de_cobros_reasignar()
    {
        /*
          No hay que sembrar `reasignacion.ver_modulo` por separado: ya esta
          derivada (`EntradasDeModulo.cs:143`). Si esto se rompiera, el permiso
          del endpoint quedaria sin puerta y el modulo se abriria a quien tiene
          la clave fina sin tener concedido el modulo.
        */
        Assert.True(EntradasDeModulo.PuedeEntrar(
            "cobros.reasignar",
            clave => clave == "reasignacion.ver_modulo"));

        Assert.False(EntradasDeModulo.PuedeEntrar(
            "cobros.reasignar",
            clave => false));
    }

    [Fact]
    public void La_clave_del_movil_no_arrastra_una_puerta_que_no_existe()
    {
        /*
          `app_movil.reasignacion.ver` todavia no esta sembrada. Lo que esta
          prueba cuida es que no se le declare una entrada de modulo que nadie
          tiene: con una puerta inexistente, la clave no serviria nunca y el
          mosaico se esconderia para siempre sin dejar rastro. Ya paso con
          `pedidos.capturar`.
        */
        Assert.True(EntradasDeModulo.PuedeEntrar(
            "app_movil.reasignacion.ver",
            clave => false));
    }

    [Fact]
    public void El_motivo_del_409_de_centros_dice_que_hacer_y_no_solo_que_no_se_pudo()
    {
        /*
          El sobre de error de esta casa es {code, message, detail} y la frase
          que dice QUE corregir viaja en `detail`. Un motivo vacio deja a la
          persona con un "no se pudo" y nada mas.
        */
        var motivo = VentasReasignacionController.MotivoSinCentros;

        Assert.False(string.IsNullOrWhiteSpace(motivo));
        Assert.Contains("matriz", motivo, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("agentes", motivo, StringComparison.OrdinalIgnoreCase);
    }

    // ───────────────────────── LOS ESPIAS ─────────────────────────

    private sealed class RepositorioDeEscrituraEspia : IVentasPagosOperacionesRepository
    {
        public VentasReasignarRequest? Ultima { get; private set; }

        public Task<ResultadoLegacyDto> ReasignarAsync(
            VentasReasignarRequest request, int idUsuario, string equipo, CancellationToken ct)
        {
            Ultima = request;
            return Task.FromResult(new ResultadoLegacyDto { Resultado = 1, Mensaje = "" });
        }

        /* Lo demas no se usa en estas pruebas. */
        public Task<ResultadoLegacyDto> DeclararIncobrableAsync(VentaIncobrableRequest r, int u, string e, CancellationToken ct) => throw new NotSupportedException();
        public Task<ResultadoLegacyDto> AbonarKilosAsync(VentaPagoKilosRequest r, int u, string e, CancellationToken ct) => throw new NotSupportedException();
        public Task<ResultadoLegacyDto> CancelarAbonoKilosAsync(VentaPagoKilosCancelarRequest r, int u, string e, CancellationToken ct) => throw new NotSupportedException();
        public Task<ResultadoLegacyDto> LimpiarPagosCentroServicioAsync(int idPagoVenta, int idVenta, CancellationToken ct) => throw new NotSupportedException();
        public Task<ResultadoLegacyDto> CrearNuevoPagoAsync(NuevoPagoCrearRequest r, int u, string e, CancellationToken ct) => throw new NotSupportedException();
        public Task<ResultadoLegacyDto> AplicarNuevoPagoAsync(NuevoPagoRequest r, int u, string e, CancellationToken ct) => throw new NotSupportedException();
        public Task<ResultadoLegacyDto> CancelarNuevoPagoAsync(NuevoPagoRequest r, int u, string e, CancellationToken ct) => throw new NotSupportedException();
        public Task<ResultadoLegacyDto> CancelarTransferenciaAsync(TransferenciaCancelarRequest r, int u, string e, CancellationToken ct) => throw new NotSupportedException();
        public Task<ResultadoLegacyDto> CancelarTransferenciaPagoAsync(TransferenciaPagoCancelarRequest r, int u, string e, CancellationToken ct) => throw new NotSupportedException();
        public Task<ResultadoLegacyDto> ActualizarVentaAsync(VentaActualizarRequest r, CancellationToken ct) => throw new NotSupportedException();
        public Task<ResultadoLegacyDto> MarcarPagoPorKiloAsync(VentasPagoPorKiloRequest r, int u, string e, CancellationToken ct) => throw new NotSupportedException();
        public Task<FilasLegacyDto> ConsultarDetalleAsync(VentasDetalleConsultaRequest r, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class RepositorioDeLecturaEspia : IVentasReasignacionRepository
    {
        public bool ClienteExiste { get; init; } = true;
        public bool SinRemisiones { get; init; }

        public int VecesQuePidioRemisiones { get; private set; }
        public int VecesQueBusco { get; private set; }
        public int UltimoNumeroBuscado { get; private set; }
        public string? UltimoNombreBuscado { get; private set; }

        public Task<List<AgenteDeReasignacionDto>> ConsultarAgentesAsync(CancellationToken ct) =>
            Task.FromResult(new List<AgenteDeReasignacionDto>
            {
                new() { IdAgente = 7, Numero = 4, Nombre = "MARIO ALMANZA ALFAYO", Activo = true },
                // Uno de baja: el catalogo los trae, marcados.
                new() { IdAgente = 8, Numero = 7, Nombre = "CARLOS MORALES FRANCISCO", Activo = false },
            });

        public Task<ClienteDeReasignacionDto?> ConsultarClienteAsync(int idCliente, CancellationToken ct) =>
            Task.FromResult(ClienteExiste
                ? new ClienteDeReasignacionDto
                {
                    IdCliente = idCliente,
                    Numero = 542,
                    Nombre = "ABARROTES LA ESPERANZA",
                    Activo = true,
                    IdAgenteActual = 7,
                    NumeroAgenteActual = 4,
                    NombreAgenteActual = "MARIO ALMANZA ALFAYO",
                }
                : null);

        public Task<ClienteCandidatoResponse> BuscarClientesAsync(int numero, string? nombre, CancellationToken ct)
        {
            VecesQueBusco++;
            UltimoNumeroBuscado = numero;
            UltimoNombreBuscado = nombre;
            return Task.FromResult(new ClienteCandidatoResponse());
        }

        public Task<(List<Dictionary<string, object?>> Filas, int Total)> ConsultarRemisionesVigentesAsync(
            int idCliente, string desde, string hasta, CancellationToken ct)
        {
            VecesQuePidioRemisiones++;
            var filas = SinRemisiones
                ? new List<Dictionary<string, object?>>()
                : new List<Dictionary<string, object?>>
                {
                    new() { ["IDVenta"] = 168565, ["Folio"] = 10482, ["Saldo"] = 1240.50m },
                };
            return Task.FromResult((filas, filas.Count));
        }
    }
}
