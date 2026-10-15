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

    /*
      ═══════════════════════════════════════════════════════════════════════
      LA LISTA ES DE LO QUE SE DEJA PASAR, NO DE LO QUE SE TAPA.
      ═══════════════════════════════════════════════════════════════════════

      Empezo siendo lo contrario -una lista de columnas de dinero que anular- y
      se dio la vuelta porque esa forma YA FALLO AL MEDIRLA. Con la lista de
      prohibidas puesta (netoPagado, sueldoSemanal, totalPercepciones,
      totalDeducciones, montoComision, ...) el renglon seguia saliendo con:

          sueldoDiario          357.22
          montoComisionZara   16,556.00
          montoComisionLuro    1,932.00

      que son los mismos pesos por otro nombre: 16,556 + 1,932 es justo el
      "ZARA + LURO = 18,488.00" que imprime el recibo en PDF. Son columnas que
      solo existen en la version Zara del procedimiento, y una lista negra no
      las podia conocer.

      `sp_n_ConsultaEmpleadosTipoSueldoRecibo` devuelve mas de sesenta columnas,
      tiene un gemelo distinto por empresa y lo edita legacy, no este repo. Una
      lista negra ahi esta mal por construccion: la columna de dinero que alguien
      agregue manana sale sola y en silencio, y nadie se enterara hasta que ya
      salio. Con lista blanca, lo nuevo se tapa por omision y como mucho aparece
      un hueco en una pantalla, que se ve y se arregla.

      ASI QUE AQUI SOLO ESTA LO QUE NO ES UN IMPORTE: identificadores, nombres,
      fechas, contadores de dias y el par result/mensaje con el que el batch dice
      si trabajo. Todo lo demas se devuelve en null.

      Los contadores se quedan a proposito: `asistencias`, `inasistencias`,
      `vacaciones`, `numeroDiasLaborados` y `totalLitros` son cantidades, no
      pesos, y son justo lo que necesita quien revisa que el calculo corrio.
    */
    private static readonly HashSet<string> ColumnasSinDinero =
        new(StringComparer.OrdinalIgnoreCase)
        {
            // Identificadores
            "idEmpleado", "idPuesto", "idStatus", "idTipoSueldo", "idChecador",
            "numeroEmpleado", "idAgente", "agente", "id", "idPeriodoTipoSueldo",
            // Identidad del empleado (la gobierna 'empleados.ver', no esta llave)
            "puesto", "nombre", "rfc", "curp", "tipoSueldo",
            // Fechas y periodo
            "fechaInicial", "fechaFinal", "fechaImpresion", "numeroSemana",
            "fechaInicialComFtm", "fechaFinalComFtm",
            // Contadores: dias y litros, no pesos
            "asistencias", "inasistencias", "vacaciones", "faltas", "festivos",
            "numeroDiasLaborados", "numeroDiasFestivosLaborados", "domingo",
            "totalLitros", "totalLitrosFtm",
            // Texto fijo y armado del papel
            "leyenda", "linebreak",
            // Con esto Interpretar sabe si el batch hizo algo: si se anulara,
            // un calculo bueno pasaria por fallido.
            "result", "mensaje", "ErrorParams"
        };

    /// <summary>
    /// Calcula los recibos del periodo y devuelve sus renglones.
    /// </summary>
    /// <param name="puedeVerSueldo">
    /// Si el que pide puede VER los importes, no si puede calcularlos.
    ///
    /// SON DOS AUTORIDADES DISTINTAS Y AQUI VENIAN JUNTAS. Para disparar el
    /// batch basta `nomina.recibos.calcular`; lo que se devolvia era el renglon
    /// CRUDO del procedimiento, con `netoPagado`, `sueldoSemanal`,
    /// `totalPercepciones` y `totalDeducciones` por empleado. O sea que quien
    /// podia recalcular la nomina leia todos los sueldos aunque le hubieran
    /// quitado `empleados.sueldo.ver`, y por una ruta que ni se llama como el
    /// dato que entrega.
    ///
    /// MEDIDO: con el permiso en 'deny' para DEMO en MacZ,
    /// POST /api/nomina/recibos/calcular contestaba 200 con esas columnas
    /// llenas. La ficha de empleados ya las anulaba (EmpleadosService.BorrarDinero)
    /// y el recibo en PDF ya pedia la llave: esta era la tercera puerta al mismo
    /// dato.
    ///
    /// NO SE CONTESTA 403 Y ES A PROPOSITO. El batch ESCRIBE -borra los recibos
    /// del periodo y los vuelve a hacer- y quien tiene permiso de calcular tiene
    /// que poder calcular; negarle la llamada entera le quitaria una funcion que
    /// si le corresponde. Lo que se le quita es solo el dato que no puede ver,
    /// igual que en la ficha: el calculo se hace y se confirma, y las columnas de
    /// importe vuelven en null.
    /// </param>
    public async Task<NominaBatchResponse> CalcularRecibosAsync(
        CalcularRecibosRequest r, bool puedeVerSueldo, CancellationToken ct)
    {
        var esTauro = await _repo.EsTauroAsync(ct);
        var renglones = await _repo.CalcularRecibosAsync(r, esTauro, ct);
        if (!puedeVerSueldo) BorrarDinero(renglones);
        return Interpretar(renglones);
    }

    /// <summary>
    /// Pone en null TODA columna que no este en <see cref="ColumnasSinDinero"/>,
    /// dejando identificadores, nombre, fechas, contadores y el par
    /// result/mensaje intactos.
    ///
    /// Se anulan y no se borran de la lista para que el front siga encontrando la
    /// columna: una clave que desaparece rompe la pantalla, un null se pinta
    /// vacio. Es lo mismo que hace BorrarDinero en EmpleadosService.
    /// </summary>
    private static void BorrarDinero(List<Dictionary<string, object?>> renglones)
    {
        foreach (var fila in renglones)
        {
            // Se recorre una copia de las claves: se escribe sobre el mismo
            // diccionario que se esta leyendo.
            foreach (var clave in fila.Keys.ToList())
            {
                if (!ColumnasSinDinero.Contains(clave))
                    fila[clave] = null;
            }
        }
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
