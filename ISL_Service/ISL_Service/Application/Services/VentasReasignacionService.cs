using System.Globalization;
using ISL_Service.Application.DTOs.VentasReasignacion;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/// <summary>
/// Las reglas de la LECTURA de la pantalla de reasignar.
/// </summary>
/// <remarks>
/// EL PERIODO POR DEFECTO ES UN ANO, Y NO ES LA REGLA DE LA CASA POR PEREZA.
///
/// Mac31 no tiene filtro de fechas en esta pantalla: pide TODAS las notas
/// vigentes del cliente (`VentasReasignacion.cs:332`, solo `@Formato = 1`). La
/// regla de la casa —los filtros abren en "hoy"— aqui daria casi siempre cero
/// filas y pareceria que el cliente no debe nada, que es la lectura mas
/// peligrosa posible en una pantalla de cobranza. Un ano hacia atras es lo que
/// usa el propio web y lo que `ConsultarVentasSeleccionar` toma de arranque
/// fuera de Tauro.
///
/// El rango SE PUEDE AMPLIAR desde el cliente, y cuando se manda vacio no se
/// pone ninguno: el procedimiento sin fechas devuelve la historia completa y a
/// veces es justo lo que hace falta.
/// </remarks>
public class VentasReasignacionService : IVentasReasignacionService
{
    /// <summary>Cuanto abarca el periodo de arranque si no se pide otro.</summary>
    public const int DiasDeArranque = 365;

    private readonly IVentasReasignacionRepository _repository;

    public VentasReasignacionService(IVentasReasignacionRepository repository)
    {
        _repository = repository;
    }

    public async Task<ReasignacionArranqueResponse> ArranqueAsync(
        int idCliente, string? desde, string? hasta, CancellationToken ct)
    {
        var agentes = await _repository.ConsultarAgentesAsync(ct);

        if (idCliente <= 0)
        {
            // SIN CLIENTE NO SE PIDEN NOTAS. No es un atajo de rendimiento: sin
            // cliente, `sp_n_ConsultaVentas` con @IDCliente = 0 devolveria las
            // notas vigentes de TODA la empresa —93,311 filas en MacZ— por una
            // pantalla que todavia no sabe de quien va a mover nada.
            return new ReasignacionArranqueResponse { Agentes = agentes };
        }

        var cliente = await _repository.ConsultarClienteAsync(idCliente, ct);
        if (cliente is null)
            throw new ArgumentException("Ese cliente no existe.");

        var (dia1, dia2) = Periodo(desde, hasta);
        var (filas, total) = await _repository.ConsultarRemisionesVigentesAsync(idCliente, dia1, dia2, ct);

        return new ReasignacionArranqueResponse
        {
            Cliente = cliente,
            Agentes = agentes,
            Remisiones = filas,
            Total = total,
            // Esta lectura NO pasa por `AcotarFiltroDeUsuario`, asi que la
            // cartera viene entera. Se afirma aqui, en el servidor, y no se
            // deduce en el telefono.
            AlcanceCompleto = true,
        };
    }

    public Task<ClienteCandidatoResponse> BuscarClientesAsync(string? texto, CancellationToken ct)
    {
        var limpio = (texto ?? "").Trim();
        if (limpio.Length == 0) return Task.FromResult(new ClienteCandidatoResponse());

        /*
          PUROS DIGITOS = NUMERO DE CLIENTE; CUALQUIER OTRA COSA = NOMBRE.

          Es lo que hace el web y lo que la gente teclea sin pensar. Obligar a
          elegir "busco por numero" o "busco por nombre" antes de escribir es lo
          que hace que nadie use el buscador.
        */
        var esNumero = limpio.Length <= 9 && limpio.All(char.IsDigit);
        return esNumero
            ? _repository.BuscarClientesAsync(int.Parse(limpio, CultureInfo.InvariantCulture), null, ct)
            : _repository.BuscarClientesAsync(0, limpio, ct);
    }

    /// <summary>
    /// El rango que se le pasa al procedimiento. Las dos fechas vacias = sin
    /// filtro de fechas; una sola vacia = se completa con el ano de arranque,
    /// porque medio rango es un rango que no se puede razonar.
    /// </summary>
    public static (string Desde, string Hasta) Periodo(string? desde, string? hasta)
    {
        var d = (desde ?? "").Trim();
        var h = (hasta ?? "").Trim();

        if (d.Length == 0 && h.Length == 0)
        {
            var hoy = DateTime.Today;
            return (Iso(hoy.AddDays(-DiasDeArranque)), Iso(hoy));
        }

        if (d.Length == 0) d = Iso(Leer(h).AddDays(-DiasDeArranque));
        if (h.Length == 0) h = Iso(DateTime.Today);

        return (d, h);
    }

    private static DateTime Leer(string texto) =>
        DateTime.TryParse(texto, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
            ? fecha
            : DateTime.Today;

    /// <summary>
    /// ISO, siempre. Es lo que SQL Server lee igual con cualquier DATEFORMAT, y
    /// es la razon por la que esta pantalla no necesita llamar a
    /// `sp_n_ConsultaConstantes` para preguntar el formato de fecha de la base
    /// — que es la rutina que hace el CIERRE DEL DIA.
    /// </summary>
    private static string Iso(DateTime dia) => dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
