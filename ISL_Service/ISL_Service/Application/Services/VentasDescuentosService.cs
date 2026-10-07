using ISL_Service.Application.DTOs.VentasDescuentos;
using ISL_Service.Application.DTOs.VentasEscrituras;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Application.Services;

/*
  LAS REGLAS DE DESCUENTOS QUE NO ESTAN EN EL PROCEDIMIENTO

  La que de verdad importa es la de los AJUSTES: las seis listas de descuentos se
  cruzan por POSICION con los renglones de la remision dentro del procedimiento.
  Una lista con un elemento menos aplica el descuento del renglon 3 al renglon 4,
  y el sintoma no es un error: es una remision con un importe distinto del que
  alguien autorizo. Legacy no lo comprueba en ningun lado —su segundo bloque de
  validacion usa `&&` entre condiciones contradictorias (`x == null && x == ""`),
  asi que nunca se cumple y no valida nada—.

  Y una decision sobre el porcentaje: se valida que sea un numero entre 0 y 100.
  El procedimiento lo recibe como varchar(50) y lo convierte el; con un texto que
  no es numero, la conversion dentro del procedimiento falla con un mensaje de SQL
  Server que no dice de que campo habla.
*/
public class VentasDescuentosService : IVentasDescuentosService
{
    private readonly IVentasDescuentosRepository _repository;

    public VentasDescuentosService(IVentasDescuentosRepository repository)
    {
        _repository = repository;
    }

    /* ------------------------------ Catalogo ------------------------------ */

    public async Task<ResultadoLegacyDto> CrearDescuentoAsync(
        DescuentoCrearRequest request, CancellationToken ct)
    {
        /*
          EL NOMBRE NO LLEGA A LA BASE, Y SE DICE.

          sp_n_InsertarDescuentos no recibe parametros: crea el renglon en blanco.
          Aceptar el nombre y tirarlo en silencio seria peor que no aceptarlo, asi
          que el nombre se acepta —el contrato lo necesita para el dia que exista
          un sp_w_ que lo guarde— y el resultado lo deja escrito en su mensaje.
        */
        var resultado = await _repository.CrearDescuentoAsync(ct);

        if (!string.IsNullOrWhiteSpace(request.Descuento) && string.IsNullOrEmpty(resultado.Mensaje))
            resultado.Mensaje = "El descuento se creo en blanco: el procedimiento de legacy no recibe el nombre.";

        return resultado;
    }

    public async Task<ResultadoLegacyDto> ActualizarDescuentoAsync(
        DescuentoActualizarRequest request, CancellationToken ct)
    {
        if (request.IdDescuento <= 0)
            throw new ArgumentException("El descuento es obligatorio.");

        return await _repository.ActualizarDescuentoAsync(request, ct);
    }

    public async Task<ResultadoLegacyDto> EliminarDescuentoAsync(
        DescuentoIdRequest request, CancellationToken ct)
    {
        if (request.IdDescuento <= 0)
            throw new ArgumentException("El descuento es obligatorio.");

        return await _repository.EliminarDescuentoAsync(request, ct);
    }

    public async Task<FilasLegacyDto> ConsultarDescuentosAsync(
        DescuentoIdRequest request, CancellationToken ct)
        => await _repository.ConsultarDescuentosAsync(request, ct);

    public async Task<ResultadoLegacyDto> SimularDescuentoAsync(
        DescuentoSimularRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Descuento))
            throw new ArgumentException("El descuento a simular es obligatorio.");

        return await _repository.SimularDescuentoAsync(request, idUsuario, equipo, ct);
    }

    /* ------------------------------- Detalle ------------------------------- */

    public async Task<ResultadoLegacyDto> CrearDetalleAsync(
        DescuentoDetalleCrearRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        ValidarDetalle(request);
        return await _repository.CrearDetalleAsync(request, idUsuario, equipo, ct);
    }

    public async Task<ResultadoLegacyDto> ActualizarDetalleAsync(
        DescuentoDetalleActualizarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (request.IdDescuentoDetalle <= 0)
            throw new ArgumentException("El renglon del detalle es obligatorio.");
        ValidarDetalle(request);

        return await _repository.ActualizarDetalleAsync(request, idUsuario, equipo, ct);
    }

    public async Task<ResultadoLegacyDto> EliminarDetalleAsync(
        DescuentoDetalleEliminarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (request.IdDescuentoDetalle <= 0)
            throw new ArgumentException("El renglon del detalle es obligatorio.");
        if (request.IdDescuento <= 0)
            throw new ArgumentException("El descuento es obligatorio.");

        return await _repository.EliminarDetalleAsync(request, idUsuario, equipo, ct);
    }

    private static void ValidarDetalle(DescuentoDetalleCrearRequest request)
    {
        if (request.IdDescuento <= 0)
            throw new ArgumentException("El descuento es obligatorio.");
        if (request.IdCategoria <= 0)
            throw new ArgumentException("La categoria es obligatoria.");

        /*
          CON `AplicarTodos = 1` LA MARCA PUEDE VENIR VACIA: el procedimiento la
          aplica a todas las marcas de la categoria. Fuera de ese modo hace falta,
          y legacy la pide siempre, incluso cuando va a aplicar a todas.
        */
        if (request.AplicarTodos != 1 && request.IdMarca <= 0)
            throw new ArgumentException("La marca es obligatoria, o pide AplicarTodos = 1.");

        if (!EsPorcentajeValido(request.Porcentaje, out var porcentaje))
            throw new ArgumentException("El porcentaje tiene que ser un numero entre 0 y 100.");

        _ = porcentaje;
    }

    /// <summary>
    /// Si el texto del porcentaje es un numero entre 0 y 100. Se acepta punto o
    /// coma decimal porque la pantalla lo escribe como lo escribe la gente, pero
    /// se interpreta con cultura invariante para no depender del servidor.
    /// Es publico y esta probado sin base en DescuentosPorcentajeTests.
    /// </summary>
    public static bool EsPorcentajeValido(string? texto, out decimal porcentaje)
    {
        porcentaje = 0m;
        if (string.IsNullOrWhiteSpace(texto)) return false;

        var limpio = texto.Trim().Replace(',', '.').Replace("%", string.Empty).Trim();
        if (!decimal.TryParse(limpio, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out porcentaje))
            return false;

        return porcentaje >= 0m && porcentaje <= 100m;
    }

    /* -------------------------- Ajustes de venta -------------------------- */

    public async Task<ResultadoLegacyDto> CrearAjusteAsync(
        VentaDescuentoAjusteCrearRequest request, int idUsuario, CancellationToken ct)
    {
        if (request.IdTipoLiquidacion <= 0)
            throw new ArgumentException("El tipo de liquidacion es obligatorio.");
        if (request.Fecha is null)
            throw new ArgumentException("La fecha es obligatoria.");
        if (request.IdVenta <= 0 && request.Folio <= 0)
            throw new ArgumentException("Hay que indicar la remision o su folio.");

        request.IdsVentaDetalle = FiltrosLegacy.LimpiarIds(request.IdsVentaDetalle);
        if (request.IdsVentaDetalle.Count == 0)
            throw new ArgumentException("Hay que indicar al menos un renglon de la remision.");

        var renglones = request.IdsVentaDetalle.Count;
        MismoLargo("IdsVentaDetalle", renglones, "DescuentoGlobal", request.DescuentoGlobal.Count);
        MismoLargo("IdsVentaDetalle", renglones, "Descuento1", request.Descuento1.Count);
        MismoLargo("IdsVentaDetalle", renglones, "Descuento2", request.Descuento2.Count);
        MismoLargo("IdsVentaDetalle", renglones, "Descuento3", request.Descuento3.Count);
        MismoLargo("IdsVentaDetalle", renglones, "DescuentoAdicional", request.DescuentoAdicional.Count);

        /*
          UN DESCUENTO NO PUEDE SER NEGATIVO NI PASAR DE 100.

          Son porcentajes en cascada: un 110 deja el importe en negativo y el
          procedimiento lo guarda. Un importe negativo en una remision es un abono
          disfrazado de descuento.
        */
        ExigirPorcentajes("DescuentoGlobal", request.DescuentoGlobal);
        ExigirPorcentajes("Descuento1", request.Descuento1);
        ExigirPorcentajes("Descuento2", request.Descuento2);
        ExigirPorcentajes("Descuento3", request.Descuento3);
        ExigirPorcentajes("DescuentoAdicional", request.DescuentoAdicional);

        return await _repository.CrearAjusteAsync(request, idUsuario, ct);
    }

    public async Task<ResultadoLegacyDto> CambiarStatusAjusteAsync(
        VentaDescuentoAjusteStatusRequest request, int idUsuario, CancellationToken ct)
    {
        if (request.IdVentasDescuentosAjustes <= 0)
            throw new ArgumentException("El ajuste es obligatorio.");
        if (request.Fecha is null)
            throw new ArgumentException("La fecha es obligatoria.");

        /*
          SIN USUARIO DE MAC31 NO SE AUTORIZA, Y NO ES UNA POLITICA: ES EL
          PROCEDIMIENTO.

          sp_n_ActualizaStatusVentasDescuentosAjustes solo escribe cuando el id es
          mayor que cero:

              IF @IDUsuarioAutorizacion > 0  UPDATE ...
              IF @IDUsuarioRechazo      > 0  UPDATE ...

          Con 0 no entra en ninguna de las dos ramas, NO cambia nada, y de todas
          formas contesta `SELECT 1 AS result`. O sea: dice que autorizo y no
          autorizo. Comprobado ejecutandolo con 0 y con 3 sobre el mismo renglon.

          El id sale del token, asi que llega en 0 cuando el usuario web no esta
          ligado a un usuario de Mac31 (dbo.Usuarios). En ese caso se para aqui: es
          preferible decir que no se puede a decir que si y no hacer nada. Y ademas,
          en un ajuste de descuento, QUIEN autoriza es justo el dato que despues se
          audita.
        */
        if (idUsuario <= 0)
            throw new ConflictException(
                "Tu usuario web no esta ligado a un usuario de Mac31, y autorizar o "
                + "rechazar un ajuste necesita esa firma.");

        /*
          RECHAZAR EXIGE MOTIVO.

          El procedimiento guarda @MotivoRechazo y es lo unico que le explica al
          vendedor por que le dijeron que no. Legacy no lo pide; su controlador
          solo comprueba que venga uno de los dos ids de usuario.
        */
        if (!request.Autorizar && string.IsNullOrWhiteSpace(request.MotivoRechazo))
            throw new ArgumentException("Para rechazar hace falta el motivo.");

        return await _repository.CambiarStatusAjusteAsync(request, idUsuario, ct);
    }

    public async Task<FilasLegacyDto> SimularAjusteAsync(
        VentaDescuentoAjusteSimularRequest request, int idUsuarioActual, CancellationToken ct)
    {
        request.IdsVentaDetalle = FiltrosLegacy.LimpiarIds(request.IdsVentaDetalle);

        if (request.IdVentasDescuentosAjustes <= 0 && request.IdVenta <= 0 && request.Folio <= 0)
            throw new ArgumentException("Hay que indicar el ajuste, la remision o su folio.");

        if (request.IdsVentaDetalle.Count > 0)
        {
            var renglones = request.IdsVentaDetalle.Count;
            MismoLargo("IdsVentaDetalle", renglones, "DescuentoGlobal", request.DescuentoGlobal.Count);
            MismoLargo("IdsVentaDetalle", renglones, "Descuento1", request.Descuento1.Count);
            MismoLargo("IdsVentaDetalle", renglones, "Descuento2", request.Descuento2.Count);
            MismoLargo("IdsVentaDetalle", renglones, "Descuento3", request.Descuento3.Count);
            MismoLargo("IdsVentaDetalle", renglones, "DescuentoAdicional", request.DescuentoAdicional.Count);
        }

        return await _repository.SimularAjusteAsync(request, idUsuarioActual, ct);
    }

    public async Task<FilasLegacyDto> ConsultarAjustesAsync(
        VentaDescuentoAjusteConsultaRequest request, int idUsuarioActual, CancellationToken ct)
    {
        request.IdsVentasDescuentosAjustes = FiltrosLegacy.LimpiarIds(request.IdsVentasDescuentosAjustes);
        request.IdsTipoLiquidacion = FiltrosLegacy.LimpiarIds(request.IdsTipoLiquidacion);
        request.IdsVenta = FiltrosLegacy.LimpiarIds(request.IdsVenta);
        request.IdsCliente = FiltrosLegacy.LimpiarIds(request.IdsCliente);
        request.IdsAgente = FiltrosLegacy.LimpiarIds(request.IdsAgente);

        /*
          LA PRIORIDAD DE LEGACY, CON NOMBRE: el folio gana sobre el estatus, y el
          estatus gana sobre las fechas. Y una regla suya que si tiene sentido: con
          Status = 1 (PENDIENTES) se tiran las fechas, porque lo pendiente es
          pendiente hoy, no en un rango.
        */
        if (request.FolioInicial > 0)
        {
            request = new VentaDescuentoAjusteConsultaRequest
            {
                FolioInicial = request.FolioInicial,
                FolioFinal = request.FolioFinal > 0 ? request.FolioFinal : request.FolioInicial
            };
        }
        else if (request.Status == 1)
        {
            request = new VentaDescuentoAjusteConsultaRequest { Status = 1 };
        }

        var hayFiltro = request.IdsVentasDescuentosAjustes.Count > 0 || request.IdsVenta.Count > 0
            || request.IdsCliente.Count > 0 || request.IdsAgente.Count > 0
            || request.FolioInicial > 0 || request.Status > 0 || request.FechaInicial is not null;

        if (!hayFiltro)
            throw new ArgumentException("Hay que indicar al menos un filtro: ajuste, remision, cliente, agente, folio, status o rango de fechas.");

        return await _repository.ConsultarAjustesAsync(request, idUsuarioActual, ct);
    }

    private static void MismoLargo(string nombreA, int largoA, string nombreB, int largoB)
    {
        if (largoA != largoB)
            throw new ArgumentException(
                $"Las listas {nombreA} ({largoA}) y {nombreB} ({largoB}) tienen que traer el mismo numero de elementos.");
    }

    private static void ExigirPorcentajes(string nombre, IEnumerable<decimal> valores)
    {
        foreach (var valor in valores)
        {
            if (valor < 0m || valor > 100m)
                throw new ArgumentException($"{nombre} trae un porcentaje fuera de rango ({valor}); tiene que estar entre 0 y 100.");
        }
    }
}
