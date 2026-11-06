namespace ISL_Service.Application.DTOs.RecepcionCascos;

/*
  RECEPCION DE CASCOS USADOS: LOS DATOS QUE VIAJAN

  Tauro apunta en una hoja de papel, dia por dia, cuantos cascos usados entrega
  cada REPARTIDOR separados por tamano. Esto es esa hoja.

  QUIEN ENTREGA ES EL REPARTIDOR, NO EL AGENTE. Nacio al reves y lo corrigio el
  negocio: el agente es a quien se le factura la ruta; el repartidor es la
  persona que trae los cascos. Son dos catalogos distintos. Con el cambio se
  cayeron el origen (agente / tienda / gallero) y la referencia escrita a mano:
  una recepcion es un dia y un repartidor.

  LO QUE NO TRAE NINGUN DTO DE ENTRADA, A PROPOSITO:

    * El TOTAL de piezas. Lo suma el procedimiento desde el detalle. Si viniera
      de fuera, un total que no cuadra con su propio desglose se guardaria sin
      que nada lo note.
    * El USUARIO y el CANAL. Salen del token y del encabezado de la peticion,
      nunca del cuerpo: si viajaran en el cuerpo, cualquiera podria firmar una
      recepcion a nombre de otro.
    * El NOMBRE del repartidor ni el del tipo de casco. Viaja el id y el
      nombre lo lee el procedimiento del catalogo. Un nombre mandado por el
      cliente es un nombre que puede no corresponder al id.
*/

/* ------------------------------------------------------------------ */
/* Catalogos                                                           */
/* ------------------------------------------------------------------ */

/*
  Un tipo de casco. Son los 8 de [Catalogo TiposUsados] y vienen en el ORDEN DE
  LA HOJA (la columna Orden del catalogo), no en orden alfabetico: la gente
  lleva anos leyendo esas columnas en ese orden.
*/
public class TipoRecepcionCascoDto
{
    public int idTipoUsado { get; set; }
    /* 'MINI CHICO(1)', 'MOTO'... Es el nombre corto, el que cabe en una columna. */
    public string? clave { get; set; }
    public string? nombre { get; set; }
    public int orden { get; set; }
}

/* Un repartidor activo de [Catalogo Repartidores]. */
public class RepartidorRecepcionCascoDto
{
    public int idRepartidor { get; set; }
    public string? nombre { get; set; }
    /* La zona a la que esta asignado. Viaja porque el catalogo la trae y
       distingue a dos personas con nombre parecido. */
    public int? idZona { get; set; }
}

/* ------------------------------------------------------------------ */
/* Consulta                                                            */
/* ------------------------------------------------------------------ */

public class RecepcionCascoDto
{
    public int idRecepcion { get; set; }
    /* El dia que se recibieron los cascos, no el dia que se capturo. */
    public DateTime fecha { get; set; }
    public int idRepartidor { get; set; }
    /* El nombre TAL COMO ESTABA el dia de la captura: se guarda en la propia
       fila, no se lee por JOIN. El catalogo es de legacy y sus filas se
       renombran y se dan de baja. */
    public string? repartidor { get; set; }
    /* El mismo nombre, con la etiqueta que usa la pantalla. */
    public string? quienEntrego { get; set; }
    public int totalPiezas { get; set; }
    public int estatus { get; set; }
    public string? estatusNombre { get; set; }
    public string? observaciones { get; set; }
    public string? motivoCancelacion { get; set; }
    public string? usuarioCreacion { get; set; }
    public DateTime? fechaCreacion { get; set; }
    public string? usuarioModificacion { get; set; }
    public DateTime? fechaModificacion { get; set; }
    public string? usuarioCancelacion { get; set; }
    public DateTime? fechaCancelacion { get; set; }
    public string? canal { get; set; }
    /* "MINI CHICO(1) 5, CHICO(2) 3". Viene armado para no pedir N detalles
       solo para pintar la lista. */
    public string? resumen { get; set; }
    public int tipos { get; set; }
}

/*
  El detalle trae TODOS los tipos activos, con cero en los que no se
  capturaron: la pantalla de modificar necesita la rejilla completa y asi no
  tiene que cruzar dos listas.
*/
public class DetalleRecepcionCascoDto
{
    public int idTipoUsado { get; set; }
    public string? clave { get; set; }
    public string? nombre { get; set; }
    public int orden { get; set; }
    public int piezas { get; set; }
}

public class BitacoraRecepcionCascoDto
{
    public int idBitacora { get; set; }
    public int idRecepcion { get; set; }
    /* ALTA, MODIFICACION, CANCELACION. */
    public string? operacion { get; set; }
    public int? idTipoUsado { get; set; }
    public string? tipo { get; set; }
    public int? piezasAnterior { get; set; }
    public int? piezasNueva { get; set; }
    /* Para lo que no son cantidades: Fecha, Repartidor, Observaciones... */
    public string? campo { get; set; }
    public string? valorAnterior { get; set; }
    public string? valorNuevo { get; set; }
    public string? usuario { get; set; }
    public string? canal { get; set; }
    public DateTime fecha { get; set; }
}

/* ------------------------------------------------------------------ */
/* Escritura                                                           */
/* ------------------------------------------------------------------ */

public class PiezasPorTipoRecepcionRequest
{
    public int IdTipoUsado { get; set; }
    public int Piezas { get; set; }
}

public class CrearRecepcionCascoRequest
{
    public DateTime? Fecha { get; set; }
    public int IdRepartidor { get; set; }
    public string? Observaciones { get; set; }
    public List<PiezasPorTipoRecepcionRequest>? Piezas { get; set; }
}

public class ActualizarRecepcionCascoRequest : CrearRecepcionCascoRequest
{
    public int IdRecepcion { get; set; }
}

/*
  Cancelar acepta una LISTA porque la pantalla deja marcar varias y operarlas
  con un solo boton. Una sola recepcion viaja igual, como lista de uno: asi hay
  un solo camino que probar y no dos que se pueden desincronizar.
*/
public class CancelarRecepcionesCascoRequest
{
    public List<int>? Ids { get; set; }
    public string? Motivo { get; set; }
}

/* ------------------------------------------------------------------ */
/* Respuestas de escritura                                             */
/* ------------------------------------------------------------------ */

public class ResultadoRecepcionCascoDto
{
    public bool ok { get; set; }
    public string? mensaje { get; set; }
    public int idRecepcion { get; set; }
    /* Se guardo, y ademas hay algo que la persona tiene que saber (por ejemplo
       que ese repartidor ya tenia una recepcion ese dia). No es un error. */
    public string? advertencia { get; set; }
}

public class ResultadoCancelacionRecepcionCascoDto
{
    public bool ok { get; set; }
    public string? mensaje { get; set; }
    public int canceladas { get; set; }
    public string? advertencia { get; set; }
}

/* ------------------------------------------------------------------ */
/* Reporte                                                             */
/* ------------------------------------------------------------------ */

/* Un renglon del papel: una recepcion y un tipo de casco. */
public class RenglonReporteRecepcionCascoDto
{
    public int idRecepcion { get; set; }
    public DateTime fecha { get; set; }
    public string? quienEntrego { get; set; }
    public int idRepartidor { get; set; }
    public int idTipoUsado { get; set; }
    public int piezas { get; set; }
    public int totalPiezas { get; set; }
    public string? observaciones { get; set; }
}

public class TotalPorTipoRecepcionCascoDto
{
    public int idTipoUsado { get; set; }
    public string? clave { get; set; }
    public string? nombre { get; set; }
    public int orden { get; set; }
    public int piezas { get; set; }
}

public class DatosReporteRecepcionCascos
{
    public List<RenglonReporteRecepcionCascoDto> Renglones { get; set; } = new();
    public List<TotalPorTipoRecepcionCascoDto> Totales { get; set; } = new();
}
