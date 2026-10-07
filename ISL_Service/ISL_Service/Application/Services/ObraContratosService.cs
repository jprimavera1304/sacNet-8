using System.Globalization;
using ISL_Service.Application.DTOs.EscriturasLegacy;
using ISL_Service.Application.DTOs.ObraContratos;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/// <summary>
/// Reglas de los contratos de obra. Lo que no estaba en el SP y Legacy hacia (o
/// dejaba de hacer) en C# esta aqui, con el porque al lado.
/// </summary>
public class ObraContratosService : IObraContratosService
{
    private readonly IObraContratosRepository _repo;

    private const int LargoContrato = 100;
    private const int LargoDescripcion = 4000;
    private const int LargoClave = 200;
    private const int LargoNombre = 400;

    public ObraContratosService(IObraContratosRepository repo)
    {
        _repo = repo;
    }

    // ==================================================================
    // El contrato
    // ==================================================================

    public Task<List<Dictionary<string, object?>>> ConsultarContratosAsync(ObraContratosFiltro filtro, CancellationToken ct = default)
    {
        ExigirRangoDeFechas(filtro.FechaInicial, filtro.FechaFinal, "fechaInicial", "fechaFinal");
        ExigirRangoDeFechas(filtro.FechaContratacionInicial, filtro.FechaContratacionFinal, "fechaContratacionInicial", "fechaContratacionFinal");
        return _repo.ConsultarContratosAsync(filtro, ct);
    }

    public async Task<ResultadoEscrituraDto> CrearContratoAsync(CrearObraContratoRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        ValidarContrato(request);
        return EscrituraLegacyReglas.Traducir(
            await _repo.InsertarContratoAsync(request, idUsuario, equipo, ct),
            "dar de alta el contrato");
    }

    public async Task<ResultadoEscrituraDto> ActualizarContratoAsync(int id, ActualizarObraContratoRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(id, "idContrato");
        request.IdStatus = EscrituraLegacyReglas.ExigirStatus(request.IdStatus);
        ValidarContrato(request);
        return EscrituraLegacyReglas.Traducir(
            await _repo.ActualizarContratoAsync(id, request, idUsuario, equipo, ct),
            "actualizar el contrato");
    }

    public async Task<ResultadoEscrituraDto> ActualizarContratoPartidasIndividualesAsync(int id, ActualizarObraContratoPartidasIndividualesRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(id, "idContrato");
        return EscrituraLegacyReglas.Traducir(
            await _repo.ActualizarContratoPartidasIndividualesAsync(id, request.PartidasIndividuales, idUsuario, equipo, ct),
            "cambiar el desglose por partidas del contrato");
    }

    public async Task<ResultadoEscrituraDto> ActualizarContratoStatusAsync(int id, ActualizarObraContratoStatusRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(id, "idContrato");
        return EscrituraLegacyReglas.Traducir(
            await _repo.ActualizarContratoStatusAsync(id, request.ContratoCerrado, idUsuario, equipo, ct),
            request.ContratoCerrado ? "cerrar el contrato" : "reabrir el contrato");
    }

    /*
      LO QUE UN CONTRATO DE OBRA TIENE QUE TRAER.

      Los requeridos salen de MCContratosController.cs:36-45, que es donde Legacy
      los validaba. Se conservan tal cual, con dos anadidos:

      * LAS FECHAS TIENEN QUE SER COHERENTES. Legacy comprobaba que las tres
        cadenas no estuvieran vacias y nada mas, asi que un contrato que termina
        antes de empezar se guardaba sin una queja, y despues el programa de obra
        y las estimaciones salian con avances negativos.
      * LOS IMPORTES NO PUEDEN SER NEGATIVOS y el total tiene que cuadrar con
        subtotal + IVA. El SP guarda los tres numeros tal como llegan, sin
        recalcular ninguno: si el cliente manda un total que no es la suma,
        queda un contrato cuyo total no es el de sus importes y el descuadre
        aparece semanas despues en la estimacion.
    */
    private static void ValidarContrato(CrearObraContratoRequest r)
    {
        EscrituraLegacyReglas.ExigirId(r.IdProveedor, "idProveedor");
        EscrituraLegacyReglas.ExigirId(r.IdTipoObra, "idTipoObra");
        EscrituraLegacyReglas.ExigirId(r.IdTipoContratacion, "idTipoContratacion");
        EscrituraLegacyReglas.ExigirId(r.IdTipoRecurso, "idTipoRecurso");
        EscrituraLegacyReglas.ExigirId(r.IdResidenteObra, "idResidenteObra");

        r.Contrato = EscrituraLegacyReglas.ExigirTexto(r.Contrato, "contrato", LargoContrato);
        r.Descripcion = EscrituraLegacyReglas.ExigirTexto(r.Descripcion, "descripcion", LargoDescripcion);

        if (r.FechaContratacion is null) throw new ArgumentException("fechaContratacion es requerida.");
        if (r.FechaInicio is null) throw new ArgumentException("fechaInicio es requerida.");
        if (r.FechaTermino is null) throw new ArgumentException("fechaTermino es requerida.");

        if (r.FechaTermino < r.FechaInicio)
            throw new ArgumentException("fechaTermino no puede ser anterior a fechaInicio.");

        if (r.SubTotal < 0 || r.Iva < 0 || r.Total < 0)
            throw new ArgumentException("Los importes del contrato no pueden ser negativos.");

        ExigirQueElTotalCuadre(r.SubTotal, r.Iva, r.Total, "del contrato");

        if (r.Ejercicio is not null && (r.Ejercicio < 1900 || r.Ejercicio > 2999))
            throw new ArgumentException("ejercicio tiene que ser un ano de cuatro cifras.");
    }

    // ==================================================================
    // Convenios
    // ==================================================================

    public Task<List<Dictionary<string, object?>>> ConsultarConveniosAsync(ObraConveniosFiltro filtro, CancellationToken ct = default)
    {
        ExigirRangoDeFechas(filtro.FechaInicial, filtro.FechaFinal, "fechaInicial", "fechaFinal");
        return _repo.ConsultarConveniosAsync(filtro, ct);
    }

    public async Task<ResultadoEscrituraDto> CrearConvenioAsync(CrearObraConvenioRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        ValidarConvenio(request);
        return EscrituraLegacyReglas.Traducir(
            await _repo.InsertarConvenioAsync(request, idUsuario, equipo, ct),
            "dar de alta el convenio");
    }

    public async Task<ResultadoEscrituraDto> ActualizarConvenioAsync(int id, ActualizarObraConvenioRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(id, "idConvenio");
        request.IdStatus = EscrituraLegacyReglas.ExigirStatus(request.IdStatus);
        ValidarConvenio(request);
        return EscrituraLegacyReglas.Traducir(
            await _repo.ActualizarConvenioAsync(id, request, idUsuario, equipo, ct),
            "actualizar el convenio");
    }

    public async Task<ResultadoEscrituraDto> ActualizarConvenioStatusAsync(int id, ActualizarObraConvenioStatusRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(id, "idConvenio");
        return EscrituraLegacyReglas.Traducir(
            await _repo.ActualizarConvenioStatusAsync(id, request.ConvenioCerrado, idUsuario, equipo, ct),
            request.ConvenioCerrado ? "cerrar el convenio" : "reabrir el convenio");
    }

    private static void ValidarConvenio(CrearObraConvenioRequest r)
    {
        EscrituraLegacyReglas.ExigirId(r.IdContrato, "idContrato");
        EscrituraLegacyReglas.ExigirId(r.IdConvenioMotivo, "idConvenioMotivo");
        r.Convenio = EscrituraLegacyReglas.ExigirTexto(r.Convenio, "convenio", LargoContrato);

        if (r.FechaInicio is null) throw new ArgumentException("fechaInicio es requerida.");
        if (r.FechaTermino is null) throw new ArgumentException("fechaTermino es requerida.");
        if (r.FechaTermino < r.FechaInicio)
            throw new ArgumentException("fechaTermino no puede ser anterior a fechaInicio.");

        /*
          UN CONVENIO SI PUEDE SER NEGATIVO.

          Un convenio modificatorio puede ser de ampliacion o de REDUCCION del
          monto contratado, asi que aqui los importes negativos son legitimos y
          no se bloquean. Lo que si se exige es que el total cuadre con el
          subtotal mas el IVA, por lo mismo que en el contrato.
        */
        ExigirQueElTotalCuadre(r.SubTotal, r.Iva, r.Total, "del convenio");
    }

    // ==================================================================
    // Grupos
    // ==================================================================

    public async Task<ResultadoEscrituraDto> CrearGrupoAsync(CrearObraContratoGrupoRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(request.IdContrato, "idContrato");
        request.Grupo = EscrituraLegacyReglas.ExigirTexto(request.Grupo, "grupo", LargoNombre);
        ValidarLargoOpcional(request.Clave, "clave", LargoClave);
        return EscrituraLegacyReglas.Traducir(
            await _repo.InsertarGrupoAsync(request, idUsuario, equipo, ct),
            "dar de alta el grupo del catalogo de conceptos");
    }

    public async Task<ResultadoEscrituraDto> ActualizarGrupoAsync(int id, ActualizarObraContratoGrupoRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(id, "idContratoGrupo");
        EscrituraLegacyReglas.ExigirId(request.IdContrato, "idContrato");
        request.Grupo = EscrituraLegacyReglas.ExigirTexto(request.Grupo, "grupo", LargoNombre);
        request.IdStatus = EscrituraLegacyReglas.ExigirStatus(request.IdStatus);
        ValidarLargoOpcional(request.Clave, "clave", LargoClave);
        return EscrituraLegacyReglas.Traducir(
            await _repo.ActualizarGrupoAsync(id, request, idUsuario, equipo, ct),
            "actualizar el grupo del catalogo de conceptos");
    }

    public async Task<ResultadoEscrituraDto> ReordenarGrupoAsync(int id, ReordenarRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(id, "idContratoGrupo");
        var delta = LeerDireccion(request);
        return EscrituraLegacyReglas.Traducir(
            await _repo.ReordenarGrupoAsync(id, request.IdContrato, delta, idUsuario, equipo, ct),
            "mover el grupo");
    }

    // ==================================================================
    // Conceptos
    // ==================================================================

    public Task<List<Dictionary<string, object?>>> ConsultarConceptosAsync(ObraContratoConceptosFiltro filtro, CancellationToken ct = default)
        => _repo.ConsultarConceptosAsync(filtro, ct);

    public Task<List<Dictionary<string, object?>>> ConsultarConceptosEstimacionesAsync(ObraConceptosEstimacionesFiltro filtro, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(filtro.IdContratoEstimacion, "idContratoEstimacion");
        EscrituraLegacyReglas.ExigirId(filtro.IdContrato, "idContrato");
        return _repo.ConsultarConceptosEstimacionesAsync(filtro, ct);
    }

    public async Task<ResultadoEscrituraDto> CrearConceptoAsync(CrearObraContratoConceptoRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        ValidarConcepto(request);
        return EscrituraLegacyReglas.Traducir(
            await _repo.InsertarConceptoAsync(request, idUsuario, equipo, ct),
            "dar de alta el concepto");
    }

    public async Task<ResultadoEscrituraDto> ActualizarConceptoAsync(int id, ActualizarObraContratoConceptoRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(id, "idContratoConcepto");
        request.IdStatus = EscrituraLegacyReglas.ExigirStatus(request.IdStatus);
        ValidarConcepto(request);
        return EscrituraLegacyReglas.Traducir(
            await _repo.ActualizarConceptoAsync(id, request, idUsuario, equipo, ct),
            "actualizar el concepto");
    }

    public async Task<ResultadoEscrituraDto> ReordenarConceptoAsync(int id, ReordenarRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(id, "idContratoConcepto");
        var delta = LeerDireccion(request);
        return EscrituraLegacyReglas.Traducir(
            await _repo.ReordenarConceptoAsync(id, request.IdContrato, delta, idUsuario, equipo, ct),
            "mover el concepto");
    }

    /*
      BAJAR UN CONCEPTO SIN BORRARLE LOS DATOS.

      Esta es la correccion mas grande del lote. En Legacy, "eliminar" un
      concepto era PUT /api/mccontratos/conceptos/eliminar, que valida UNICAMENTE
      que venga el IDContratoConcepto (MCContratoConceptosController.cs:95-100) y
      despues llama al MISMO updateAsync que la modificacion normal. Resultado:
      el SP recibe

          @IDStatus = 0        (lo que traiga el cuerpo, y el cuerpo no lo trae)
          @Clave = ''
          @Concepto = ''
          @PrecioUnitario = 0
          @Cantidad = 0

      y hace `UPDATE ... SET Clave = @Clave, Concepto = @Concepto,
      PrecioUnitario = @PrecioUnitario, Cantidad = @Cantidad, Total = @Total`.
      O sea que borrar un concepto le BORRABA la clave, la descripcion, el precio
      unitario y la cantidad, y lo dejaba en IDStatus = 0, que no es ni activo ni
      cancelado: no sale en ninguna pantalla y no hay forma de reactivarlo
      (la rama de reactivacion del SP comprueba `IDStatus = 2`). El renglon queda
      en la tabla, mudo, y el catalogo del contrato ya no suma lo que sumaba.

      Aqui se lee el concepto, se reenvia tal como esta y solo se le cambia el
      status a 2. Dos viajes a la base, ningun dato perdido, y la reactivacion
      sigue funcionando.
    */
    public async Task<ResultadoEscrituraDto> EliminarConceptoAsync(int id, int idContrato, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(id, "idContratoConcepto");
        EscrituraLegacyReglas.ExigirId(idContrato, "idContrato");

        var actuales = await _repo.ConsultarConceptosAsync(
            new ObraContratoConceptosFiltro { IdContratoConcepto = id, IdContrato = idContrato }, ct);
        if (actuales.Count == 0)
            throw new NotFoundException($"El concepto {id} no existe en el contrato {idContrato}.");

        var fila = actuales[0];
        var request = new ActualizarObraContratoConceptoRequest
        {
            IdContratoGrupo = LeerEntero(fila, "idContratoGrupo"),
            IdContrato = idContrato,
            IdUnidadMedida = LeerEntero(fila, "idUnidadMedida"),
            IdContratoPartida = LeerEntero(fila, "idContratoPartida"),
            Clave = LeerCadena(fila, "clave"),
            Concepto = LeerCadena(fila, "concepto"),
            PrecioUnitario = LeerNumero(fila, "precioUnitario"),
            Cantidad = LeerNumero(fila, "cantidad"),
            Ajuste = LeerNumero(fila, "ajuste"),
            ConceptoGlobal = LeerEntero(fila, "conceptoGlobal") == 1,
            IdStatus = 2
        };

        return EscrituraLegacyReglas.Traducir(
            await _repo.ActualizarConceptoAsync(id, request, idUsuario, equipo, ct),
            "dar de baja el concepto");
    }

    public async Task<ResultadoEscrituraDto> CargarConceptosDeArchivoAsync(CargarObraContratoConceptosRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        request.Archivo = EscrituraLegacyReglas.ExigirTexto(request.Archivo, "archivo", LargoDescripcion);
        request.Contrato = EscrituraLegacyReglas.ExigirTexto(request.Contrato, "contrato", LargoDescripcion);
        request.Grupo = EscrituraLegacyReglas.ExigirTexto(request.Grupo, "grupo", LargoDescripcion);

        if (request.PrecioUnitario < 0)
            throw new ArgumentException("El precio unitario no puede ser negativo.");

        return EscrituraLegacyReglas.Traducir(
            await _repo.CargarConceptosDeArchivoAsync(request, idUsuario, equipo, ct),
            "cargar el catalogo de conceptos del archivo");
    }

    /*
      LAS CINCO LISTAS TIENEN QUE MEDIR LO MISMO.

      El SP parte las cinco cadenas por "~" y las recorre a la vez, emparejando
      por posicion. Legacy solo comprobaba que las dos primeras no estuvieran
      vacias (MCContratoConceptosController.cs:35) y mandaba las cinco tal como
      el cliente las hubiera concatenado: si las cantidades traian un elemento
      menos que los conceptos, el ultimo concepto se estimaba con la cantidad de
      otro, o con cero, sin ningun error. En una estimacion de obra eso es dinero
      que se paga mal.

      Las tres ultimas listas pueden venir vacias (son opcionales: excedentes y
      ajustes no siempre aplican); si vienen, tienen que medir igual.
    */
    public async Task<ResultadoEscrituraDto> ActualizarConceptoEstimacionAsync(ActualizarObraConceptoEstimacionRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(request.IdContrato, "idContrato");
        EscrituraLegacyReglas.ExigirId(request.IdContratoEstimacion, "idContratoEstimacion");

        if (request.IdsContratoConcepto.Count == 0)
            throw new ArgumentException("idsContratoConcepto tiene que traer al menos un concepto.");

        var n = request.IdsContratoConcepto.Count;
        if (request.Cantidades.Count != n)
            throw new ArgumentException($"cantidades tiene {request.Cantidades.Count} elementos y idsContratoConcepto tiene {n}: tienen que ser iguales.");

        ExigirListaParalela(request.CantidadesExcedentes, n, "cantidadesExcedentes");
        ExigirListaParalela(request.PorcentajesExcedentes, n, "porcentajesExcedentes");
        ExigirListaParalela(request.Ajustes, n, "ajustes");

        if (request.IdsContratoConcepto.Any(x => x <= 0))
            throw new ArgumentException("idsContratoConcepto no puede traer ceros ni negativos.");

        if (request.IdsContratoConcepto.Distinct().Count() != n)
            throw new ArgumentException("idsContratoConcepto trae el mismo concepto dos veces.");

        if (request.Cantidades.Any(x => x < 0))
            throw new ArgumentException("Las cantidades estimadas no pueden ser negativas.");

        return EscrituraLegacyReglas.Traducir(
            await _repo.ActualizarConceptoEstimacionAsync(request, idUsuario, equipo, ct),
            "guardar las cantidades estimadas");
    }

    private static void ExigirListaParalela(List<decimal> lista, int esperado, string nombre)
    {
        if (lista.Count != 0 && lista.Count != esperado)
            throw new ArgumentException($"{nombre} tiene {lista.Count} elementos: tiene que venir vacia o con {esperado}.");
    }

    private static void ValidarConcepto(CrearObraContratoConceptoRequest r)
    {
        EscrituraLegacyReglas.ExigirId(r.IdContratoGrupo, "idContratoGrupo");
        EscrituraLegacyReglas.ExigirId(r.IdContrato, "idContrato");
        r.Clave = EscrituraLegacyReglas.ExigirTexto(r.Clave, "clave", LargoClave);
        r.Concepto = EscrituraLegacyReglas.ExigirTexto(r.Concepto, "concepto", LargoDescripcion);

        if (r.PrecioUnitario < 0) throw new ArgumentException("El precio unitario no puede ser negativo.");
        if (r.Cantidad < 0) throw new ArgumentException("La cantidad no puede ser negativa.");
    }

    // ==================================================================
    // Partidas del contrato
    // ==================================================================

    public Task<List<Dictionary<string, object?>>> ConsultarContratoPartidasAsync(ObraContratoPartidasFiltro filtro, CancellationToken ct = default)
        => _repo.ConsultarContratoPartidasAsync(filtro, ct);

    public async Task<ResultadoEscrituraDto> CrearContratoPartidaAsync(CrearObraContratoPartidaRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(request.IdContrato, "idContrato");
        EscrituraLegacyReglas.ExigirId(request.IdPartida, "idPartida");
        return EscrituraLegacyReglas.Traducir(
            await _repo.InsertarContratoPartidaAsync(request, idUsuario, equipo, ct),
            "agregar la partida al contrato");
    }

    public async Task<ResultadoEscrituraDto> ActualizarContratoPartidaAsync(int id, ActualizarObraContratoPartidaRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(id, "idContratoPartida");
        EscrituraLegacyReglas.ExigirId(request.IdContrato, "idContrato");
        EscrituraLegacyReglas.ExigirId(request.IdPartida, "idPartida");
        request.IdStatus = EscrituraLegacyReglas.ExigirStatus(request.IdStatus);
        return EscrituraLegacyReglas.Traducir(
            await _repo.ActualizarContratoPartidaAsync(id, request, idUsuario, equipo, ct),
            "actualizar la partida del contrato");
    }

    public async Task<ResultadoEscrituraDto> ReordenarContratoPartidaAsync(int id, ReordenarRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(id, "idContratoPartida");
        var delta = LeerDireccion(request);
        return EscrituraLegacyReglas.Traducir(
            await _repo.ReordenarContratoPartidaAsync(id, request.IdContrato, delta, idUsuario, equipo, ct),
            "mover la partida del contrato");
    }

    // ==================================================================
    // Periodos
    // ==================================================================

    public Task<List<Dictionary<string, object?>>> ConsultarPeriodosAsync(ObraContratoPeriodosFiltro filtro, CancellationToken ct = default)
        => _repo.ConsultarPeriodosAsync(filtro, ct);

    public async Task<ResultadoEscrituraDto> CrearPeriodoAsync(CrearObraContratoPeriodoRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        ValidarPeriodo(request);
        return EscrituraLegacyReglas.Traducir(
            await _repo.InsertarPeriodoAsync(request, idUsuario, equipo, ct),
            "dar de alta el periodo");
    }

    public async Task<ResultadoEscrituraDto> ActualizarPeriodoAsync(int id, ActualizarObraContratoPeriodoRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(id, "idContratoPeriodo");
        request.IdStatus = EscrituraLegacyReglas.ExigirStatus(request.IdStatus);
        ValidarPeriodo(request);
        return EscrituraLegacyReglas.Traducir(
            await _repo.ActualizarPeriodoAsync(id, request, idUsuario, equipo, ct),
            "actualizar el periodo");
    }

    private static void ValidarPeriodo(CrearObraContratoPeriodoRequest r)
    {
        EscrituraLegacyReglas.ExigirId(r.IdContrato, "idContrato");
        r.Periodo = EscrituraLegacyReglas.ExigirTexto(r.Periodo, "periodo", LargoNombre);

        if (r.FechaInicio is null) throw new ArgumentException("fechaInicio es requerida.");
        if (r.FechaTermino is null) throw new ArgumentException("fechaTermino es requerida.");
        if (r.FechaTermino < r.FechaInicio)
            throw new ArgumentException("fechaTermino no puede ser anterior a fechaInicio.");

        ExigirPorcentaje(r.PorcentajeAvanceFisicoProgramado, "porcentajeAvanceFisicoProgramado");
    }

    // ==================================================================
    // Estimaciones
    // ==================================================================

    public async Task<ResultadoEscrituraDto> CrearEstimacionAsync(CrearObraContratoEstimacionRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        ValidarEstimacion(request);
        return EscrituraLegacyReglas.Traducir(
            await _repo.InsertarEstimacionAsync(request, idUsuario, equipo, ct),
            "dar de alta la estimacion");
    }

    public async Task<ResultadoEscrituraDto> ActualizarEstimacionAsync(int id, ActualizarObraContratoEstimacionRequest request, int idUsuario, string equipo, CancellationToken ct = default)
    {
        EscrituraLegacyReglas.ExigirId(id, "idContratoEstimacion");
        request.IdStatus = EscrituraLegacyReglas.ExigirStatus(request.IdStatus);
        ValidarEstimacion(request);
        return EscrituraLegacyReglas.Traducir(
            await _repo.ActualizarEstimacionAsync(id, request, idUsuario, equipo, ct),
            "actualizar la estimacion");
    }

    private static void ValidarEstimacion(CrearObraContratoEstimacionRequest r)
    {
        EscrituraLegacyReglas.ExigirId(r.IdContrato, "idContrato");
        r.Nombre = EscrituraLegacyReglas.ExigirTexto(r.Nombre, "nombre", LargoDescripcion);

        if (r.FechaElaboracion is null) throw new ArgumentException("fechaElaboracion es requerida.");
        if (r.FechaInicioEstimacion is null) throw new ArgumentException("fechaInicioEstimacion es requerida.");
        if (r.FechaTerminoEstimacion is null) throw new ArgumentException("fechaTerminoEstimacion es requerida.");

        if (r.FechaTerminoEstimacion < r.FechaInicioEstimacion)
            throw new ArgumentException("fechaTerminoEstimacion no puede ser anterior a fechaInicioEstimacion.");

        /*
          LA FECHA REAL DE TERMINO SOLO EXISTE SI EL CONTRATO SE TERMINA.

          El SP guarda las dos cosas por separado y no las cruza. Una fecha real
          de termino en un contrato que sigue abierto es una fecha que nadie va a
          volver a mirar y que sale en el acta de entrega; al reves —terminado sin
          fecha— deja el acta sin fecha de recepcion.
        */
        if (r.ContratoTerminado && r.FechaRealTermino is null)
            throw new ArgumentException("Si la estimacion termina el contrato, fechaRealTermino es requerida.");

        if (!r.ContratoTerminado && r.FechaRealTermino is not null)
            throw new ArgumentException("fechaRealTermino solo aplica si la estimacion termina el contrato.");

        if (r.TotalEstimacionProgramada < 0)
            throw new ArgumentException("El total programado de la estimacion no puede ser negativo.");

        ExigirPorcentaje(r.PorcentajeAvanceFisicoReal, "porcentajeAvanceFisicoReal");
    }

    // ==================================================================
    // Reglas compartidas
    // ==================================================================

    /// <summary>
    /// "subir" = una posicion arriba (delta -1), "bajar" = una abajo (+1).
    /// El numero no viaja: ver el bloque de arriba de ObraContratosDtos.
    /// </summary>
    private static int LeerDireccion(ReordenarRequest request)
    {
        EscrituraLegacyReglas.ExigirId(request.IdContrato, "idContrato");

        return (request.Direccion ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "subir" => -1,
            "bajar" => 1,
            _ => throw new ArgumentException("direccion tiene que ser \"subir\" o \"bajar\".")
        };
    }

    private static void ExigirPorcentaje(decimal valor, string nombre)
    {
        if (valor < 0 || valor > 100)
            throw new ArgumentException($"{nombre} tiene que estar entre 0 y 100.");
    }

    private static void ExigirRangoDeFechas(DateOnly? desde, DateOnly? hasta, string nombreDesde, string nombreHasta)
    {
        if (hasta is not null && desde is null)
            throw new ArgumentException($"{nombreHasta} necesita {nombreDesde}.");
        if (desde is not null && hasta is not null && hasta < desde)
            throw new ArgumentException($"{nombreHasta} no puede ser anterior a {nombreDesde}.");
    }

    /// <summary>
    /// subtotal + IVA tiene que dar el total, con un centavo de tolerancia por
    /// el redondeo del front.
    /// </summary>
    private static void ExigirQueElTotalCuadre(decimal subTotal, decimal iva, decimal total, string de)
    {
        if (subTotal == 0 && iva == 0 && total == 0) return;

        var diferencia = Math.Abs(subTotal + iva - total);
        if (diferencia > 0.01m)
            throw new ArgumentException(
                $"El total {de} ({total.ToString(CultureInfo.InvariantCulture)}) no cuadra con subtotal mas IVA " +
                $"({(subTotal + iva).ToString(CultureInfo.InvariantCulture)}).");
    }

    private static void ValidarLargoOpcional(string? valor, string nombre, int maximo)
    {
        if (!string.IsNullOrWhiteSpace(valor) && valor.Trim().Length > maximo)
            throw new ArgumentException($"{nombre} no puede pasar de {maximo} caracteres.");
    }

    private static int LeerEntero(Dictionary<string, object?> fila, string clave)
        => fila.TryGetValue(clave, out var v) && v is not null && v is not DBNull ? Convert.ToInt32(v) : 0;

    private static decimal LeerNumero(Dictionary<string, object?> fila, string clave)
        => fila.TryGetValue(clave, out var v) && v is not null && v is not DBNull ? Convert.ToDecimal(v) : 0m;

    private static string LeerCadena(Dictionary<string, object?> fila, string clave)
        => fila.TryGetValue(clave, out var v) && v is not null && v is not DBNull ? Convert.ToString(v) ?? string.Empty : string.Empty;
}
