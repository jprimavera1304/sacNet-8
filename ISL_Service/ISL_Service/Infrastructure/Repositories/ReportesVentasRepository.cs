using System.Data;
using ISL_Service.Application.DTOs.Reportes;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Infrastructure.Reports;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

public partial class ReportesVentasRepository : IReportesVentasRepository
{
    private readonly IConfiguration _configuration;
    private readonly ICentrosServicioRepository _centros;

    public ReportesVentasRepository(IConfiguration configuration, ICentrosServicioRepository centros)
    {
        _configuration = configuration;
        _centros = centros;
    }

    /*
      DE UN IDCentro AL NOMBRE DE SU BASE.

      Devuelve "" para la principal, que es lo que quiere decir "sin centro".
      Si el Id no existe o esta inactivo tambien devuelve "" — no se inventa una
      base ni se truena: se contesta con los datos de la matriz, que es el
      comportamiento de siempre.

      Es el UNICO camino del cliente a un nombre de base: llega un numero, sale
      un nombre de la tabla. Nunca al reves.
    */
    private async Task<string> BaseDeCentroAsync(int idCentro, CancellationToken ct)
    {
        if (idCentro <= 0) return "";
        var centro = await _centros.BuscarAsync(idCentro, ct);
        return centro?.Base ?? "";
    }

    public async Task<ReportesVentasCatalogosResponse> ConsultarCatalogosAcumuladoresProductosAsync(
        int? idGrupoCategoria,
        IReadOnlyCollection<int>? idCategorias,
        CancellationToken ct = default)
    {
        var grupoCategoria = idGrupoCategoria.GetValueOrDefault(GrupoCategoriaAcumuladores);
        if (grupoCategoria <= 0) grupoCategoria = GrupoCategoriaAcumuladores;

        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var constants = await ConsultarConstantesAsync(conn, ct);
        var empresas = await ConsultarEmpresasAsync(conn, ct);
        var almacenes = await ConsultarAlmacenesAsync(conn, ct);
        var agentes = await ConsultarAgentesAsync(conn, ct);
        var proveedores = await ConsultarProveedoresAsync(conn, ct);
        var categorias = await ConsultarGruposCategoriasAsync(conn, ct);
        var subcategorias = await ConsultarSubcategoriasAsync(conn, grupoCategoria, ct);
        var selectedCategorias = NormalizeCatalogIds(idCategorias);
        if (selectedCategorias.Count == 0)
            selectedCategorias = subcategorias.Select(x => x.IDCategoria).Distinct().ToList();
        var marcas = await ConsultarMarcasPorCategoriasAsync(conn, grupoCategoria, selectedCategorias, ct);

        return new ReportesVentasCatalogosResponse
        {
            FechaOperacion = constants.FechaOperacion,
            Empresas = empresas,
            Almacenes = almacenes,
            Agentes = agentes,
            Categorias = categorias,
            Subcategorias = subcategorias,
            Marcas = marcas,
            Documentos = BuildDocumentos(),
            Proveedores = proveedores
        };
    }

    public async Task<ReportesVentasCatalogosResponse> ConsultarCatalogosRemisionesAsync(CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var constants = await ConsultarConstantesAsync(conn, ct);
        var empresas = await ConsultarEmpresasAsync(conn, ct);
        var agentes = await ConsultarAgentesAsync(conn, ct);
        var usuarios = await ConsultarUsuariosAsync(conn, ct);
        var repartidores = await ConsultarRepartidoresAsync(conn, ct);
        var proveedores = await ConsultarProveedoresAsync(conn, ct);
        var autos = await ConsultarAutosAsync(conn, ct);
        var tiposGasto = await ConsultarTiposGastoAsync(conn, ct);
        var tiposPago = await ConsultarTiposPagoAsync(conn, ct);

        return new ReportesVentasCatalogosResponse
        {
            FechaOperacion = constants.FechaOperacion,
            Empresas = empresas,
            Agentes = agentes,
            Usuarios = usuarios,
            Repartidores = repartidores,
            Autos = autos,
            TiposGasto = tiposGasto,
            TiposPago = tiposPago,
            Documentos = BuildDocumentosConTodos(),
            StatusFolios = BuildStatusFolios(),
            Proveedores = proveedores
        };
    }

    public async Task<List<ReportesVentasClienteItem>> ConsultarClientesAsync(
        int? numero,
        int? idCliente,
        CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        return await ConsultarClientesAsync(conn, numero, idCliente, ct);
    }

    public async Task<ReportesVentasGenerateResponse> GenerarAcumuladoresProductosAsync(
        ReportesVentasAcumuladoresProductosRequest request,
        CancellationToken ct = default)
    {
        var idReporte = ResolveReporte(request);

        await using var conn = GetConnection(await BaseDeCentroAsync(request.IdCentro, ct));
        await conn.OpenAsync(ct);

        var legacyParams = await BuildLegacyParamsAsync(conn, idReporte, request, ct);
        var parametroId = await GuardarParametrosAsync(conn, idReporte, legacyParams, ct);
        var config = await ConsultarConfiguracionAsync(conn, idReporte, ct);

        return new ReportesVentasGenerateResponse
        {
            IDReporte = idReporte,
            NombreReporte = config.NombreReporte,
            StoredProcedure = config.StoredProcedure,
            ParametrosLegacy = parametroId.ToString(),
            Salida = (request.Salida ?? "pantalla").Trim().ToLowerInvariant()
        };
    }

    public async Task<ReportesVentasGenerateResponse> GenerarRemisionesAsync(
        ReportesVentasRemisionesRequest request,
        CancellationToken ct = default)
    {
        var idReporte = ResolveReporteRemisiones(request);

        await using var conn = GetConnection(await BaseDeCentroAsync(request.IdCentro, ct));
        await conn.OpenAsync(ct);

        var legacyParams = await BuildRemisionesLegacyParamsAsync(conn, request, ct);
        var parametroId = await GuardarParametrosAsync(conn, idReporte, legacyParams, ct);
        var config = await ConsultarConfiguracionAsync(conn, idReporte, ct);

        return new ReportesVentasGenerateResponse
        {
            IDReporte = idReporte,
            NombreReporte = config.NombreReporte,
            StoredProcedure = config.StoredProcedure,
            ParametrosLegacy = parametroId.ToString(),
            Salida = (request.Salida ?? "pantalla").Trim().ToLowerInvariant()
        };
    }

    public async Task<ReportesVentasGenerateResponse> GenerarFoliosAsync(
        ReportesVentasFoliosRequest request,
        CancellationToken ct = default)
    {
        var idReporte = ResolveReporteFolios(request);

        await using var conn = GetConnection(await BaseDeCentroAsync(request.IdCentro, ct));
        await conn.OpenAsync(ct);

        var legacyParams = await BuildDocumentosVentaLegacyParamsAsync(
            conn, idReporte, request, request.EstatusFolio, request.IDUsuarios, ct);
        var parametroId = await GuardarParametrosAsync(conn, idReporte, legacyParams, ct);
        var config = await ConsultarConfiguracionAsync(conn, idReporte, ct);

        return new ReportesVentasGenerateResponse
        {
            IDReporte = idReporte,
            NombreReporte = config.NombreReporte,
            StoredProcedure = config.StoredProcedure,
            ParametrosLegacy = parametroId.ToString(),
            Salida = (request.Salida ?? "pantalla").Trim().ToLowerInvariant()
        };
    }

    public async Task<ReportesVentasGenerateResponse> GenerarFacturasAsync(
        ReportesVentasFacturasRequest request,
        CancellationToken ct = default)
    {
        var idReporte = ResolveReporteFacturas(request);

        await using var conn = GetConnection(await BaseDeCentroAsync(request.IdCentro, ct));
        await conn.OpenAsync(ct);

        var legacyParams = await BuildDocumentosVentaLegacyParamsAsync(
            conn,
            idReporte,
            request,
            request.EstatusFolio,
            request.IDUsuarios,
            ct,
            formato: 0,
            tipo: ResolveTipoFactura(request.TipoFactura));
        var parametroId = await GuardarParametrosAsync(conn, idReporte, legacyParams, ct);
        var config = await ConsultarConfiguracionAsync(conn, idReporte, ct);

        return new ReportesVentasGenerateResponse
        {
            IDReporte = idReporte,
            NombreReporte = config.NombreReporte,
            StoredProcedure = config.StoredProcedure,
            ParametrosLegacy = parametroId.ToString(),
            Salida = (request.Salida ?? "pantalla").Trim().ToLowerInvariant()
        };
    }

    public async Task<ReportesVentasGenerateResponse> GenerarConcentradosAsync(
        ReportesVentasConcentradosRequest request,
        CancellationToken ct = default)
    {
        var idReporte = ResolveReporteConcentrados(request);

        await using var conn = GetConnection(await BaseDeCentroAsync(request.IdCentro, ct));
        await conn.OpenAsync(ct);

        var legacyParams = BuildConcentradosLegacyParams(request, request.IDRepartidores);
        var parametroId = await GuardarParametrosAsync(conn, idReporte, legacyParams, ct);
        var config = await ConsultarConfiguracionAsync(conn, idReporte, ct);

        return new ReportesVentasGenerateResponse
        {
            IDReporte = idReporte,
            NombreReporte = config.NombreReporte,
            StoredProcedure = config.StoredProcedure,
            ParametrosLegacy = parametroId.ToString(),
            Salida = (request.Salida ?? "pantalla").Trim().ToLowerInvariant()
        };
    }

    public async Task<ReportesVentasGenerateResponse> GenerarCobranzaAsync(
        ReportesVentasCobranzaRequest request,
        CancellationToken ct = default)
    {
        await using var conn = GetConnection(await BaseDeCentroAsync(request.IdCentro, ct));
        await conn.OpenAsync(ct);

        // El id se resuelve DESPUES de abrir la conexion porque depende de la
        // empresa (1160 en Tauro, 1161 en Zaragoza), y eso lo dice la base.
        var idReporte = ResolveReporteCobranza(await ConsultarConstantesAsync(conn, ct));

        var legacyParams = await BuildCobranzaLegacyParamsAsync(conn, request, request.CobranzaStatus, ct);
        var parametroId = await GuardarParametrosAsync(conn, idReporte, legacyParams, ct);
        var config = await ConsultarConfiguracionAsync(conn, idReporte, ct);

        return new ReportesVentasGenerateResponse
        {
            IDReporte = idReporte,
            NombreReporte = config.NombreReporte,
            StoredProcedure = config.StoredProcedure,
            ParametrosLegacy = parametroId.ToString(),
            Salida = (request.Salida ?? "pantalla").Trim().ToLowerInvariant()
        };
    }

    public async Task<ReportesVentasGenerateResponse> GenerarLegacyVentasAsync(
        ReportesVentasLegacyRequest request,
        CancellationToken ct = default)
    {
        await using var conn = GetConnection(await BaseDeCentroAsync(request.IdCentro, ct));
        await conn.OpenAsync(ct);

        // La conexion se abre ANTES de resolver el id porque Pagos y Listas de
        // Precios eligen entre variantes segun Constantes (que empresa es esta).
        var idReporte = await ResolveReporteLegacyVentasAsync(conn, request, ct);

        /*
          EL 118 SOLO EXISTE EN EXCEL, Y NO ES UNA DECISION DE DISEÑO: ES EL SP.

          Mac31 dice por que y lo fuerza igual: "Acumuladores, Moto y Lubricantes
          no caben en pantalla, por eso se mandan solo a Excel"
          (ReportesMenu.cs:3597-3601). Comprobado en Produccion_svr: con
          Presentacion = 0 el propio sp_n_rptClienteCompraAcumMotoLubMensual
          revienta con "Incorrect syntax near the keyword 'FROM'" (su rama de
          pantalla arma mal el SQL); con Presentacion = 3 entrega el xlsx
          completo. El SP es de legacy y no se toca, asi que se le pide lo unico
          que sabe contestar.
        */
        if (idReporte == ReporteClientesAcumMotoLubMensual)
            request.Salida = "excel";

        var legacyParams = await BuildLegacyVentasParamsAsync(conn, idReporte, request, ct);
        var parametroId = await GuardarParametrosAsync(conn, idReporte, legacyParams, ct);
        var config = await ConsultarConfiguracionAsync(conn, idReporte, ct);

        return new ReportesVentasGenerateResponse
        {
            IDReporte = idReporte,
            NombreReporte = config.NombreReporte,
            StoredProcedure = config.StoredProcedure,
            ParametrosLegacy = parametroId.ToString(),
            Salida = (request.Salida ?? "pantalla").Trim().ToLowerInvariant()
        };
    }

    public async Task<ReportesVentasPreviewResponse> ConsultarAcumuladoresProductosAsync(
        ReportesVentasAcumuladoresProductosRequest request,
        CancellationToken ct = default)
    {
        var idReporte = ResolveReporte(request);

        await using var conn = GetConnection(await BaseDeCentroAsync(request.IdCentro, ct));
        await conn.OpenAsync(ct);

        var legacyParams = await BuildLegacyParamsAsync(conn, idReporte, request, ct);
        var parametroId = await GuardarParametrosAsync(conn, idReporte, legacyParams, ct);
        return await ConsultarAcumuladoresProductosPorParametrosAsync(conn, parametroId, request, ct);
    }

    public async Task<ReportesVentasPreviewResponse> ConsultarAcumuladoresProductosPorParametrosAsync(
        int parametrosLegacy,
        CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var legacy = await ConsultarParametrosAsync(conn, parametrosLegacy, ct);
        return await ConsultarAcumuladoresProductosPorParametrosAsync(conn, parametrosLegacy, legacy.Request, ct);
    }

    public async Task<ReportesVentasFileResponse> GenerarAcumuladoresProductosExcelPorParametrosAsync(
        int parametrosLegacy,
        CancellationToken ct = default)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        var legacy = await ConsultarParametrosAsync(conn, parametrosLegacy, ct);
        if (ResolvePresentacion(legacy.Request.Salida) != 3)
            throw new InvalidOperationException("El psp no corresponde a una salida Excel.");

        var idReporte = legacy.IDReporte;
        var config = await ConsultarConfiguracionAsync(conn, idReporte, ct);
        /* El renglon de parametros se vuelve a leer porque hay dos reportes cuyo
           procedimiento recibe DOS argumentos y el segundo vive ahi. Ver
           ArgumentosDelProcedimiento. */
        var renglon = await ConsultarParametrosRowAsync(conn, parametrosLegacy, ct);
        var data = await ExecuteLegacySpAsync(conn, config.StoredProcedure, ArgumentosDelProcedimiento(idReporte, parametrosLegacy, renglon), ct);
        var source = data.Tables.Count > 0 ? data.Tables[0] : new DataTable();
        var errorParams = source.Columns.Contains("ErrorParams") && source.Rows.Count > 0
            ? Convert.ToString(source.Rows[0]["ErrorParams"]) ?? "Error de parametros legacy."
            : "";
        if (!string.IsNullOrWhiteSpace(errorParams))
            throw new InvalidOperationException(errorParams);

        var excelTable = BuildLegacyExcelTable(idReporte, source);
        return LegacyPlainXlsxReportRenderer.Render(excelTable, config.NombreReporte, legacy.Request);
    }

    public async Task<ReportesVentasPreviewResponse> ConsultarReporteVentasPorParametrosAsync(
        int parametrosLegacy,
        int idCentro = 0,
        CancellationToken ct = default)
    {
        var reporte = await ArmarReportePorParametrosAsync(parametrosLegacy, idCentro, ct);

        /*
          Y aqui se le pone de donde salio. Va en UN solo lugar y no en las doce
          ramas de abajo: si se sellara en cada una, la que se agregue manana
          saldria sin marca y nadie lo notaria hasta que dos PDF identicos
          trajeran numeros distintos.
        */
        var centro = idCentro > 0 ? await _centros.BuscarAsync(idCentro, ct) : null;
        if (centro is not null)
        {
            reporte.NombreReporte = SelloDeCentro.Nombre(reporte.NombreReporte, centro.Nombre);
            reporte.Html = SelloDeCentro.Html(reporte.Html, centro.Nombre);
        }
        return reporte;
    }

    private async Task<ReportesVentasPreviewResponse> ArmarReportePorParametrosAsync(
        int parametrosLegacy,
        int idCentro,
        CancellationToken ct)
    {
        /* Los parametros se guardaron en la base del centro, asi que hay que
           leerlos de ahi: el reporte entero vive en una sola base. */
        await using var conn = GetConnection(await BaseDeCentroAsync(idCentro, ct));
        await conn.OpenAsync(ct);

        var row = await ConsultarParametrosRowAsync(conn, parametrosLegacy, ct);
        var idReporte = ReadInt(row, "IDReporte");
        if (IsAcumuladoresProductosReporte(idReporte))
        {
            var legacy = BuildAcumuladoresProductosStoredParams(row);
            return await ConsultarAcumuladoresProductosPorParametrosAsync(conn, parametrosLegacy, legacy.Request, ct);
        }
        if (IsRemisionesReporte(idReporte))
        {
            var request = BuildRemisionesStoredRequest(row);
            return await ConsultarReportePorParametrosAsync(conn, parametrosLegacy, idReporte, request, ct);
        }
        if (IsFoliosReporte(idReporte))
        {
            var request = BuildRemisionesStoredRequest(row);
            return await ConsultarReportePorParametrosAsync(conn, parametrosLegacy, idReporte, request, ct);
        }
        if (IsFacturasReporte(idReporte))
        {
            var request = BuildRemisionesStoredRequest(row);
            return await ConsultarReportePorParametrosAsync(conn, parametrosLegacy, idReporte, request, ct);
        }
        if (IsConcentradosReporte(idReporte))
        {
            var request = BuildConcentradosStoredRequest(row);
            return await ConsultarReportePorParametrosAsync(conn, parametrosLegacy, idReporte, request, ct);
        }
        if (IsCobranzaReporte(idReporte))
        {
            var request = BuildCobranzaStoredRequest(row);
            return await ConsultarReportePorParametrosAsync(conn, parametrosLegacy, idReporte, request, ct);
        }
        if (IsCobroReporte(idReporte))
        {
            return await ConsultarReportePorParametrosAsync(conn, parametrosLegacy, idReporte, BuildCobroStoredRequest(), ct);
        }
        if (IsConcentradoDocumentoReporte(idReporte))
        {
            return await ConsultarReportePorParametrosAsync(conn, parametrosLegacy, idReporte, BuildConcentradoStoredRequest(), ct);
        }
        if (IsGenericVentasReporte(idReporte))
        {
            var request = BuildGenericVentasStoredRequest(row);
            return await ConsultarReportePorParametrosAsync(conn, parametrosLegacy, idReporte, request, ct);
        }

        throw new InvalidOperationException("El psp no corresponde a un reporte soportado.");
    }

    public async Task<ReportesVentasFileResponse> GenerarReporteVentasExcelPorParametrosAsync(
        int parametrosLegacy,
        int idCentro = 0,
        CancellationToken ct = default)
    {
        await using var conn = GetConnection(await BaseDeCentroAsync(idCentro, ct));
        await conn.OpenAsync(ct);

        var row = await ConsultarParametrosRowAsync(conn, parametrosLegacy, ct);
        var idReporte = ReadInt(row, "IDReporte");
        var salida = ResolveSalida(ReadString(row, "Param10"));
        if (IsAcumuladoresProductosReporte(idReporte))
            salida = ResolveSalida(ReadString(row, "Param12"));
        if (IsConcentradosReporte(idReporte))
            salida = ResolveSalida(ReadString(row, "Param5"));
        /*
          La familia de cobranza lee la presentacion en Param10 menos el 1170,
          que solo tiene seis parametros y la lleva en Param6. Sin este renglon
          el Excel de "Rutas de Agentes" devolvia un PDF en silencio; el porque
          esta en ResolveCobranzaSalida.
        */
        if (IsCobranzaReporte(idReporte))
            salida = ResolveCobranzaSalida(idReporte, row);
        if (IsGenericVentasReporte(idReporte))
            salida = ResolveGenericVentasSalida(idReporte, row);

        if (salida != "excel")
            throw new InvalidOperationException("El psp no corresponde a una salida Excel.");

        if (!IsAcumuladoresProductosReporte(idReporte) && !IsRemisionesReporte(idReporte) && !IsFoliosReporte(idReporte) && !IsFacturasReporte(idReporte) && !IsConcentradosReporte(idReporte) && !IsCobranzaReporte(idReporte) && !IsGenericVentasReporte(idReporte))
            throw new InvalidOperationException("El psp no corresponde a un reporte soportado.");

        var request = BuildStoredExcelRequest(idReporte, row);
        var config = await ConsultarConfiguracionAsync(conn, idReporte, ct);
        var data = await ExecuteLegacySpAsync(conn, config.StoredProcedure, ArgumentosDelProcedimiento(idReporte, parametrosLegacy, row), ct);
        var source = data.Tables.Count > 0 ? data.Tables[0] : new DataTable();
        var errorParams = source.Columns.Contains("ErrorParams") && source.Rows.Count > 0
            ? Convert.ToString(source.Rows[0]["ErrorParams"]) ?? "Error de parametros legacy."
            : "";
        if (!string.IsNullOrWhiteSpace(errorParams))
            throw new InvalidOperationException(errorParams);

        var excelTable = BuildLegacyExcelTable(idReporte, source);
        return LegacyPlainXlsxReportRenderer.Render(excelTable, config.NombreReporte, request);
    }

    private static ReportesVentasAcumuladoresProductosRequest BuildStoredExcelRequest(int idReporte, DataRow row)
    {
        if (IsAcumuladoresProductosReporte(idReporte))
            return BuildAcumuladoresProductosStoredParams(row).Request;
        if (IsRemisionesReporte(idReporte) || IsFoliosReporte(idReporte) || IsFacturasReporte(idReporte))
            return BuildRemisionesStoredRequest(row);
        if (IsConcentradosReporte(idReporte))
            return BuildConcentradosStoredRequest(row);
        if (IsCobranzaReporte(idReporte))
            return BuildCobranzaStoredRequest(row);
        if (IsGenericVentasReporte(idReporte))
            return BuildGenericVentasStoredRequest(row);

        return new ReportesVentasAcumuladoresProductosRequest();
    }

    private static async Task<ReportesVentasPreviewResponse> ConsultarAcumuladoresProductosPorParametrosAsync(
        SqlConnection conn,
        int parametroId,
        ReportesVentasAcumuladoresProductosRequest request,
        CancellationToken ct)
    {
        var idReporte = ResolveReporte(request);
        return await ConsultarReportePorParametrosAsync(conn, parametroId, idReporte, request, ct);
    }

    private static async Task<ReportesVentasPreviewResponse> ConsultarReportePorParametrosAsync(
        SqlConnection conn,
        int parametroId,
        int idReporte,
        ReportesVentasAcumuladoresProductosRequest request,
        CancellationToken ct)
    {
        var config = await ConsultarConfiguracionAsync(conn, idReporte, ct);
        /* Idem: el segundo argumento de los dos reportes de recibos vive en el
           renglon de parametros. Ver ArgumentosDelProcedimiento. */
        var renglon = await ConsultarParametrosRowAsync(conn, parametroId, ct);
        var data = await ExecuteLegacySpAsync(conn, config.StoredProcedure, ArgumentosDelProcedimiento(idReporte, parametroId, renglon), ct);

        var table = data.Tables.Count > 0 ? data.Tables[0] : new DataTable();
        var errorParams = table.Columns.Contains("ErrorParams") && table.Rows.Count > 0
            ? Convert.ToString(table.Rows[0]["ErrorParams"]) ?? "Error de parametros legacy."
            : "";

        var reportHtml = "";
        var template = new LegacyTemplate();
        if (ResolvePresentacion(request.Salida) == 0)
        {
            var constants = await ConsultarConstantesAsync(conn, ct);
            var logo = ResolveLogoForHtml(constants.Logo);
            var templateReportId = config.IDReporteMaster > 0 ? config.IDReporteMaster : idReporte;
            var templates = await ConsultarTemplatesAsync(conn, templateReportId, ct);
            template = BuildTemplateInfo(templates);
            if (!string.IsNullOrWhiteSpace(errorParams))
            {
                reportHtml = GenerateSinInformacionHtml(logo, templates, config.NombreReporte, request, errorParams);
            }
            /*
              LA HOJA DE COBRO SE ARMA APARTE, Y SE DESVIA AQUI Y NO ANTES.

              Este es el punto donde ya estan en la mano las tres cosas que el
              armador necesita y que cuesta conseguir: el DataSet con las tres
              tablas (ExecuteLegacySpAsync ya recorrio el `spV2` de tres
              procedimientos y nombro cada tabla con su SP), las once
              plantillas `cobro_*` y los dos logos —el de la cabecera y el de
              la marca de agua—. Armarlo mas arriba obligaria a volver a
              pedirlo todo.

              Va ANTES del `3columnas` y del formato normal porque su `aspx`
              esta vacio en la base y caeria en el formato normal, que buscaria
              una plantilla `cuerpo` que este reporte no tiene.
            */
            else if (idReporte == ReporteCobro)
            {
                reportHtml = CobroHtmlBuilder.Construir(
                    logo,
                    constants.LogoWatermark,
                    config.NombreReporte,
                    data,
                    templates);
            }
            /*
              EL CONCENTRADO TAMBIEN SE ARMA APARTE, Y AQUI POR LA MISMA RAZON
              QUE LA HOJA DE COBRO: en este punto ya estan el DataSet con las
              tres tablas -ExecuteLegacySpAsync recorrio los tres `sp_n_` del
              `spV2` y nombro cada tabla con el suyo- y las seis plantillas
              `concentrado_*`. Va antes del `3columnas` y del formato normal
              porque su `aspx` esta VACIO en la base y caeria en el formato
              normal, que buscaria un `cuerpo_n` que este reporte no tiene.
            */
            else if (idReporte == ReporteConcentrado)
            {
                reportHtml = ConcentradoHtmlBuilder.Construir(
                    logo,
                    config.NombreReporte,
                    data,
                    templates);

                /*
                  SIN CONCENTRADO NO SE ENTREGA UN PAPEL EN BLANCO.

                  Legacy aqui tampoco imprime nada: su GroupBy().CopyToDataTable()
                  lanza "La fuente no contiene DataRows" y el catch de
                  ReporteConcentradoService devuelve el texto de la excepcion. O
                  sea que en Mac31 sale un mensaje, no una hoja vacia. Se dice
                  en castellano en vez de copiar el texto de la excepcion: el
                  unico caso en que pasa es un IDConcentrado que no existe.
                */
                if (string.IsNullOrWhiteSpace(reportHtml))
                    throw new InvalidOperationException("No se encontro el concentrado que se pidio.");
            }
            /*
              LOS DOS REPORTES DE ASISTENCIAS POR DIA (1025 y 1024) Y EL RECIBO
              DE NOMINA (1020) SE ARMAN APARTE.

              Los tres tienen `Aspx` que no es "3columnas" y caerian en el
              formato normal, que pinta UN renglon por fila de un SOLO
              procedimiento. Estos tres necesitan mas de uno:

              · 1025/1024 cruzan los recibos con las siete columnas de dias de
                `sp_n_ConsultaEmpleadosPeriodoAsistencia` y los encabezados de
                `sp_n_ConsultaPeriodosTipoSueldoDias`. Sin ese cruce el papel
                salia con "#asistenciaDia1#" escrito donde va la hora de
                entrada, que es lo que tenia a estos dos reportes sin dar de
                alta.
              · 1020 no tiene renglon en `dbo.Reportes` y su papel son recibos
                individuales con salto de pagina, no un listado.

              Se desvia AQUI, igual que la hoja de cobro y el concentrado, porque
              es el punto donde ya estan las plantillas, el logo y las
              constantes (que dicen si esta empresa es Tauro o Zaragoza).
            */
            /*
              EL DOCUMENTO DE UNA COMPRA (1015) SE ARMA APARTE, Y AQUI POR LAS
              MISMAS DOS RAZONES: en este punto ya estan el DataSet con las
              tres tablas -ExecuteLegacySpAsync recorrio los tres
              procedimientos de su columna `SP`, que es donde los declara- y
              las cuatro plantillas 1015. Y va antes del formato normal porque
              su `Aspx` es "rptCompraDetalle", que no es "3columnas" y caeria
              alli a buscar un `cuerpo_n` que este reporte no tiene: sus piezas
              se llaman `cuerpo`, `detalle`, `usados_credito_encabezado` y
              `usados_credito_detalle`.
            */
            else if (idReporte == ReporteComprasDetalle)
            {
                reportHtml = CompraHtmlBuilder.Construir(
                    logo,
                    config.NombreReporte,
                    data,
                    templates);
            }
            else if (idReporte is ReporteNominaAsistencias or ReporteNominaAsistenciasCorto)
            {
                var periodoDelRenglon = ReadInt(renglon, "Param3");
                var recibosNomina = await ConsultarRecibosAsync(
                    conn, SpDeRecibos(EsTauro(constants)), parametroId, 0, ct);

                /* El periodo gemelo de comision es el siguiente id, y legacy lo
                   lee del propio recibo, no del renglon de parametros
                   (ReporteAsistenciaService.cs:168). Si no vino ni un recibo se
                   cae al periodo del renglon para no perder los encabezados. */
                var periodoNomina = recibosNomina.Rows.Count > 0
                    ? ReadInt(recibosNomina.Rows[0], "IDPeriodoTipoSueldo")
                    : periodoDelRenglon;

                var recibosComision = await ConsultarRecibosAsync(
                    conn, SpDeRecibos(EsTauro(constants)), parametroId, periodoNomina + 1, ct);

                reportHtml = NominaAsistenciasHtmlBuilder.Construir(
                    logo,
                    config.NombreReporte,
                    recibosNomina,
                    recibosComision,
                    await ConsultarAsistenciasDelPeriodoAsync(conn, periodoNomina, ct),
                    await ConsultarDiasDelPeriodoAsync(conn, periodoNomina, ct),
                    templates,
                    template.MaxLineas,
                    /*
                      EL DINERO SOLO VIAJA AL 1025.

                      Los dos reportes piden los MISMOS recibos (con netoPagado
                      dentro), pero solo el 1025 tiene columna SUELDO y solo el
                      1025 pide `empleados.sueldo.ver` (ver ReportesCatalogo). Al
                      1024 se le entrega la tabla con las dos columnas de dinero
                      en null, asi que no puede imprimir un sueldo ni aunque le
                      descomenten la celda en `Template_Html`.
                    */
                    incluirImportes: idReporte == ReporteNominaAsistencias);
            }
            else if (idReporte == ReporteNominaRecibos)
            {
                var esTauro = EsTauro(constants);
                var sp = SpDeRecibos(esTauro);

                var recibosNomina = await ConsultarRecibosAsync(conn, sp, parametroId, 0, ct);
                var periodoNomina = recibosNomina.Rows.Count > 0
                    ? ReadInt(recibosNomina.Rows[0], "IDPeriodoTipoSueldo")
                    : ReadInt(renglon, "Param3");

                reportHtml = ReciboNominaHtmlBuilder.Construir(
                    logo,
                    templates,
                    await ConsultarPeriodoAsync(conn, periodoNomina, ct),
                    recibosNomina,
                    await ConsultarPeriodoAsync(conn, periodoNomina + 1, ct),
                    await ConsultarRecibosAsync(conn, sp, parametroId, periodoNomina + 1, ct),
                    esTauro);
            }
            else if (idReporte == ReporteListasPreciosZaragoza)
            {
                reportHtml = GenerateListasPreciosZaragozaHtml(logo, config.NombreReporte, data, templates, request);
            }
            else if (string.Equals(config.Aspx, "3columnas", StringComparison.OrdinalIgnoreCase))
            {
                reportHtml = GenerateFormatoColumnas3Html(logo, config.NombreReporte, data, templates, template, request);
            }
            else
            {
                reportHtml = GenerateFormatoNormalHtml(logo, constants.LogoWatermark, config.NombreReporte, data, templates, template, request);
            }

            // Ultimo filtro antes del PDF: un <img> que no se puede cargar hace
            // que wkhtmltopdf no entregue NADA. Ver SanearImagenesNoCargables.
            reportHtml = SanearImagenesNoCargables(reportHtml);
        }

        if (!string.IsNullOrWhiteSpace(errorParams) && string.IsNullOrWhiteSpace(reportHtml))
            throw new InvalidOperationException(errorParams);

        /*
          EL RECIBO SE IMPRIME COMO LO IMPRIME MACREPORTES, NO COMO DICE SU
          PLANTILLA.

          Las cuatro filas 1020 de `Template_Html` dicen Landscape y margenes
          3/8/8/15, y MacReportes las IGNORA: fija Portrait y 4/6/2/8 en el
          controlador (EmpleadosTipoSueldoRecibosController.cs:71-74). El papel
          que la gente conoce es el del controlador, asi que se copia ese.
        */
        if (idReporte == ReporteNominaRecibos)
        {
            template.Orientacion = "Portrait";
            template.MarginTop = 4;
            template.MarginBottom = 6;
            template.MarginLeft = 2;
            template.MarginRight = 8;
        }

        return new ReportesVentasPreviewResponse
        {
            IDReporte = idReporte,
            NombreReporte = config.NombreReporte,
            StoredProcedure = config.StoredProcedure,
            ParametrosLegacy = parametroId.ToString(),
            Html = reportHtml,
            Orientacion = template.Orientacion,
            Ancho = template.Ancho,
            Alto = template.Alto,
            MarginTop = template.MarginTop,
            MarginBottom = template.MarginBottom,
            MarginLeft = template.MarginLeft,
            MarginRight = template.MarginRight,
            Columns = BuildColumns(table),
            Rows = BuildRows(table),
            Total = table.Rows.Count
        };
    }

    /*
      DE QUE BASE SALEN LOS DATOS DE ESTE REPORTE.

      Sin centro, la de siempre. Con centro, la suya: mismo servidor, mismo
      usuario, solo cambia el catalogo (ver BaseDelCentro). El sistema es
      identico en la matriz y en cada centro; lo unico que cambia es a quien se
      le pregunta.

      `baseDelCentro` NUNCA viene del navegador. El front manda un IDCentro y
      quien resuelve el nombre es CentrosServicioRepository, contra la tabla, en
      el servidor.

      Solo lo usan los REPORTES. Los demas modulos siguen con su base y no se
      enteran de que esto existe.
    */
    private SqlConnection GetConnection(string? baseDelCentro = null)
    {
        var cs = _configuration.GetConnectionString("Main")
            ?? _configuration.GetConnectionString("Mac3")
            ?? _configuration.GetConnectionString("Local")
            ?? _configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException("ConnectionString (Main/Mac3/Local/Default) no encontrada.");

        var connector = new Mac3SqlServerConnector(BaseDelCentro.Apuntar(cs, baseDelCentro));
        return connector.GetConnection;
    }
}
