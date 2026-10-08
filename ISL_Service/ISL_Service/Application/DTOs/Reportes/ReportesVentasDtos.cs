using System.Text.Json.Serialization;

namespace ISL_Service.Application.DTOs.Reportes;

public class ReportesVentasAcumuladoresProductosRequest
{
    /*
      DE QUE CENTRO SE SACA ESTE REPORTE. 0 = la matriz, como siempre.

      Es un Id, no un nombre de base: el servidor lo resuelve contra
      dbo.CentrosServicio. Dejar que el cliente mande el nombre seria dejarle
      elegir a que base entrar.
    */
    public int IdCentro { get; set; }

    public DateTime FechaInicial { get; set; }
    public DateTime FechaFinal { get; set; }
    public string Categoria { get; set; } = "acumuladores";
    public int IDGrupoCategoria { get; set; } = 1;
    public string TipoReporte { get; set; } = "empresa";
    public string Documento { get; set; } = "ventas";
    public bool SoloServiciosDomicilio { get; set; }
    public List<int> IDEmpresas { get; set; } = new();
    public List<int> IDAlmacenes { get; set; } = new();
    public List<int> IDSubcategorias { get; set; } = new();
    public List<int> IDMarcas { get; set; } = new();
    public List<int> IDAgentes { get; set; } = new();
    public List<int> IDClientes { get; set; } = new();
    public string Salida { get; set; } = "pantalla";

    [JsonIgnore]
    public int LegacyIDUsuario { get; set; }

    [JsonIgnore]
    public string LegacyNombreEquipo { get; set; } = "WEB";
}

public class ReportesVentasRemisionesRequest : ReportesVentasAcumuladoresProductosRequest
{
    public string EstatusFolio { get; set; } = "todos";
    public List<int> IDUsuarios { get; set; } = new();
}

public class ReportesVentasFoliosRequest : ReportesVentasRemisionesRequest
{
    public bool FoliosDineroCascos { get; set; }
}

public class ReportesVentasFacturasRequest : ReportesVentasRemisionesRequest
{
    public string TipoFactura { get; set; } = "factura";
}

public class ReportesVentasConcentradosRequest : ReportesVentasAcumuladoresProductosRequest
{
    public List<int> IDRepartidores { get; set; } = new();
}

public class ReportesVentasCobranzaRequest : ReportesVentasAcumuladoresProductosRequest
{
    public string CobranzaStatus { get; set; } = "pagadas";
}

public class ReportesVentasLegacyRequest : ReportesVentasAcumuladoresProductosRequest
{
    public string ReporteKey { get; set; } = "";
    public int IDReporte { get; set; }
    public int IDProveedor { get; set; }
    public string EstatusFolio { get; set; } = "todos";
    public string TipoFactura { get; set; } = "factura";
    public string TipoFechaCompras { get; set; } = "captura";
    public string EstatusComprasFacturas { get; set; } = "vigentes";
    public bool InventarioConCostos { get; set; }
    public bool InventarioHistorico { get; set; }
    public bool InventarioGenerarPedido { get; set; }
    public string Formato { get; set; } = "detallado";
    public string FormatoMotobaterias { get; set; } = "cliente";
    public string TipoPago { get; set; } = "todos";
    public List<int> IDTiposPago { get; set; } = new();
    public bool PagosPorDia { get; set; }
    public bool PagosExcedentes { get; set; }
    public string TransferenciaEstatus { get; set; } = "todas";
    public string DineroCascos { get; set; } = "dinero";
    public bool FiltrarFechas { get; set; }
    public decimal Gastos { get; set; }
    public int IDTipoGasto { get; set; }
    public string Comentarios { get; set; } = "";
    public bool Redondear { get; set; }
    public bool IncluirDescuentos { get; set; } = true;
    public List<int> IDDiasSemana { get; set; } = new();
    public List<int> IDUsuarios { get; set; } = new();
    public List<int> IDRepartidores { get; set; } = new();
    public List<int> IDAutos { get; set; } = new();
    public List<int> IDCentros { get; set; } = new();
    public List<int> IDStatusGarantias { get; set; } = new();

    /*
      LOS CAMPOS QUE LE FALTABAN A LA RUTA GENERICA.

      Hasta ahora seis claves del catalogo (acumuladores_y_productos, folios,
      remisiones, facturas, concentrados, cobranza) pasaban la validacion del
      catalogo y morian abajo con 500 "Reporte de ventas no soportado", porque
      solo se podian pedir por su endpoint propio. Con estos campos la ruta
      generica arma los mismos parametros que esos endpoints y ya no hay una
      clave del catalogo que la ruta no acepte.
    */
    public bool FoliosDineroCascos { get; set; }
    public string CobranzaStatus { get; set; } = "pagadas";

    /*
      QUE ESTADO DE CUENTA SE QUIERE. Vacio = dinero y cascos (1370),
      "dinero" = 1375, "cascos" = 1380.

      Es un campo aparte de DineroCascos a proposito: alla "dinero" es el valor
      por omision porque el reporte de Cobros SIEMPRE es de uno de los dos, y
      aqui el valor por omision tiene que ser "los dos", como el combo de Mac31.
      Compartirlos haria que pedir un estado de cuenta sin decir nada devolviera
      el de dinero.
    */
    public string FormatoEstadoCuenta { get; set; } = "";

    /*
      CUAL DE LAS CUATRO LISTAS PERSONALIZADAS DE TAURO.

      Es el combo cboTipoLista de Mac31: 1 = todas las marcas,
      2 = todas menos Tauro, 3 = FP/CALE/DYNAMO/LTH/AMERICA/CRONOS/GOHNER/XTREME,
      4 = FP/AMERICA/LTH. Viaja ademas como Param17 al SP, igual que en Mac31.
    */
    public int TipoLista { get; set; } = 1;

    /// <summary>Clientes Acum Moto Lub: el acumulado mensual (118 / 119).</summary>
    public bool VentasMensual { get; set; }

    /// <summary>Control Acumulado de Estimaciones (1250): la estimacion a imprimir.</summary>
    public int IDContratoEstimacion { get; set; }

    /// <summary>Control Acumulado de Estimaciones (1250): su contrato.</summary>
    public int IDContrato { get; set; }

    /// <summary>Registro de Garantias (1236): filtro de productos, vacio = todos.</summary>
    public List<int> IDProductos { get; set; } = new();

    /// <summary>
    /// Cambios de Vigencia (1535): el documento a imprimir. Es OBLIGATORIO y no
    /// tiene valor por omision util: el reporte imprime UN cambio de vigencia, no
    /// un listado, y con 0 el procedimiento devuelve cero renglones despues de
    /// haber intentado sellar la impresion del documento 0.
    /// </summary>
    public int IDCambioVigencia { get; set; }

    /// <summary>
    /// Liquidaciones de Descuento (1030): los folios SELECCIONADOS a imprimir.
    ///
    /// Es obligatorio y no hay "todos": el boton IMPRIMIR de
    /// ConsultarLiquidacionesDescuentos recorre la rejilla, junta los marcados y
    /// si no hay ninguno dice "SELECCIONE UN FOLIO" y no imprime
    /// (ConsultarLiquidacionesDescuentos.cs:468-484). El reporte es de lo
    /// seleccionado, no del resultado de la busqueda.
    /// </summary>
    /// <summary>
    /// 1003: el comprobante timbrado cuyo PDF se quiere (factura, nota de credito
    /// o complemento de pago). Es OBLIGATORIO: el reporte imprime UNO, no un
    /// listado, y con 0 el procedimiento devuelve cero renglones.
    /// </summary>
    public int IDVentaFactura { get; set; }

    /// <summary>
    /// Los reportes de nomina (1024, 1025, 1027, 1190): los empleados marcados.
    ///
    /// Es obligatorio y no hay "todos": el formulario de Mac31 recorre la rejilla,
    /// junta los marcados y si no hay ninguno dice "SELECCIONE UN EMPLEADO." y no
    /// imprime (ReporteEmpleadoAsistenciaRecibos.cs:328-340).
    /// </summary>
    public List<int> IDEmpleados { get; set; } = new();

    /// <summary>
    /// Los reportes de nomina: el periodo de tipo de sueldo elegido en la rejilla
    /// de arriba. Mac31 lo exige para ASISTENCIAS y para RECIBOS
    /// ("SELECCIONE UN PERIODO.", :299) y lo manda igual en los otros dos.
    /// </summary>
    public int IDPeriodoTipoSueldo { get; set; }

    public List<int> IDLiquidacionesDescuento { get; set; } = new();
}

public class ReportesVentasPreviewResponse
{
    public int IDReporte { get; set; }
    public string NombreReporte { get; set; } = "";
    public string StoredProcedure { get; set; } = "";
    public string ParametrosLegacy { get; set; } = "";
    public string Html { get; set; } = "";
    public string Orientacion { get; set; } = "Landscape";
    public int Ancho { get; set; }
    public int Alto { get; set; }
    public int MarginTop { get; set; }
    public int MarginBottom { get; set; }
    public int MarginLeft { get; set; }
    public int MarginRight { get; set; }
    public List<ReportesVentasColumnDto> Columns { get; set; } = new();
    public List<Dictionary<string, object?>> Rows { get; set; } = new();
    public int Total { get; set; }
}

public class ReportesVentasGenerateResponse
{
    public int IDReporte { get; set; }
    public string NombreReporte { get; set; } = "";
    public string StoredProcedure { get; set; } = "";
    public string ParametrosLegacy { get; set; } = "";
    public string Url { get; set; } = "";
    public string Salida { get; set; } = "pantalla";
}

public class ReportesVentasFileResponse
{
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "";
    public string FileName { get; set; } = "";
}

public class ReportesVentasColumnDto
{
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string Type { get; set; } = "text";
}

public class ReportesVentasCatalogosResponse
{
    public string FechaOperacion { get; set; } = "";
    public List<ReportesVentasCatalogoItem> Empresas { get; set; } = new();
    public List<ReportesVentasCatalogoItem> Almacenes { get; set; } = new();
    public List<ReportesVentasCatalogoItem> Agentes { get; set; } = new();
    public List<ReportesVentasCatalogoItem> Categorias { get; set; } = new();
    public List<ReportesVentasProductoItem> Subcategorias { get; set; } = new();
    public List<ReportesVentasProductoItem> Marcas { get; set; } = new();
    public List<ReportesVentasCatalogoItem> Documentos { get; set; } = new();
    public List<ReportesVentasCatalogoItem> StatusFolios { get; set; } = new();
    public List<ReportesVentasCatalogoItem> Usuarios { get; set; } = new();
    public List<ReportesVentasCatalogoItem> Repartidores { get; set; } = new();
    public List<ReportesVentasCatalogoItem> Proveedores { get; set; } = new();
    public List<ReportesVentasCatalogoItem> Autos { get; set; } = new();
    /// <summary>
    /// Las formas de pago del filtro del reporte de Pagos (gridTiposPago de
    /// Mac31, con EsPago = 1). Se publican aqui y no en
    /// /api/catalogos/tipos-pago porque ese endpoint exige catalogos.ver, y
    /// quien tiene reportes no tiene por que tenerlo: el filtro contestaria 403
    /// y el campo se quedaria vacio sin explicar nada.
    /// </summary>
    public List<ReportesVentasCatalogoItem> TiposPago { get; set; } = new();

    public List<ReportesVentasCatalogoItem> TiposGasto { get; set; } = new();
}

public class ReportesVentasCatalogoItem
{
    public int Id { get; set; }
    public string Nombre { get; set; } = "";
    public string? Clave { get; set; }
}

public class ReportesVentasClienteItem
{
    public int IDCliente { get; set; }
    public int Numero { get; set; }
    public string NombreCliente { get; set; } = "";
    public string Nombre { get; set; } = "";
}

public class ReportesVentasProductoItem
{
    public int IDGrupoCategoria { get; set; }
    public string GrupoCategoria { get; set; } = "";
    public int IDCategoria { get; set; }
    public string Categoria { get; set; } = "";
    public int IDMarca { get; set; }
    public string Marca { get; set; } = "";
}

/// <summary>
/// Que reportes puede ver quien pregunta. Lo consumen el menu del web y el de
/// la app; el mapeo reporte->permiso vive en ReportesCatalogo.
/// </summary>
public class ReportesPermisosResponse
{
    /// <summary>El maestro del modulo. Sin el no se llega ni a este endpoint.</summary>
    public string PermisoModulo { get; set; } = "";

    /// <summary>Los 53 reportes, en el orden del menu.</summary>
    public List<ReportesPermisoItem> Reportes { get; set; } = new();

    /// <summary>Solo las claves permitidas. Es lo unico que necesita un menu.</summary>
    public List<string> ClavesPermitidas { get; set; } = new();
}

public class ReportesPermisoItem
{
    public string Clave { get; set; } = "";
    public string Grupo { get; set; } = "";
    public string Etiqueta { get; set; } = "";

    /// <summary>null = ese reporte no tiene permiso propio todavia.</summary>
    public string? Permiso { get; set; }

    public bool Permitido { get; set; }
}
