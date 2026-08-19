using ISL_Service.Application.DTOs.Checador;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Services;

namespace ISL_Service.Tests;

public class ChecadorServiceTests
{
    [Fact]
    public async Task RegistrarComidaAsync_Tipo_Vacio_Se_Manda_Vacio_Al_SP()
    {
        var repo = new FakeChecadorRepository();
        var service = new ChecadorService(repo);

        await service.RegistrarComidaAsync(new RegistrarChecadaComidaRequest { IDEmpleado = 7 }, 33, "CAJA1");

        Assert.Equal(string.Empty, repo.LastTipo);
        Assert.Equal(ChecadaComidaOrigen.Web, repo.LastOrigen);
        Assert.Equal(7, repo.LastIDEmpleado);
        Assert.Equal(33, repo.LastIDUsuario);
        Assert.Equal("CAJA1", repo.LastEquipo);
    }

    [Theory]
    [InlineData("inicio", ChecadaComidaTipo.Inicio)]
    [InlineData("COMIDA_INICIO", ChecadaComidaTipo.Inicio)]
    [InlineData("fin", ChecadaComidaTipo.Fin)]
    [InlineData(" comida_fin ", ChecadaComidaTipo.Fin)]
    public async Task RegistrarComidaAsync_Normaliza_Tipo(string entrada, string esperado)
    {
        var repo = new FakeChecadorRepository();
        var service = new ChecadorService(repo);

        await service.RegistrarComidaAsync(new RegistrarChecadaComidaRequest { IDEmpleado = 1, Tipo = entrada }, 1, "PC");

        Assert.Equal(esperado, repo.LastTipo);
    }

    [Fact]
    public async Task RegistrarComidaAsync_Normaliza_Origen_Checador()
    {
        var repo = new FakeChecadorRepository();
        var service = new ChecadorService(repo);

        await service.RegistrarComidaAsync(
            new RegistrarChecadaComidaRequest { IDEmpleado = 1, Origen = "checador", IDEmpleadoHuella = 5 }, 1, "PC");

        Assert.Equal(ChecadaComidaOrigen.Checador, repo.LastOrigen);
        Assert.Equal(5, repo.LastIDEmpleadoHuella);
    }

    [Fact]
    public async Task RegistrarComidaAsync_Huella_Cero_Va_Como_Null()
    {
        var repo = new FakeChecadorRepository();
        var service = new ChecadorService(repo);

        await service.RegistrarComidaAsync(
            new RegistrarChecadaComidaRequest { IDEmpleado = 1, IDEmpleadoHuella = 0 }, 1, "PC");

        Assert.Null(repo.LastIDEmpleadoHuella);
    }

    [Fact]
    public async Task RegistrarComidaAsync_Rechaza_Empleado_Invalido()
    {
        var service = new ChecadorService(new FakeChecadorRepository());

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.RegistrarComidaAsync(new RegistrarChecadaComidaRequest { IDEmpleado = 0 }, 1, "PC"));
        Assert.Contains("idEmpleado", ex.Message);
    }

    [Fact]
    public async Task RegistrarComidaAsync_Rechaza_Tipo_Desconocido()
    {
        var service = new ChecadorService(new FakeChecadorRepository());

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.RegistrarComidaAsync(new RegistrarChecadaComidaRequest { IDEmpleado = 1, Tipo = "ENTRADA" }, 1, "PC"));
        Assert.Contains("tipo invalido", ex.Message);
    }

    [Fact]
    public async Task RegistrarComidaAsync_Rechaza_Origen_Desconocido()
    {
        var service = new ChecadorService(new FakeChecadorRepository());

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.RegistrarComidaAsync(new RegistrarChecadaComidaRequest { IDEmpleado = 1, Origen = "KIOSKO" }, 1, "PC"));
        Assert.Contains("origen invalido", ex.Message);
    }

    [Fact]
    public async Task RegistrarComidaAsync_Recorta_Equipo_A_200()
    {
        var repo = new FakeChecadorRepository();
        var service = new ChecadorService(repo);

        await service.RegistrarComidaAsync(
            new RegistrarChecadaComidaRequest { IDEmpleado = 1 }, 1, new string('E', 500));

        Assert.Equal(200, repo.LastEquipo!.Length);
    }

    [Fact]
    public async Task ConsultarComidasAsync_Ignora_La_Hora_Y_Manda_Solo_Fecha()
    {
        var repo = new FakeChecadorRepository();
        var service = new ChecadorService(repo);

        await service.ConsultarComidasAsync(
            0, new DateTime(2026, 7, 1, 18, 45, 0), new DateTime(2026, 7, 1, 6, 0, 0), false);

        Assert.Equal(new DateTime(2026, 7, 1), repo.LastFechaInicial);
        Assert.Equal(new DateTime(2026, 7, 1), repo.LastFechaFinal);
    }

    [Fact]
    public async Task ConsultarComidasAsync_Empleado_Negativo_Se_Vuelve_Todos()
    {
        var repo = new FakeChecadorRepository();
        var service = new ChecadorService(repo);

        await service.ConsultarComidasAsync(-4, new DateTime(2026, 7, 1), new DateTime(2026, 7, 2), true);

        Assert.Equal(0, repo.LastIDEmpleado);
        Assert.True(repo.LastIncluirCanceladas);
    }

    [Fact]
    public async Task ConsultarComidasAsync_Rechaza_Fechas_Faltantes()
    {
        var service = new ChecadorService(new FakeChecadorRepository());

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.ConsultarComidasAsync(0, null, new DateTime(2026, 7, 2), false));
        Assert.Contains("fechaInicial y fechaFinal", ex.Message);
    }

    [Fact]
    public async Task ConsultarComidasAsync_Rechaza_Rango_Invertido()
    {
        var service = new ChecadorService(new FakeChecadorRepository());

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.ConsultarComidasAsync(0, new DateTime(2026, 7, 5), new DateTime(2026, 7, 1), false));
        Assert.Contains("no puede ser mayor", ex.Message);
    }

    [Fact]
    public async Task ConsultarComidasAsync_Rechaza_Rango_Demasiado_Largo()
    {
        var service = new ChecadorService(new FakeChecadorRepository());

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.ConsultarComidasAsync(0, new DateTime(2020, 1, 1), new DateTime(2026, 1, 1), false));
        Assert.Contains("dias", ex.Message);
    }

    [Fact]
    public async Task CancelarComidaAsync_Recorta_Motivo_A_400()
    {
        var repo = new FakeChecadorRepository();
        var service = new ChecadorService(repo);

        await service.CancelarComidaAsync(9, new string('M', 900), 12, "PC");

        Assert.Equal(400, repo.LastMotivo!.Length);
        Assert.Equal(9, repo.LastIDEmpleadoChecada);
        Assert.Equal(12, repo.LastIDUsuario);
    }

    [Fact]
    public async Task CancelarComidaAsync_Rechaza_Motivo_Vacio()
    {
        var service = new ChecadorService(new FakeChecadorRepository());

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CancelarComidaAsync(9, "   ", 1, "PC"));
        Assert.Contains("motivo", ex.Message);
    }

    [Fact]
    public async Task CancelarComidaAsync_Rechaza_Id_Invalido()
    {
        var service = new ChecadorService(new FakeChecadorRepository());

        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => service.CancelarComidaAsync(0, "error de captura", 1, "PC"));
        Assert.Contains("idEmpleadoChecada", ex.Message);
    }

    private sealed class FakeChecadorRepository : IChecadorRepository
    {
        public int LastIDEmpleado { get; private set; }
        public string? LastTipo { get; private set; }
        public int? LastIDEmpleadoHuella { get; private set; }
        public string? LastOrigen { get; private set; }
        public int LastIDUsuario { get; private set; }
        public string? LastEquipo { get; private set; }
        public DateTime LastFechaInicial { get; private set; }
        public DateTime LastFechaFinal { get; private set; }
        public bool LastIncluirCanceladas { get; private set; }
        public int LastIDEmpleadoChecada { get; private set; }
        public string? LastMotivo { get; private set; }

        public Task<ChecadaComidaRegistradaDto> RegistrarComidaAsync(
            int idEmpleado, string tipo, int? idEmpleadoHuella, string origen, int idUsuario, string equipo, CancellationToken ct = default)
        {
            LastIDEmpleado = idEmpleado;
            LastTipo = tipo;
            LastIDEmpleadoHuella = idEmpleadoHuella;
            LastOrigen = origen;
            LastIDUsuario = idUsuario;
            LastEquipo = equipo;

            return Task.FromResult(new ChecadaComidaRegistradaDto
            {
                IDEmpleadoChecada = 1,
                IDEmpleado = idEmpleado,
                Tipo = string.IsNullOrEmpty(tipo) ? ChecadaComidaTipo.Inicio : tipo,
                FechaHora = new DateTime(2026, 7, 24, 14, 0, 0)
            });
        }

        public Task<ChecadaComidaRowsResponse> ConsultarComidasAsync(
            int idEmpleado, DateTime fechaInicial, DateTime fechaFinal, bool incluirCanceladas, CancellationToken ct = default)
        {
            LastIDEmpleado = idEmpleado;
            LastFechaInicial = fechaInicial;
            LastFechaFinal = fechaFinal;
            LastIncluirCanceladas = incluirCanceladas;
            return Task.FromResult(new ChecadaComidaRowsResponse());
        }

        public Task CancelarComidaAsync(int idEmpleadoChecada, string motivo, int idUsuario, string equipo, CancellationToken ct = default)
        {
            LastIDEmpleadoChecada = idEmpleadoChecada;
            LastMotivo = motivo;
            LastIDUsuario = idUsuario;
            LastEquipo = equipo;
            return Task.CompletedTask;
        }

        // El resto del checador (movimientos, huellas, configuracion) se agrego
        // despues y estas pruebas no lo tocan: son de las comidas. Van como no
        // implementados para que el doble siga cumpliendo el contrato — si
        // alguna prueba futura los llama, truena y se ve, en vez de devolver un
        // vacio que parezca una respuesta buena.
        public Task<ChecadaMovimientoRegistradoDto> RegistrarMovimientoAsync(
            int idEmpleado, string tipo, int? idEmpleadoHuella, string origen, int idUsuario, string equipo, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<ChecadaComidaRowsResponse> ConsultarMovimientosDiaAsync(DateTime fecha, int idEmpleado, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<ChecadaComidaRowsResponse> ConsultarAsistenciaDiaAsync(DateTime fecha, int idEmpleado, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<ChecadaComidaRowsResponse> ConsultarConfiguracionAsync(CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<ChecadaComidaRowsResponse> ConsultarEmpleadosAsync(string filtro, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<ChecadaComidaRowsResponse> ConsultarHuellasAsync(int idEmpleado, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<ChecadaComidaRowsResponse> GuardarHuellaAsync(
            int idEmpleado, int idMano, int idDedo, byte[] huella, byte[] huella2, int idUsuario, string equipo, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task BajaHuellaAsync(int idEmpleadoHuella, int idUsuario, string equipo, CancellationToken ct = default)
            => throw new NotImplementedException();
    }
}
