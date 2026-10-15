using ISL_Service.Application.DTOs.VentasDescuentos;
using ISL_Service.Application.DTOs.VentasEscrituras;

namespace ISL_Service.Application.Interfaces;

/// <summary>
/// Los doce procedimientos de descuentos: catalogo, detalle y ajustes de venta.
/// Cuatro del catalogo NO existen en esta base; estan implementados igual, con su
/// nota, porque legacy falla igual al llamarlos.
/// </summary>
public interface IVentasDescuentosRepository
{
    Task<ResultadoLegacyDto> CrearDescuentoAsync(CancellationToken ct);
    Task<ResultadoLegacyDto> ActualizarDescuentoAsync(DescuentoActualizarRequest request, CancellationToken ct);
    Task<ResultadoLegacyDto> EliminarDescuentoAsync(DescuentoIdRequest request, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarDescuentosAsync(DescuentoIdRequest request, CancellationToken ct);
    Task<ResultadoLegacyDto> SimularDescuentoAsync(DescuentoSimularRequest request, int idUsuario, string equipo, CancellationToken ct);

    /*
      SI ESE ESQUEMA ES EL BORRADOR DE SIMULACION DE ESTE USUARIO Y ESTE EQUIPO.

      Es la pregunta que deja simular sin regalar el permiso del catalogo de
      descuentos. El contexto, medido:

      En Mac31, el editor de descuentos abierto desde cualquiera de las dos
      pantallas de SIMULAR no comprueba permisos: `DetalleDescuentos_Load` hace
      `if (!this.simular) Globales.ValidaOperadorProcesos(this)`
      (DetalleDescuentos.cs:81-82), o sea que con la bandera de simulacion los
      botones de agregar, modificar y eliminar quedan habilitados sin pedir
      nada. Quien puede simular, puede teclear porcentajes.

      Aqui esos tres botones van contra `api/ventas/descuentos/detalle*`, que es
      el MISMO endpoint con el que se edita el esquema de un cliente de verdad.
      Dejar entrar ahi a quien solo tiene permiso de simular seria regalarle la
      edicion de todos los esquemas: eso no se hace ni para que una funcion
      pase una prueba.

      Con esto el permiso no se regala, se acota: quien puede simular escribe EN
      SU PROPIO BORRADOR y en ningun otro. El borrador se identifica por las
      tres cosas que lo definen en la tabla -EsSimular = 1, IDUsuario y
      NombreEquipo, que son la llave con la que `sp_n_SimularDescuento` lo busca
      y lo crea- y el usuario y el equipo salen del token, no del cuerpo.
    */
    Task<bool> EsBorradorDeSimulacionAsync(int idDescuento, int idUsuario, string equipo, CancellationToken ct);


    Task<ResultadoLegacyDto> CrearDetalleAsync(DescuentoDetalleCrearRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> ActualizarDetalleAsync(DescuentoDetalleActualizarRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> EliminarDetalleAsync(DescuentoDetalleEliminarRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<ResultadoLegacyDto> CrearAjusteAsync(VentaDescuentoAjusteCrearRequest request, int idUsuario, CancellationToken ct);
    Task<ResultadoLegacyDto> CambiarStatusAjusteAsync(VentaDescuentoAjusteStatusRequest request, int idUsuario, CancellationToken ct);
    Task<FilasLegacyDto> SimularAjusteAsync(VentaDescuentoAjusteSimularRequest request, int idUsuarioActual, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarAjustesAsync(VentaDescuentoAjusteConsultaRequest request, int idUsuarioActual, CancellationToken ct);

    /// <summary>
    /// El abono de un descuento ya autorizado: sp_n_InsertarVentasPagos con su
    /// `@IDVentasDescuentosAjustes` de verdad, que es lo que marca la liquidacion
    /// como aplicada. El importe lo decide el servicio desde el simulador, no el
    /// cliente.
    /// </summary>
    Task<ResultadoLegacyDto> InsertarPagoDeDescuentoAsync(
        int idVentasDescuentosAjustes,
        int idVenta,
        int idTipoPago,
        decimal abono,
        string? observaciones,
        int idUsuario,
        string equipo,
        CancellationToken ct);
}

/// <summary>Las reglas de descuentos que no son del procedimiento.</summary>
public interface IVentasDescuentosService
{
    Task<ResultadoLegacyDto> CrearDescuentoAsync(DescuentoCrearRequest request, CancellationToken ct);
    Task<ResultadoLegacyDto> ActualizarDescuentoAsync(DescuentoActualizarRequest request, CancellationToken ct);
    Task<ResultadoLegacyDto> EliminarDescuentoAsync(DescuentoIdRequest request, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarDescuentosAsync(DescuentoIdRequest request, CancellationToken ct);
    Task<ResultadoLegacyDto> SimularDescuentoAsync(DescuentoSimularRequest request, int idUsuario, string equipo, CancellationToken ct);

    /// Si ese esquema es el borrador de simulacion de ese usuario y ese equipo.
    /// Ver la nota del repositorio: es lo que deja simular sin regalar el
    /// permiso de editar el catalogo de descuentos.
    Task<bool> EsBorradorDeSimulacionAsync(int idDescuento, int idUsuario, string equipo, CancellationToken ct);

    Task<ResultadoLegacyDto> CrearDetalleAsync(DescuentoDetalleCrearRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> ActualizarDetalleAsync(DescuentoDetalleActualizarRequest request, int idUsuario, string equipo, CancellationToken ct);
    Task<ResultadoLegacyDto> EliminarDetalleAsync(DescuentoDetalleEliminarRequest request, int idUsuario, string equipo, CancellationToken ct);

    Task<ResultadoLegacyDto> CrearAjusteAsync(VentaDescuentoAjusteCrearRequest request, int idUsuario, CancellationToken ct);
    Task<ResultadoLegacyDto> CambiarStatusAjusteAsync(VentaDescuentoAjusteStatusRequest request, int idUsuario, CancellationToken ct);
    Task<FilasLegacyDto> SimularAjusteAsync(VentaDescuentoAjusteSimularRequest request, int idUsuarioActual, CancellationToken ct);
    Task<FilasLegacyDto> ConsultarAjustesAsync(VentaDescuentoAjusteConsultaRequest request, int idUsuarioActual, CancellationToken ct);

    /// <summary>
    /// Aplica una liquidacion de descuento autorizada: es el paso que mete el
    /// abono y baja el saldo de la remision. El cuerpo solo trae el id; el
    /// importe, la remision y el tipo de pago salen del ajuste guardado y del
    /// simulador, y el usuario y el equipo del token.
    /// </summary>
    Task<VentaDescuentoAjusteAplicarResponse> AplicarAjusteAsync(
        VentaDescuentoAjusteAplicarRequest request, int idUsuario, string equipo, CancellationToken ct);
}
