namespace ISL_Service.Application.DTOs.Nomina;

/*
  LOS TRES BATCHES DE NOMINA QUE EN LEGACY SE LLAMABAN "CONSULTAR" Y "VALIDAR".

  Son las operaciones mas peligrosas de este lote y las tres venian disfrazadas:

  1. POST /api/empleadostiposueldorecibos/consultar NO consulta: CALCULA la
     nomina. El procedimiento borra los recibos del periodo y los vuelve a
     insertar con un cursor. Y legacy lo ejecutaba DOS VECES por peticion —una
     para nomina y otra para comision— sin transaccion.

  2. POST /api/periodostiposueldodias/validar NO valida: CREA los periodos de
     nomina que falten. Se disparaba en el LOGIN de Mac31, asi que cualquiera que
     entrara podia crear periodos; y si dos personas entraban al mismo tiempo, las
     dos los creaban.

  3. POST /api/empleadosprestamo/validar NO valida: es el BATCH que descuenta los
     prestamos de la nomina. Abre transaccion, inserta los pagos y actualiza los
     prestamos.

  Aqui las tres rutas dicen lo que hacen —calcular, generar, descontar—, son POST,
  piden un permiso propio y el id del usuario sale del TOKEN. En legacy ese id
  viajaba en el cuerpo: cualquiera con token podia disparar el descuento de la
  nomina a nombre de otra persona.
*/

/// <summary>El par result/mensaje de legacy, traducido.</summary>
public class NominaBatchResponse
{
    public bool Guardo { get; set; }
    public string Mensaje { get; set; } = string.Empty;

    /// <summary>Los renglones que devolvio el procedimiento.</summary>
    public List<Dictionary<string, object?>> Renglones { get; set; } = new();

    public int Total => Renglones.Count;
}

/// <summary>
/// Calcula los recibos de un periodo.
///
/// `idParametros` es el id de un renglon de la tabla Parametros, que es donde la
/// pantalla deja lo que eligio el usuario (los empleados, el periodo, el rango de
/// fechas) antes de pedir el recibo. Ese es el contrato de legacy y se conserva
/// porque el procedimiento hace un INSERT ... EXEC sp_n_ConsultaParametros con
/// ese id para recuperarlos: no hay forma de pasarle los datos directamente sin
/// reescribir el procedimiento, y los sp_n_ no se tocan.
///
/// Los parametros se guardan con PUT api/parametros, que devuelve el renglon.
/// </summary>
public class CalcularRecibosRequest
{
    /// <summary>El renglon de Parametros con los filtros de la pantalla.</summary>
    public int IdParametros { get; set; }

    /// <summary>
    /// 0 = toma el periodo que diga el renglon de parametros (es lo que legacy
    /// manda para la nomina). Un id distinto de 0 lo sustituye, que es lo que
    /// legacy usa para el periodo de comision.
    /// </summary>
    public int IdPeriodoTipoSueldo { get; set; }
}
