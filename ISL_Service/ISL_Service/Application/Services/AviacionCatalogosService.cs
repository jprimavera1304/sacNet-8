using ISL_Service.Application.DTOs.Aviacion;
using ISL_Service.Application.DTOs.EscriturasLegacy;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Application.Services;

/// <summary>
/// Reglas de los catalogos de aviacion. Cada escritura hace dos cosas: llama al
/// sp_n_ y, si esta instalacion es una caja, baja a la replica local las tablas
/// que el cambio toca (ver <see cref="TablasDeAviacion"/>).
/// </summary>
public class AviacionCatalogosService : IAviacionCatalogosService
{
    private readonly IAviacionCatalogosRepository _repo;
    private readonly IConexionDeAviacion _conexion;

    private const int LargoNombre = 400;

    public AviacionCatalogosService(IAviacionCatalogosRepository repo, IConexionDeAviacion conexion)
    {
        _repo = repo;
        _conexion = conexion;
    }

    /*
      ESCRIBIR Y DESPUES SINCRONIZAR.

      Si el SP devolvio -1, NO se sincroniza: Legacy tampoco lo hacia (sale por el
      return del error antes de llegar al bloque de sincronizacion), y bajar
      tablas por un cambio que no ocurrio solo gasta tiempo de la caja.

      Si la sincronizacion falla, Legacy contestaba 500 y el cambio quedaba hecho
      en el central pero no replicado. Aqui se contesta 409 diciendo exactamente
      eso —que el cambio se guardo y la replica no se actualizo— porque "se
      guardo" y "no se guardo" exigen cosas distintas de quien esta en la caja.
    */
    private async Task<ResultadoEscrituraDto> EscribirYSincronizarAsync(
        Func<Task<ResultadoSp>> escritura,
        int[] tablas,
        string queSeIntentaba,
        int idUsuario,
        string equipo,
        CancellationToken ct)
    {
        var resultado = EscrituraLegacyReglas.Traducir(await escritura(), queSeIntentaba);

        if (tablas.Length == 0) return resultado;

        var config = await _conexion.LeerAsync(ct);
        if (!config.EsCaja) return resultado;

        foreach (var tabla in tablas)
        {
            var sync = await _repo.SincronizarDescargaAsync(tabla, idUsuario, equipo, ct);
            if (sync.Result < 0)
                throw new Exceptions.ConflictException(
                    $"El cambio se guardo, pero no se pudo bajar la tabla {tabla} a la caja.",
                    string.IsNullOrWhiteSpace(sync.Mensaje) ? null : sync.Mensaje);
        }

        return resultado;
    }

    // ==================================================================
    // Cajas
    // ==================================================================

    public async Task<ResultadoEscrituraDto> ActualizarCajaAsync(int idCaja, ActualizarAviacionCajaRequest r, int idUsuario, string equipo, CancellationToken ct = default)
    {
        /*
          EL ID DE LA CAJA SE EXIGE, Y EN LEGACY NO.

          PUT /api/catcajas validaba NumCaja, NombreCaja y EquipoCaja, pero NO
          IDCaja (CatCajasController.cs:43-49). Con IDCaja = 0,
          sp_n_ActualizarCaja hace su UPDATE con WHERE IDCaja = 0: o no toca
          nada, o toca el renglon equivocado si alguna vez hubo un 0. En
          cualquier caso el usuario cree que guardo.
        */
        EscrituraLegacyReglas.ExigirId(idCaja, "idCaja");
        if (r.NumCaja <= 0) throw new ArgumentException("numCaja es requerido.");
        r.NombreCaja = EscrituraLegacyReglas.ExigirTexto(r.NombreCaja, "nombreCaja", LargoNombre);
        r.EquipoCaja = EscrituraLegacyReglas.ExigirTexto(r.EquipoCaja, "equipoCaja", LargoNombre);

        return await EscribirYSincronizarAsync(
            () => _repo.ActualizarCajaAsync(idCaja, r, idUsuario, equipo, ct),
            TablasDeAviacion.NoSeSincroniza, "actualizar la caja", idUsuario, equipo, ct);
    }

    // ==================================================================
    // Conceptos
    // ==================================================================

    public Task<List<Dictionary<string, object?>>> ConsultarConceptosAsync(AviacionConceptosFiltro filtro, CancellationToken ct = default)
        => _repo.ConsultarConceptosAsync(filtro, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarConceptoSiguienteCuentaAsync(int idConcepto, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(idConcepto, "idConcepto");
        return _repo.ConsultarConceptoSiguienteCuentaAsync(idConcepto, ct);
    }

    public async Task<ResultadoEscrituraDto> CrearConceptoAsync(CrearAviacionConceptoRequest r, int idUsuario, string equipo, CancellationToken ct = default)
    {
        r.NumConcepto = EscrituraLegacyReglas.ExigirTexto(r.NumConcepto, "numConcepto", 100);
        r.NombreConcepto = EscrituraLegacyReglas.ExigirTexto(r.NombreConcepto, "nombreConcepto", LargoNombre);

        return await EscribirYSincronizarAsync(
            () => _repo.InsertarConceptoAsync(r, idUsuario, equipo, ct),
            TablasDeAviacion.NoSeSincroniza, "dar de alta el concepto", idUsuario, equipo, ct);
    }

    public async Task<ResultadoEscrituraDto> ActualizarConceptoAsync(int idConcepto, ActualizarAviacionConceptoRequest r, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(idConcepto, "idConcepto");
        r.IdStatus = EscrituraLegacyReglas.ExigirStatus(r.IdStatus);
        r.NumConcepto = EscrituraLegacyReglas.ExigirTexto(r.NumConcepto, "numConcepto", 100);
        r.NombreConcepto = EscrituraLegacyReglas.ExigirTexto(r.NombreConcepto, "nombreConcepto", LargoNombre);

        return await EscribirYSincronizarAsync(
            () => _repo.ActualizarConceptoAsync(idConcepto, r, idUsuario, equipo, ct),
            TablasDeAviacion.NoSeSincroniza, "actualizar el concepto", idUsuario, equipo, ct);
    }

    // ==================================================================
    // Cuentas
    // ==================================================================

    public Task<List<Dictionary<string, object?>>> ConsultarCuentasAsync(AviacionCuentasFiltro filtro, bool central, CancellationToken ct = default)
    {
        ExigirDebeHaber(filtro.DebeHaber, obligatorio: false);
        return _repo.ConsultarCuentasAsync(filtro, central, ct);
    }

    public Task<List<Dictionary<string, object?>>> ConsultarCuentaSiguienteNumeroAsync(int idCuenta, CancellationToken ct = default)
        => _repo.ConsultarCuentaSiguienteNumeroAsync(idCuenta, ct);

    public async Task<ResultadoEscrituraDto> CrearCuentaAsync(CrearAviacionCuentaRequest r, int idUsuario, string equipo, CancellationToken ct = default)
    {
        ValidarCuenta(r);
        return await EscribirYSincronizarAsync(
            () => _repo.InsertarCuentaAsync(r, idUsuario, equipo, ct),
            TablasDeAviacion.AltaDeCuenta, "dar de alta la cuenta", idUsuario, equipo, ct);
    }

    public async Task<ResultadoEscrituraDto> ActualizarCuentaAsync(int idCuenta, ActualizarAviacionCuentaRequest r, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(idCuenta, "idCuenta");
        r.IdStatus = EscrituraLegacyReglas.ExigirStatus(r.IdStatus);
        ValidarCuenta(r);
        return await EscribirYSincronizarAsync(
            () => _repo.ActualizarCuentaAsync(idCuenta, r, idUsuario, equipo, ct),
            TablasDeAviacion.CambioDeCuenta, "actualizar la cuenta", idUsuario, equipo, ct);
    }

    public async Task<ResultadoEscrituraDto> AceptarCuentaAsync(int idCuenta, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(idCuenta, "idCuenta");
        return await EscribirYSincronizarAsync(
            () => _repo.AceptarCuentaAsync(idCuenta, idUsuario, equipo, ct),
            TablasDeAviacion.AceptacionDeCuenta, "aceptar la cuenta", idUsuario, equipo, ct);
    }

    public Task<List<Dictionary<string, object?>>> ConsultarTarjetaPorcentajesAsync(int idCuenta, int idStatus, CancellationToken ct = default)
        => _repo.ConsultarTarjetaPorcentajesAsync(idCuenta, idStatus, ct);

    public async Task<ResultadoEscrituraDto> ActualizarTarjetaPorcentajesAsync(ActualizarAviacionTarjetaPorcentajesRequest r, int idUsuario, string equipo, CancellationToken ct = default)
    {
        ValidarTarjetaPorcentajes(r);
        return await EscribirYSincronizarAsync(
            () => _repo.ActualizarTarjetaPorcentajesAsync(r, idUsuario, equipo, ct),
            TablasDeAviacion.CambioDeTarjetaPorcentajes, "guardar los porcentajes de tarjeta", idUsuario, equipo, ct);
    }

    /*
      LOS PORCENTAJES DE TARJETA TIENEN QUE SUMAR 100.

      Es una tabla de distribucion: el cobro con tarjeta se reparte entre varias
      cuentas segun estos porcentajes. Legacy solo comprobaba que las dos listas
      no estuvieran vacias (CatCuentasController.cs:108-112) y le pasaba al SP la
      cadena que el cliente hubiera concatenado. Si los porcentajes sumaban 90, el
      10 por ciento del dinero de cada cobro con tarjeta no se asignaba a ninguna
      cuenta; si sumaban 110, se asignaba dos veces. En los dos casos la
      contabilidad cuadra mal y nadie se entera hasta el cierre.
    */
    public static void ValidarTarjetaPorcentajes(ActualizarAviacionTarjetaPorcentajesRequest r)
    {
        if (r.IdsCuenta.Count == 0)
            throw new ArgumentException("idsCuenta tiene que traer al menos una cuenta.");

        if (r.Porcentajes.Count != r.IdsCuenta.Count)
            throw new ArgumentException($"porcentajes tiene {r.Porcentajes.Count} elementos y idsCuenta tiene {r.IdsCuenta.Count}: tienen que ser iguales.");

        if (r.IdsCuenta.Any(x => x <= 0))
            throw new ArgumentException("idsCuenta no puede traer ceros ni negativos.");

        if (r.IdsCuenta.Distinct().Count() != r.IdsCuenta.Count)
            throw new ArgumentException("idsCuenta trae la misma cuenta dos veces.");

        if (r.Porcentajes.Any(x => x < 0 || x > 100))
            throw new ArgumentException("Cada porcentaje tiene que estar entre 0 y 100.");

        var suma = r.Porcentajes.Sum();
        if (Math.Abs(suma - 100m) > 0.01m)
            throw new ArgumentException($"Los porcentajes suman {suma} y tienen que sumar 100.");
    }

    private static void ValidarCuenta(CrearAviacionCuentaRequest r)
    {
        EscrituraLegacyReglas.ExigirId(r.IdConcepto, "idConcepto");
        r.NumCuenta = EscrituraLegacyReglas.ExigirTexto(r.NumCuenta, "numCuenta", 100);
        r.NombreCuenta = EscrituraLegacyReglas.ExigirTexto(r.NombreCuenta, "nombreCuenta", LargoNombre);
        ExigirDebeHaber(r.DebeHaber, obligatorio: false);

        if (r.Importe < 0) throw new ArgumentException("El importe de la cuenta no puede ser negativo.");

        /*
          GLOBALIZADA E INDIVIDUAL SON EXCLUYENTES.

          Una cuenta se cobra globalizada (un solo renglon por permisionario) o
          individual (uno por economico). Las dos a la vez no quiere decir nada y
          el SP guarda las dos banderas sin mirarse; despues el cobro toma un
          camino o el otro segun el orden de los IF y el resultado depende del SP,
          no de lo que quiso el usuario.
        */
        if (r.CuentaGlobalizada && r.CuentaIndividual)
            throw new ArgumentException("Una cuenta no puede ser globalizada e individual a la vez.");
    }

    /// <summary>"D" o "H", nada mas. El SP lo concatena a un WHERE sin validarlo.</summary>
    private static void ExigirDebeHaber(string? valor, bool obligatorio)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            if (obligatorio) throw new ArgumentException("debeHaber es requerido.");
            return;
        }

        var limpio = valor.Trim().ToUpperInvariant();
        if (limpio != "D" && limpio != "H")
            throw new ArgumentException("debeHaber solo puede ser \"D\" (debe) o \"H\" (haber).");
    }

    // ==================================================================
    // Economicos
    // ==================================================================

    public Task<List<Dictionary<string, object?>>> ConsultarEconomicosAsync(AviacionEconomicosFiltro filtro, bool central, CancellationToken ct = default)
        => _repo.ConsultarEconomicosAsync(filtro, central, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarEconomicoSiguienteIdAsync(int idPermisionario, CancellationToken ct = default)
        => _repo.ConsultarEconomicoSiguienteIdAsync(idPermisionario, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarEconomicoSiguienteNumeroCuentaAsync(int idPermisionario, CancellationToken ct = default)
        => _repo.ConsultarEconomicoSiguienteNumeroCuentaAsync(idPermisionario, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarEconomicosAuxiliaresAsync(AviacionEconomicosAuxiliaresFiltro filtro, CancellationToken ct = default)
        => _repo.ConsultarEconomicosAuxiliaresAsync(filtro, ct);

    public async Task<ResultadoEscrituraDto> CrearEconomicoAsync(CrearAviacionEconomicoRequest r, int idUsuario, string equipo, CancellationToken ct = default)
    {
        ValidarEconomico(r);
        return await EscribirYSincronizarAsync(
            () => _repo.InsertarEconomicoAsync(r, idUsuario, equipo, ct),
            TablasDeAviacion.AltaDeEconomico, "dar de alta el economico", idUsuario, equipo, ct);
    }

    public async Task<ResultadoEscrituraDto> ActualizarEconomicoAsync(int idEconomico, ActualizarAviacionEconomicoRequest r, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(idEconomico, "idEconomico");
        r.IdStatus = EscrituraLegacyReglas.ExigirStatus(r.IdStatus);
        ValidarEconomico(r);
        return await EscribirYSincronizarAsync(
            () => _repo.ActualizarEconomicoAsync(idEconomico, r, idUsuario, equipo, ct),
            TablasDeAviacion.CambioDeEconomico, "actualizar el economico", idUsuario, equipo, ct);
    }

    public async Task<ResultadoEscrituraDto> ActualizarEconomicoAmortizacionAsync(int idEconomico, ActualizarAviacionEconomicoAmortizacionRequest r, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(idEconomico, "idEconomico");
        r.IdStatus = EscrituraLegacyReglas.ExigirStatus(r.IdStatus);
        return await EscribirYSincronizarAsync(
            () => _repo.ActualizarEconomicoAmortizacionAsync(idEconomico, r, idUsuario, equipo, ct),
            TablasDeAviacion.CambioDeAmortizacion, "cambiar los conceptos que paga el economico", idUsuario, equipo, ct);
    }

    public async Task<ResultadoEscrituraDto> ReasignarEconomicoAsync(int idEconomico, ReasignarAviacionEconomicoRequest r, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(idEconomico, "idEconomico");
        EscrituraLegacyReglas.ExigirId(r.IdPermisionarioAnterior, "idPermisionarioAnterior");
        EscrituraLegacyReglas.ExigirId(r.IdPermisionarioNuevo, "idPermisionarioNuevo");

        /*
          REASIGNAR AL MISMO PERMISIONARIO NO ES REASIGNAR.

          El SP escribe el movimiento en el historico y rehace las
          amortizaciones. Con el mismo permisionario de origen y de destino queda
          un renglon de historico que dice que la unidad cambio de dueno sin
          cambiar de dueno, y las amortizaciones se reinician por nada. Legacy
          exigia los dos ids pero no comparaba.
        */
        if (r.IdPermisionarioAnterior == r.IdPermisionarioNuevo)
            throw new ArgumentException("El permisionario nuevo tiene que ser distinto del anterior.");

        return await EscribirYSincronizarAsync(
            () => _repo.ReasignarEconomicoAsync(idEconomico, r, idUsuario, equipo, ct),
            TablasDeAviacion.ReasignacionDeEconomico, "reasignar el economico", idUsuario, equipo, ct);
    }

    public async Task<ResultadoEscrituraDto> AceptarEconomicoAsync(int idEconomico, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(idEconomico, "idEconomico");
        return await EscribirYSincronizarAsync(
            () => _repo.AceptarEconomicoAsync(idEconomico, idUsuario, equipo, ct),
            TablasDeAviacion.AceptacionDeEconomico, "aceptar el economico", idUsuario, equipo, ct);
    }

    private static void ValidarEconomico(CrearAviacionEconomicoRequest r)
    {
        EscrituraLegacyReglas.ExigirId(r.IdPermisionario, "idPermisionario");
        if (r.NumEconomico <= 0) throw new ArgumentException("numEconomico es requerido.");
        r.NombreEconomico = EscrituraLegacyReglas.ExigirTexto(r.NombreEconomico, "nombreEconomico", LargoNombre);

        /*
          UNA UNIDAD QUE NO PAGA NADA NO SE COBRA NUNCA.

          Las tres banderas (recaudacion, amortizacion, inscripcion) son lo que la
          caja usa para saber que cobrarle a la unidad. Con las tres en cero el
          economico existe, sale en el catalogo y no se le puede cobrar: es un
          renglon que parece bueno y no sirve. Legacy no miraba ninguna.
        */
        if (!r.EsRecaudacion && !r.EsAmortizacion && !r.EsInscripcion)
            throw new ArgumentException("El economico tiene que pagar al menos uno de: recaudacion, amortizacion o inscripcion.");
    }

    // ==================================================================
    // Motivos de fianza
    // ==================================================================

    public Task<List<Dictionary<string, object?>>> ConsultarMotivosFianzaAsync(AviacionMotivosFianzaFiltro filtro, bool central, CancellationToken ct = default)
        => _repo.ConsultarMotivosFianzaAsync(filtro, central, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarMotivoFianzaSiguienteIdAsync(int idMotivoFianza, CancellationToken ct = default)
        => _repo.ConsultarMotivoFianzaSiguienteIdAsync(idMotivoFianza, ct);

    public async Task<ResultadoEscrituraDto> CrearMotivoFianzaAsync(CrearAviacionMotivoFianzaRequest r, int idUsuario, string equipo, CancellationToken ct = default)
    {
        if (r.NumMotivoFianza <= 0) throw new ArgumentException("numMotivoFianza es requerido.");
        r.MotivoFianza = EscrituraLegacyReglas.ExigirTexto(r.MotivoFianza, "motivoFianza", LargoNombre);

        /*
          UN MOTIVO ES DE INGRESO O DE EGRESO, Y TIENE QUE SER UNO DE LOS DOS.

          sp_n_ConsultaMotivosFianza filtra por @EsIngreso / @EsEgreso, y asi se
          llenan los dos desplegables de la pantalla de fianzas. Con las dos
          banderas en cero el motivo no sale en ninguno; con las dos en uno sale
          en los dos y el mismo movimiento puede sumar o restar a la fianza segun
          por donde se capture. Y una vez dado de alta ya no se puede corregir:
          sp_n_ActualizarMotivoFianza no recibe estas banderas.
        */
        if (r.EsIngreso == r.EsEgreso)
            throw new ArgumentException("El motivo de fianza tiene que ser de ingreso o de egreso, y solo uno de los dos.");

        return await EscribirYSincronizarAsync(
            () => _repo.InsertarMotivoFianzaAsync(r, idUsuario, equipo, ct),
            TablasDeAviacion.CambioDeMotivoFianza, "dar de alta el motivo de fianza", idUsuario, equipo, ct);
    }

    public async Task<ResultadoEscrituraDto> ActualizarMotivoFianzaAsync(int idMotivoFianza, ActualizarAviacionMotivoFianzaRequest r, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(idMotivoFianza, "idMotivoFianza");
        r.IdStatus = EscrituraLegacyReglas.ExigirStatus(r.IdStatus);
        if (r.NumMotivoFianza <= 0) throw new ArgumentException("numMotivoFianza es requerido.");
        r.MotivoFianza = EscrituraLegacyReglas.ExigirTexto(r.MotivoFianza, "motivoFianza", LargoNombre);

        return await EscribirYSincronizarAsync(
            () => _repo.ActualizarMotivoFianzaAsync(idMotivoFianza, r, idUsuario, equipo, ct),
            TablasDeAviacion.CambioDeMotivoFianza, "actualizar el motivo de fianza", idUsuario, equipo, ct);
    }

    // ==================================================================
    // Operadores
    // ==================================================================

    public Task<List<Dictionary<string, object?>>> ConsultarOperadoresAsync(AviacionOperadoresFiltro filtro, bool central, CancellationToken ct = default)
        => _repo.ConsultarOperadoresAsync(filtro, central, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarOperadorSiguienteNumeroCuentaAsync(int idOperador, CancellationToken ct = default)
        => _repo.ConsultarOperadorSiguienteNumeroCuentaAsync(idOperador, ct);

    public async Task<ResultadoEscrituraDto> CrearOperadorAsync(CrearAviacionOperadorRequest r, int idUsuario, string equipo, CancellationToken ct = default)
    {
        ValidarOperador(r);
        return await EscribirYSincronizarAsync(
            () => _repo.InsertarOperadorAsync(r, idUsuario, equipo, ct),
            TablasDeAviacion.AltaDeOperador, "dar de alta el operador", idUsuario, equipo, ct);
    }

    public async Task<ResultadoEscrituraDto> ActualizarOperadorAsync(int idOperador, ActualizarAviacionOperadorRequest r, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(idOperador, "idOperador");
        r.IdStatus = EscrituraLegacyReglas.ExigirStatus(r.IdStatus);
        ValidarOperador(r);
        return await EscribirYSincronizarAsync(
            () => _repo.ActualizarOperadorAsync(idOperador, r, idUsuario, equipo, ct),
            TablasDeAviacion.CambioDeOperador, "actualizar el operador", idUsuario, equipo, ct);
    }

    public async Task<ResultadoEscrituraDto> AceptarOperadorAsync(int idOperador, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(idOperador, "idOperador");
        return await EscribirYSincronizarAsync(
            () => _repo.AceptarOperadorAsync(idOperador, idUsuario, equipo, ct),
            TablasDeAviacion.AceptacionDeOperador, "aceptar el operador", idUsuario, equipo, ct);
    }

    public async Task<ResultadoEscrituraDto> ActualizarOperadorFianzaAsync(int idOperador, ActualizarAviacionOperadorFianzaRequest r, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(idOperador, "idOperador");
        if (r.ImporteFianza < 0) throw new ArgumentException("El importe de la fianza no puede ser negativo.");

        return await EscribirYSincronizarAsync(
            () => _repo.ActualizarOperadorFianzaAsync(idOperador, r.ImporteFianza, idUsuario, equipo, ct),
            TablasDeAviacion.CambioDeFianzaDeOperador, "guardar la fianza del operador", idUsuario, equipo, ct);
    }

    public async Task<ResultadoEscrituraDto> ActualizarOperadorCuentaAsync(int idOperador, ActualizarAviacionOperadorCuentaRequest r, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(idOperador, "idOperador");
        EscrituraLegacyReglas.ExigirId(r.IdCuenta, "idCuenta");
        if (r.Importe < 0) throw new ArgumentException("El importe no puede ser negativo.");

        return await EscribirYSincronizarAsync(
            () => _repo.ActualizarOperadorCuentaAsync(idOperador, r, idUsuario, equipo, ct),
            TablasDeAviacion.CambioDeCuentaDeOperador, "asignar la cuenta al operador", idUsuario, equipo, ct);
    }

    private static void ValidarOperador(CrearAviacionOperadorRequest r)
    {
        if (r.NumNomina <= 0) throw new ArgumentException("numNomina es requerido.");
        r.OperadorNombre = EscrituraLegacyReglas.ExigirTexto(r.OperadorNombre, "operadorNombre", LargoNombre);
        r.OperadorApellidoPaterno = EscrituraLegacyReglas.ExigirTexto(r.OperadorApellidoPaterno, "operadorApellidoPaterno", LargoNombre);

        /*
          LA FECHA DE ALTA SE EXIGE.

          El SP la recibe como datetime y la guarda; Legacy la mandaba como la
          cadena que trajera el cuerpo, sin validar, y vacia llegaba como "" ->
          la conversion fallaba o guardaba 1900-01-01. Una fecha de alta en 1900
          hace que todos los calculos de antiguedad y de fianza salgan mal.
        */
        if (r.FechaAlta is null) throw new ArgumentException("fechaAlta es requerida.");
    }

    // ==================================================================
    // Permisionarios
    // ==================================================================

    public Task<List<Dictionary<string, object?>>> ConsultarPermisionariosAsync(AviacionPermisionariosFiltro filtro, bool central, CancellationToken ct = default)
        => _repo.ConsultarPermisionariosAsync(filtro, central, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarPermisionarioSiguienteNumeroCuentaAsync(CancellationToken ct = default)
        => _repo.ConsultarPermisionarioSiguienteNumeroCuentaAsync(ct);

    public async Task<ResultadoEscrituraDto> CrearPermisionarioAsync(CrearAviacionPermisionarioRequest r, int idUsuario, string equipo, CancellationToken ct = default)
    {
        if (r.NumPermisionario <= 0) throw new ArgumentException("numPermisionario es requerido.");
        r.NombrePermisionario = EscrituraLegacyReglas.ExigirTexto(r.NombrePermisionario, "nombrePermisionario", LargoNombre);

        return await EscribirYSincronizarAsync(
            () => _repo.InsertarPermisionarioAsync(r, idUsuario, equipo, ct),
            TablasDeAviacion.CambioDePermisionario, "dar de alta el permisionario", idUsuario, equipo, ct);
    }

    public async Task<ResultadoEscrituraDto> ActualizarPermisionarioAsync(int idPermisionario, ActualizarAviacionPermisionarioRequest r, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(idPermisionario, "idPermisionario");
        r.IdStatus = EscrituraLegacyReglas.ExigirStatus(r.IdStatus);
        if (r.NumPermisionario <= 0) throw new ArgumentException("numPermisionario es requerido.");
        r.NombrePermisionario = EscrituraLegacyReglas.ExigirTexto(r.NombrePermisionario, "nombrePermisionario", LargoNombre);

        return await EscribirYSincronizarAsync(
            () => _repo.ActualizarPermisionarioAsync(idPermisionario, r, idUsuario, equipo, ct),
            TablasDeAviacion.CambioDePermisionario, "actualizar el permisionario", idUsuario, equipo, ct);
    }

    public async Task<ResultadoEscrituraDto> AceptarPermisionarioAsync(int idPermisionario, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(idPermisionario, "idPermisionario");
        return await EscribirYSincronizarAsync(
            () => _repo.AceptarPermisionarioAsync(idPermisionario, idUsuario, equipo, ct),
            TablasDeAviacion.CambioDePermisionario, "aceptar el permisionario", idUsuario, equipo, ct);
    }

    // ==================================================================
    // Impresoras
    // ==================================================================

    public Task<List<Dictionary<string, object?>>> ConsultarImpresorasAsync(AviacionImpresorasFiltro filtro, CancellationToken ct = default)
        => _repo.ConsultarImpresorasAsync(filtro, ct);

    public async Task<ResultadoEscrituraDto> ActualizarImpresoraAsync(ActualizarAviacionImpresoraRequest r, int idUsuario, CancellationToken ct = default)
    {
        r.Equipo = EscrituraLegacyReglas.ExigirTexto(r.Equipo, "equipo", 1000);
        r.ImpresoraTickets = EscrituraLegacyReglas.ExigirTexto(r.ImpresoraTickets, "impresoraTickets", LargoNombre);
        r.ImpresoraReportes = EscrituraLegacyReglas.ExigirTexto(r.ImpresoraReportes, "impresoraReportes", LargoNombre);

        return EscrituraLegacyReglas.Traducir(
            await _repo.ActualizarImpresoraAsync(r, idUsuario, ct),
            "guardar las impresoras del equipo");
    }
}
