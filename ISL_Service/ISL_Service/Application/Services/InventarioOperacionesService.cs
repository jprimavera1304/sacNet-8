using ISL_Service.Application.DTOs.InventarioOperaciones;
using ISL_Service.Application.DTOs.VentasEscrituras;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Application.Services;

/*
  LAS REGLAS DE INVENTARIO QUE NO ESTAN EN EL PROCEDIMIENTO

  La que importa de verdad: ENTRADA Y SALIDA SON EXCLUYENTES.

  sp_n_InsertarAjusteInventario recibe las dos y escribe un solo renglon de
  ajuste. El controlador de legacy acepta que vengan las dos distintas de cero
  —solo exige que no sean las dos cero— y entonces el movimiento resultante
  depende de como las sume el procedimiento. En un ajuste de existencias eso es
  inaceptable: un ajuste es "entraron 5" o "salieron 5", nunca las dos cosas, y
  si se equivoca el resultado es inventario que no cuadra con lo que hay en el
  piso.
*/
public class InventarioOperacionesService : IInventarioOperacionesService
{
    private readonly IInventarioOperacionesRepository _repository;

    public InventarioOperacionesService(IInventarioOperacionesRepository repository)
    {
        _repository = repository;
    }

    /* ------------------------ Ajustes de inventario ------------------------ */

    public async Task<ResultadoLegacyDto> CrearAjusteAsync(
        AjusteInventarioCrearRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (request.IdAlmacen <= 0) throw new ArgumentException("El almacen es obligatorio.");
        if (request.IdProducto <= 0) throw new ArgumentException("El producto es obligatorio.");
        if (string.IsNullOrWhiteSpace(request.Motivo))
            throw new ArgumentException("El motivo es obligatorio.");

        if (request.Entrada < 0 || request.Salida < 0)
            throw new ArgumentException("Las piezas no pueden ser negativas.");
        if (request.Entrada == 0 && request.Salida == 0)
            throw new ArgumentException("Indica las piezas que entran o las que salen.");
        if (request.Entrada > 0 && request.Salida > 0)
            throw new ArgumentException("Un ajuste es de entrada o de salida, no de las dos.");

        return await _repository.CrearAjusteAsync(request, idUsuario, equipo, ct);
    }

    public async Task<ResultadoLegacyDto> CancelarAjusteAsync(
        AjusteInventarioCancelarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (request.IdAjusteInventario <= 0)
            throw new ArgumentException("El ajuste es obligatorio.");

        return await _repository.CancelarAjusteAsync(request, idUsuario, equipo, ct);
    }

    public async Task<FilasLegacyDto> ConsultarAjustesAsync(
        AjusteInventarioConsultaRequest request, int idUsuarioActual, CancellationToken ct)
    {
        /*
          EL RANGO DE FECHAS ES OBLIGATORIO.

          Sin fechas, el procedimiento recorre el historico entero de ajustes de
          todos los almacenes. En legacy esto era peor y mejor a la vez: peor
          porque no lo pedia, y mejor porque si no llegaban fechas reventaba con
          un error de Substring al formatearlas, asi que la consulta sin filtro no
          llegaba a correr. El error no era una validacion: era un accidente que
          hacia de validacion.
        */
        if (request.FechaInicial is null || request.FechaFinal is null)
            throw new ArgumentException("El rango de fechas es obligatorio.");
        if (request.FechaInicial > request.FechaFinal)
            throw new ArgumentException("La fecha inicial no puede ser posterior a la final.");

        request.IdsGrupoCategoria = FiltrosLegacy.LimpiarIds(request.IdsGrupoCategoria);
        request.IdsCategoria = FiltrosLegacy.LimpiarIds(request.IdsCategoria);
        request.IdsMarca = FiltrosLegacy.LimpiarIds(request.IdsMarca);
        request.IdsAlmacen = FiltrosLegacy.LimpiarIds(request.IdsAlmacen);

        return await _repository.ConsultarAjustesAsync(request, idUsuarioActual, ct);
    }

    /* ----------------------- Inventario de pedidos ----------------------- */

    public async Task<ResultadoLegacyDto> CrearInventarioPedidoAsync(
        InventarioPedidoCrearRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (request.Partidas.Count == 0)
            throw new ArgumentException("Hay que capturar al menos un producto.");

        foreach (var partida in request.Partidas)
        {
            if (partida.IdProducto <= 0)
                throw new ArgumentException("Hay una partida sin producto.");
            if (partida.CantidadInventario < 0 || partida.CantidadVentas < 0
                || partida.CantidadFaltante < 0 || partida.CantidadPedido < 0)
                throw new ArgumentException("Las cantidades no pueden ser negativas.");
        }

        /*
          UN PRODUCTO NO PUEDE VENIR DOS VECES.

          El procedimiento cruza las listas por posicion e inserta un renglon por
          cada una, asi que el mismo producto dos veces da dos renglones con dos
          conteos distintos del mismo producto. Despues, "cuantos hay" tiene dos
          respuestas.
        */
        var repetido = request.Partidas
            .GroupBy(p => p.IdProducto)
            .FirstOrDefault(g => g.Count() > 1);
        if (repetido is not null)
            throw new ArgumentException($"El producto {repetido.Key} viene mas de una vez.");

        if (request.IdStatus is not (1 or 2))
            throw new ArgumentException("El status tiene que ser 1 (activo) o 2 (cancelado).");

        return await _repository.CrearInventarioPedidoAsync(request, idUsuario, equipo, ct);
    }

    public async Task<ResultadoLegacyDto> CancelarInventarioPedidoAsync(
        InventarioPedidoCancelarRequest request, int idUsuario, string equipo, CancellationToken ct)
    {
        if (request.IdInventarioPedido <= 0)
            throw new ArgumentException("El inventario de pedidos es obligatorio.");

        return await _repository.CancelarInventarioPedidoAsync(request, idUsuario, equipo, ct);
    }

    public async Task<FilasLegacyDto> ConsultarInventarioPedidosAsync(
        InventarioPedidoConsultaRequest request, CancellationToken ct)
    {
        var filtro = request;
        if (request.IdInventarioPedido > 0)
            filtro = new InventarioPedidoConsultaRequest { IdInventarioPedido = request.IdInventarioPedido };

        if (filtro.IdInventarioPedido <= 0 && filtro.FechaInicial is null)
            throw new ArgumentException("Hay que indicar el inventario o un rango de fechas.");

        return await _repository.ConsultarInventarioPedidosAsync(filtro, ct);
    }

    /* --------------------- Catalogo de tipos de casco --------------------- */

    public async Task<ResultadoLegacyDto> CrearTipoUsadoAsync(
        TipoUsadoCrearRequest request, CancellationToken ct)
    {
        ValidarTipoUsado(request);
        return await _repository.CrearTipoUsadoAsync(request, ct);
    }

    public async Task<ResultadoLegacyDto> ActualizarTipoUsadoAsync(
        TipoUsadoActualizarRequest request, CancellationToken ct)
    {
        if (request.IdTipoUsado <= 0)
            throw new ArgumentException("El tipo de casco es obligatorio.");
        ValidarTipoUsado(request);

        var actual = await _repository.LeerTipoUsadoAsync(request.IdTipoUsado, ct);
        if (actual is null)
            throw new NotFoundException("El tipo de casco no existe.");

        /*
          SE CONSERVA EL ESTATUS QUE TENIA.

          sp_n_ActualizarTipoUsado recibe @IDStatus y sobrescribe el renglon, asi
          que si "actualizar" lo tomara del cuerpo, cambiar un precio podria
          inhabilitar el tipo sin que nadie lo pidiera. El estatus se mueve por su
          propio endpoint. Es exactamente el defecto que tenia legacy, donde el
          IDStatus venia en el cuerpo y el DELETE y el PUT eran el mismo codigo.
        */
        return await _repository.ActualizarTipoUsadoAsync(request, actual.IdStatus, ct);
    }

    public async Task<ResultadoLegacyDto> CambiarStatusTipoUsadoAsync(
        TipoUsadoCambiarStatusRequest request, CancellationToken ct)
    {
        if (request.IdTipoUsado <= 0)
            throw new ArgumentException("El tipo de casco es obligatorio.");
        if (request.IdStatus is not (1 or 2))
            throw new ArgumentException("El status tiene que ser 1 (activo) o 2 (inactivo).");

        /*
          SE LEE EL RENGLON Y SE VUELVE A ESCRIBIR IGUAL, SOLO CON OTRO ESTATUS.

          El procedimiento no tiene un "cambiar status": tiene un actualizar que
          sobrescribe los doce campos. En legacy eso lo resolvia el cliente
          mandando el DTO completo, y cuando no lo mandaba —el DELETE no valida
          nada— el tipo de casco quedaba con precios, bonificacion y peso en cero.
          Un casco que vale 0 se cobra a 0: no es un renglon inactivo, es un
          renglon mentiroso.
        */
        var guardado = await _repository.LeerTipoUsadoAsync(request.IdTipoUsado, ct);
        if (guardado is null)
            throw new NotFoundException("El tipo de casco no existe.");

        if (guardado.IdStatus == request.IdStatus)
            throw new ConflictException("El tipo de casco ya tiene ese status.");

        return await _repository.ActualizarTipoUsadoAsync(guardado.Datos, request.IdStatus, ct);
    }

    private static void ValidarTipoUsado(TipoUsadoCrearRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TipoUsado))
            throw new ArgumentException("El nombre del tipo de casco es obligatorio.");

        /*
          EL NOMBRE CORTO ES OBLIGATORIO, Y LEGACY LO PEDIA SIN PEDIRLO.

          Su servicio hace TipoUsadoCorto.Replace("'","") sin comprobar nada, y
          el DTO no lo inicializa: si el JSON no lo trae, el endpoint contesta 400
          con un NullReferenceException y su stack. El dato era obligatorio de
          hecho; aqui lo es de derecho y con un mensaje que se entiende.
        */
        if (string.IsNullOrWhiteSpace(request.TipoUsadoCorto))
            throw new ArgumentException("El nombre corto es obligatorio.");

        if (request.VentaCostoCargoSinIva < 0 || request.VentaCostoCargoConIva < 0
            || request.VentaCostoCreditoSinIva < 0 || request.VentaCostoCreditoConIva < 0
            || request.Bonificacion < 0 || request.CostoExcedentes < 0 || request.PesoKg < 0)
            throw new ArgumentException("Los importes y el peso no pueden ser negativos.");
    }
}
