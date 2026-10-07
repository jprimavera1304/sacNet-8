using ISL_Service.Application.DTOs.EscriturasLegacy;
using ISL_Service.Application.DTOs.Gasolinas;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/// <summary>Reglas de los recibos de gasolina.</summary>
public class GasolinasService : IGasolinasService
{
    private readonly IGasolinasRepository _repo;

    public GasolinasService(IGasolinasRepository repo)
    {
        _repo = repo;
    }

    public Task<List<Dictionary<string, object?>>> ConsultarAsync(GasolinasFiltro f, CancellationToken ct = default)
    {
        ExigirRango(f.FechaInicial, f.FechaFinal, "fechaInicial", "fechaFinal");
        ExigirRango(f.FechaInicialCaptura, f.FechaFinalCaptura, "fechaInicialCaptura", "fechaFinalCaptura");
        return _repo.ConsultarAsync(f, ct);
    }

    public async Task<ResultadoEscrituraDto> CrearAsync(CrearGasolinaRequest r, int idUsuario, string equipo, CancellationToken ct = default)
    {
        Validar(r);
        return EscrituraLegacyReglas.Traducir(
            await _repo.InsertarAsync(r, idUsuario, equipo, ct),
            "registrar el recibo de gasolina");
    }

    public async Task<ResultadoEscrituraDto> ActualizarAsync(int idGasolina, ActualizarGasolinaRequest r, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(idGasolina, "idGasolina");
        Validar(r);
        return EscrituraLegacyReglas.Traducir(
            await _repo.ActualizarAsync(idGasolina, r, idUsuario, equipo, ct),
            "actualizar el recibo de gasolina");
    }

    public async Task<ResultadoCancelacionGasolinasDto> CancelarAsync(CancelarGasolinasRequest r, int idUsuario, string equipo, CancellationToken ct = default)
    {
        if (r.IdsGasolina.Count == 0)
            throw new ArgumentException("idsGasolina tiene que traer al menos un recibo.");

        if (r.IdsGasolina.Any(x => x <= 0))
            throw new ArgumentException("idsGasolina no puede traer ceros ni negativos.");

        if (r.IdsGasolina.Distinct().Count() != r.IdsGasolina.Count)
            throw new ArgumentException("idsGasolina trae el mismo recibo dos veces.");

        /*
          LAS DOS LISTAS TIENEN QUE MEDIR LO MISMO.

          El SP recorre @IDsGasolina y @NumerosRecibo a la vez para dejar el numero
          de recibo en la bitacora de cancelacion. Legacy recibia las dos cadenas
          ya concatenadas desde el cliente y no las comparaba: con una lista mas
          corta, los ultimos recibos se cancelaban con el numero de otro, y la
          bitacora —que es lo que se revisa cuando falta dinero— queda mintiendo.
        */
        if (r.NumerosRecibo.Count != r.IdsGasolina.Count)
            throw new ArgumentException($"numerosRecibo tiene {r.NumerosRecibo.Count} elementos y idsGasolina tiene {r.IdsGasolina.Count}: tienen que ser iguales.");

        /*
          EL NUMERO DE RECIBO TIENE QUE SER TODO DIGITOS, Y NO ES OBVIO.

          La columna Gasolina.NumeroRecibo es varchar(100) y el parametro del SP es
          varchar(4000), asi que todo apunta a que admite texto. No lo admite:
          sp_n_CancelarGasolina parte la cadena y la mete en una tabla temporal
          declarada `#TableNumeroRecibo ( NumeroRecibo int )` (leido del cuerpo del
          procedimiento). Con un recibo que traiga una letra, SQL Server lanza
          "Conversion failed when converting the varchar value 'X' to data type
          int" y eso sale como un 500 con el mensaje de la base: el usuario lee un
          error de SQL y no sabe que el problema es su numero de recibo.

          Asi que se comprueba aqui y se contesta un 400 que lo dice. Legacy no lo
          comprobaba y el 500 era lo que veia la pantalla.
        */
        foreach (var numero in r.NumerosRecibo)
        {
            var limpio = (numero ?? string.Empty).Trim();
            if (limpio.Length == 0 || !limpio.All(char.IsAsciiDigit))
                throw new ArgumentException(
                    $"numerosRecibo solo admite digitos: \"{numero}\" no lo es. El procedimiento de cancelacion los convierte a entero.");
        }

        /*
          AQUI NO SE TRADUCE UN (result, mensaje): se devuelve la tabla.

          Este SP contesta un renglon por recibo con su propio texto, asi que
          "salio bien" no es una sola cosa: unos recibos pueden quedar cancelados y
          otros no (porque ya lo estaban). Aplanarlo a un si/no perderia justo lo
          que el usuario necesita leer.
        */
        return new ResultadoCancelacionGasolinasDto
        {
            Recibos = await _repo.CancelarAsync(r, idUsuario, equipo, ct)
        };
    }

    private static void Validar(CrearGasolinaRequest r)
    {
        EscrituraLegacyReglas.ExigirId(r.IdAuto, "idAuto");
        EscrituraLegacyReglas.ExigirId(r.IdRepartidor, "idRepartidor");
        r.NumeroRecibo = EscrituraLegacyReglas.ExigirTexto(r.NumeroRecibo, "numeroRecibo", 100);

        if (r.FechaRecibo is null) throw new ArgumentException("fechaRecibo es requerida.");

        /*
          UN RECIBO DE GASOLINA DEL FUTURO NO EXISTE.

          Es un comprobante que ya se pago. Legacy aceptaba cualquier fecha porque
          solo comprobaba que la cadena no estuviera vacia.
        */
        if (r.FechaRecibo > DateOnly.FromDateTime(DateTime.Today))
            throw new ArgumentException("fechaRecibo no puede ser futura.");

        if (r.KmInicial < 0 || r.KmFinal < 0)
            throw new ArgumentException("El kilometraje no puede ser negativo.");

        if (r.PrecioLitro < 0) throw new ArgumentException("El precio por litro no puede ser negativo.");
        if (r.TotalRecibo < 0) throw new ArgumentException("El total del recibo no puede ser negativo.");
    }

    private static void ExigirRango(DateOnly? desde, DateOnly? hasta, string nombreDesde, string nombreHasta)
    {
        if (hasta is not null && desde is null)
            throw new ArgumentException($"{nombreHasta} necesita {nombreDesde}.");
        if (desde is not null && hasta is not null && hasta < desde)
            throw new ArgumentException($"{nombreHasta} no puede ser anterior a {nombreDesde}.");
    }
}
