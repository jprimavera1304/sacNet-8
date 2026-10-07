using ISL_Service.Application.DTOs.Nomina;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/// <summary>
/// Capa de aplicacion de los tres batches de nomina.
///
/// Su unico trabajo propio es MIRAR SI EL BATCH HIZO ALGO. Legacy no lo miraba en
/// ninguno de los tres: contestaba 200 con la lista de renglones y nadie sabia si
/// el proceso habia terminado o se habia detenido a la mitad. En el descuento de
/// prestamos eso significa no saber si se descontó.
/// </summary>
public class NominaBatchService : INominaBatchService
{
    private readonly INominaBatchRepository _repo;

    public NominaBatchService(INominaBatchRepository repo)
    {
        _repo = repo;
    }

    public async Task<NominaBatchResponse> CalcularRecibosAsync(CalcularRecibosRequest r, CancellationToken ct)
    {
        var esTauro = await _repo.EsTauroAsync(ct);
        return Interpretar(await _repo.CalcularRecibosAsync(r, esTauro, ct));
    }

    public async Task<NominaBatchResponse> GenerarPeriodosAsync(int idUsuario, CancellationToken ct)
        => Interpretar(await _repo.GenerarPeriodosAsync(idUsuario, ct));

    public async Task<NominaBatchResponse> DescontarPrestamosAsync(int idUsuario, CancellationToken ct)
        => Interpretar(await _repo.DescontarPrestamosAsync(idUsuario, ct));

    /// <summary>
    /// Mira el primer renglon buscando el par result/mensaje y la columna de error
    /// con la que el recibo avisa que no pudo recuperar sus parametros
    /// ('ErrorParams').
    ///
    /// Sin renglones se trata como "no hizo nada", no como exito: un batch que no
    /// devolvio nada no se puede dar por bueno, y con la nomina menos.
    ///
    /// Si el procedimiento devuelve la lista SIN columna 'result' —varios lo
    /// hacen— traer renglones es la senal de que trabajo. Se dice aqui para que
    /// nadie piense que el ok esta adivinado.
    /// </summary>
    private static NominaBatchResponse Interpretar(List<Dictionary<string, object?>> renglones)
    {
        if (renglones.Count == 0)
            return new NominaBatchResponse
            {
                Guardo = false,
                Mensaje = "El procedimiento no devolvio renglones: no se puede dar por hecho."
            };

        var primero = renglones[0];

        if (primero.TryGetValue("ErrorParams", out var errorParams))
        {
            var texto = Convert.ToString(errorParams) ?? string.Empty;
            if (texto.Length > 0)
                return new NominaBatchResponse { Guardo = false, Mensaje = texto, Renglones = renglones };
        }

        if (!primero.ContainsKey("result"))
            return new NominaBatchResponse { Guardo = true, Renglones = renglones };

        var result = Leer(primero, "result");
        var mensaje = primero.TryGetValue("mensaje", out var m) ? Convert.ToString(m) ?? string.Empty : string.Empty;

        return new NominaBatchResponse
        {
            Guardo = result > 0,
            Mensaje = result > 0 ? string.Empty : mensaje,
            Renglones = renglones
        };
    }

    private static int Leer(Dictionary<string, object?> fila, string columna)
    {
        if (!fila.TryGetValue(columna, out var v) || v is null) return 0;
        return int.TryParse(Convert.ToString(v), out var n) ? n : 0;
    }
}
