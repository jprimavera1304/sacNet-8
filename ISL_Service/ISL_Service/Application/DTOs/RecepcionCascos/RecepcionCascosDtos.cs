namespace ISL_Service.Application.DTOs.RecepcionCascos;

/*
  RECEPCION DE CASCOS USADOS: LOS DATOS QUE VIAJAN

  Tauro apunta en una hoja de papel, dia por dia, cuantos cascos usados le
  entrega cada agente separados por tamano. La hoja lleva ademas dos apartados
  propios: lo que llega de tiendas y lo de los galleros. Esto es esa hoja.

  LO QUE NO TRAE NINGUN DTO DE ENTRADA, A PROPOSITO:

    * El TOTAL de piezas. Lo suma el procedimiento desde el detalle. Si viniera
      de fuera, un total que no cuadra con su propio desglose se guardaria sin
      que nada lo note.
    * El USUARIO y el CANAL. Salen del token y del encabezado de la peticion,
      nunca del cuerpo: si viajaran en el cuerpo, cualquiera podria firmar una
      recepcion a nombre de otro.
    * El NOMBRE del agente ni el del tipo de casco. Viajan los ids y los
      nombres se leen del catalogo al consultar. Un nombre mandado por el
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

public class AgenteRecepcionCascoDto
{
    public int idAgente { get; set; }
    /* El numero visible del agente. Puede repetirse entre agentes. */
    public int numero { get; set; }
    public string? nombre { get; set; }
}

/* ------------------------------------------------------------------ */
/* Consulta                                                            */
/* ------------------------------------------------------------------ */

public class RecepcionCascoDto
{
    public int idRecepcion { get; set; }
    /* El dia que se recibieron los cascos, no el dia que se capturo. */
    public DateTime fecha { get; set; }
    /* 1 AGENTE, 2 TIENDA, 3 GALLERO. */
    public int idOrigen { get; set; }
    public string? origen { get; set; }
    public int? idAgente { get; set; }
    public string? agente { get; set; }
    /* El nombre de la tienda o del gallero, que no tienen catalogo. */
    public string? referencia { get; set; }
    /* El agente cuando lo hay, y si no la referencia: es la columna que se lee. */
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
    /* Para lo que no son cantidades: Fecha, Origen, Agente, Observaciones... */
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
    public int IdOrigen { get; set; }
    public int? IdAgente { get; set; }
    public string? Referencia { get; set; }
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
       que ese agente ya tenia una recepcion ese dia). No es un error. */
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
    public int idOrigen { get; set; }
    public string? origen { get; set; }
    public string? quienEntrego { get; set; }
    public int? idAgente { get; set; }
    public int? numeroAgente { get; set; }
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
