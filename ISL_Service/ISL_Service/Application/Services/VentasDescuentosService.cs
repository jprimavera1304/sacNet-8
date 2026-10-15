using System.Globalization;
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
        /*
          EL NOMBRE NO ES OBLIGATORIO, Y EXIGIRLO ROMPIA LA SIMULACION.

          Aqui habia `if (string.IsNullOrWhiteSpace(request.Descuento)) throw`.
          Medido: Mac31 llama a este procedimiento con el nombre VACIO. Su DTO
          lo inicializa en "" (DescuentoInputDTO.cs:23) y `VentasSimular.
          SimularDescuento` (:85-107) solo llena IDUsuario y NombreEquipo. Y el
          procedimiento lo declara con valor por omision `@Descuento
          varchar(1000) = ''`: es solo el NOMBRE del renglon de prueba, no una
          llave. La llave es usuario + equipo.

          Con la exigencia puesta, el unico cliente que ya lo llamaba
          -`simularDescuento({})` en ModalesDeCascos.tsx:261- recibia un 400
          siempre. O sea que el endpoint no habia funcionado nunca desde el web.

          Y cuando no viene se manda VACIO, no un nombre nuestro: el renglon de
          Mac31 queda con el nombre en blanco y lo que se escribe en la base
          tiene que ser lo mismo. Ponerle "SIMULACION" se vería mejor en el
          catalogo y seria un dato inventado en una tabla compartida.
        */
        request.Descuento ??= string.Empty;

        return await _repository.SimularDescuentoAsync(request, idUsuario, equipo, ct);
    }

    public Task<bool> EsBorradorDeSimulacionAsync(
        int idDescuento, int idUsuario, string equipo, CancellationToken ct)
        => _repository.EsBorradorDeSimulacionAsync(idDescuento, idUsuario, equipo, ct);

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

    /*
      APLICAR UNA LIQUIDACION DE DESCUENTO. ES EL PASO QUE MUEVE EL DINERO.

      TODO SALE DEL AJUSTE GUARDADO Y DEL SIMULADOR, NADA DEL CUERPO
      -------------------------------------------------------------
      El cuerpo trae el id y nada mas. Con el:

        1) se lee el ajuste (sp_n_ConsultaVentasDescuentosAjustes) para saber su
           remision, de que TIPO de descuento es y en que estado esta;
        2) se simula (sp_n_SimulaVentasDescuentosAjustes) para que la BASE calcule
           `DescuentoAplicarReal`, que es el importe que se abona;
        3) se abona (sp_n_InsertarVentasPagos) con el @IDVentasDescuentosAjustes
           de verdad, que es lo que marca la liquidacion como aplicada.

      Es el mismo orden de Mac31: su formulario vuelve a correr el Load -que
      simula- antes de aplicar (LiquidacionesDescuento.cs:1008-1012 y :1050-1062).

      LOS TRES ESTADOS QUE LO BLOQUEAN, con el mensaje de Mac31
      (ConsultarLiquidacionesDescuentos.cs:1043-1066), son los MISMOS que decide
      si el boton se ve (LiquidacionesDescuento.cs:304-375):

        ya aplicada  -> "EL FOLIO n YA ESTA APLICADO."
        rechazada    -> "EL FOLIO n ESTA RECHAZADO."
        sin autorizar-> "EL FOLIO n NO ESTA AUTORIZADO."

      El de "ya aplicada" es el que importa de verdad: sin el, aplicar dos veces
      mete el abono dos veces y la remision baja de saldo el doble. Y en Mac31 es
      un freno del CLIENTE, no del procedimiento: el boton se queda visible
      incluso ya aplicada (:1363-1370) y lo unico que lo evita es el `continue`
      de la pantalla de consulta. Un endpoint no tiene pantalla, asi que el freno
      va aqui.

      DE QUE TIPO ES EL DESCUENTO: 6 GLOBAL / 8 POR LINEA / 4 ADICIONAL
      ----------------------------------------------------------------
      Son los valores de enumTipoPagos de Mac31 (Enums.cs:152 Adicional = 4,
      :158 Global = 6, :164 PorLinea = 8), elegidos en
      LiquidacionesDescuento.cs:1054-1059 segun el radio marcado. Y el radio no lo
      elige el usuario cuando se edita: lo deduce el Load de las tres columnas del
      ajuste guardado (:116 DescuentoGlobal, :162 DescuentoAdicional, :198
      IDVentasDetalle). Aqui se deduce de las mismas tres.

      EL ORDEN DE LA DEDUCCION ES EL DE LEGACY, QUE NO ES EL OBVIO. Alla son tres
      `if` seguidos, cada uno con su `.Checked = true`, asi que GANA EL ULTIMO que
      se cumpla: por linea, luego adicional, luego global. Se respeta, porque si
      una fila vieja trae dos columnas llenas el tipo de pago que escribiria Mac31
      es el de por linea, y el tipo de pago es lo que despues se lee en el corte.
      Ademas "0" NO cuenta como descuento adicional, que es la comprobacion
      literal de :162 (`!= "" && != "0"`).
    */
    public async Task<VentaDescuentoAjusteAplicarResponse> AplicarAjusteAsync(
        VentaDescuentoAjusteAplicarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (request.IdVentasDescuentosAjustes <= 0)
            throw new ArgumentException("El ajuste a aplicar es obligatorio.");

        var consulta = await _repository.ConsultarAjustesAsync(
            new VentaDescuentoAjusteConsultaRequest
            {
                IdsVentasDescuentosAjustes = new List<int> { request.IdVentasDescuentosAjustes }
            },
            idUsuario,
            ct);

        var ajuste = consulta.Rows.Count > 0 ? consulta.Rows[0] : null;
        if (ajuste is null)
            throw new NotFoundException("El ajuste de descuento no existe o no es visible para este usuario.");

        var folio = Numero(ajuste, "Folio");

        if (Numero(ajuste, "IDUsuarioAplicacion") != 0)
            throw new ConflictException($"El folio {folio} ya esta aplicado.");
        if (Numero(ajuste, "IDUsuarioRechazo") != 0)
            throw new ConflictException($"El folio {folio} esta rechazado.");
        if (Numero(ajuste, "IDUsuarioAutorizacion") == 0)
            throw new ConflictException($"El folio {folio} no esta autorizado.");

        var idVenta = Numero(ajuste, "IDVenta");
        if (idVenta <= 0)
            throw new ConflictException($"El folio {folio} no tiene remision: no hay a que abonarle el descuento.");

        var descuentoGlobal = Cadena(ajuste, "DescuentoGlobal");
        var idsVentaDetalle = Cadena(ajuste, "IDVentasDetalle");
        var descuentoAdicional = Cadena(ajuste, "DescuentoAdicional");

        var idTipoPago = ResolverTipoDePagoDelDescuento(descuentoGlobal, idsVentaDetalle, descuentoAdicional);
        if (idTipoPago == 0)
            throw new ConflictException(
                $"El folio {folio} no dice de que tipo es el descuento (sin descuento global, sin renglones y sin adicional), asi que no se puede saber con que tipo de pago abonarlo.");

        /*
          LA SIMULACION VA CON EL ID Y NADA MAS, como la de Mac31 cuando edita un
          ajuste que ya existe: el procedimiento rearma los porcentajes desde el
          renglon guardado. Mandarle los porcentajes desde aqui seria darle la
          oportunidad de aplicar un importe distinto del que se autorizo.
        */
        var simulacion = await _repository.SimularAjusteAsync(
            new VentaDescuentoAjusteSimularRequest
            {
                IdVentasDescuentosAjustes = request.IdVentasDescuentosAjustes
            },
            idUsuario,
            ct);

        var fila = simulacion.Rows.Count > 0 ? simulacion.Rows[0] : null;
        if (fila is null)
            throw new ConflictException(
                $"El folio {folio} no se pudo simular, asi que no se conoce el importe a abonar. No se aplica nada.");

        var abono = Importe(fila, "DescuentoAplicarReal");
        if (abono <= 0m)
            throw new ConflictException(
                $"El folio {folio} simula un descuento de {abono:0.00}: no hay nada que abonar.");

        /* El concepto que se guarda en el pago es el texto que armo la base, el
           mismo que manda Mac31 (LiquidacionesDescuento.cs:1062). */
        var observaciones = Cadena(fila, "TextoDescuentosPorLinea");

        var resultado = await _repository.InsertarPagoDeDescuentoAsync(
            request.IdVentasDescuentosAjustes,
            idVenta,
            idTipoPago,
            abono,
            observaciones,
            idUsuario,
            equipo,
            ct);

        var primera = resultado.Filas.Count > 0 ? resultado.Filas[0] : null;

        return new VentaDescuentoAjusteAplicarResponse
        {
            /* El procedimiento contesta result = 0 cuando guardo y -1 con ROLLBACK
               cuando no; el texto de `mensaje` es el suyo y se publica tal cual. */
            Hecho = resultado.Resultado == 0,
            Mensaje = resultado.Mensaje,
            IdVentasDescuentosAjustes = request.IdVentasDescuentosAjustes,
            IdVenta = idVenta,
            IdTipoPago = idTipoPago,
            Abono = abono,
            SaldoFavorDinero = Importe(primera, "SaldoFavorDineroCliente"),
            SaldoFavorCascos = Importe(primera, "SaldoFavorCascosCliente")
        };
    }

    /// <summary>
    /// 8 por linea, 4 adicional, 6 global, 0 si no se puede saber. El orden es el
    /// de los tres `if` de LiquidacionesDescuento.cs:116, :162 y :198, donde gana
    /// el ultimo que se cumple.
    /// </summary>
    public static int ResolverTipoDePagoDelDescuento(
        string? descuentoGlobal, string? idsVentaDetalle, string? descuentoAdicional)
    {
        const int TipoPagoDescuentoAdicional = 4;
        const int TipoPagoDescuentoGlobal = 6;
        const int TipoPagoDescuentoPorLinea = 8;

        if (!string.IsNullOrWhiteSpace(idsVentaDetalle))
            return TipoPagoDescuentoPorLinea;

        var adicional = (descuentoAdicional ?? string.Empty).Trim();
        if (adicional.Length > 0 && adicional != "0")
            return TipoPagoDescuentoAdicional;

        if (!string.IsNullOrWhiteSpace(descuentoGlobal))
            return TipoPagoDescuentoGlobal;

        return 0;
    }

    private static int Numero(Dictionary<string, object?> fila, string columna)
    {
        if (!fila.TryGetValue(columna, out var valor) || valor is null) return 0;
        return int.TryParse(Convert.ToString(valor, CultureInfo.InvariantCulture), out var n) ? n : 0;
    }

    private static string Cadena(Dictionary<string, object?> fila, string columna)
    {
        if (!fila.TryGetValue(columna, out var valor) || valor is null) return string.Empty;
        return (Convert.ToString(valor, CultureInfo.InvariantCulture) ?? string.Empty).Trim();
    }

    private static decimal Importe(Dictionary<string, object?>? fila, string columna)
    {
        if (fila is null || !fila.TryGetValue(columna, out var valor) || valor is null) return 0m;
        return decimal.TryParse(
            Convert.ToString(valor, CultureInfo.InvariantCulture),
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out var n) ? n : 0m;
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
