namespace ISL_Service.Application.Empresa;

/// <summary>
/// LA FILA DE dbo.Constantes, CRUDA, MAS LA FECHA DE PAGOS QUE LE TOCA AL USUARIO.
///
/// Es lo unico que el repositorio saca de la base. La traduccion a capacidades la
/// hace <see cref="MapaDeCapacidades"/>, que es codigo puro y se prueba sin base
/// ni DI (mismo criterio que Application/Reportes/ReporteCobranzaPorEmpresa.cs).
///
/// POR QUE NO SE LEE CON sp_n_ConsultaConstantes
/// ---------------------------------------------
/// Porque ese procedimiento NO es una consulta: cuando la fecha de operacion esta
/// atrasada borra y rehace AlmacenProductoHist y adelanta Constantes.FechaOperacion,
/// o sea hace el CIERRE DEL DIA (ver migracion/CUIDADO-consultas-que-escriben.md).
/// Pedir las capacidades al iniciar sesion dispararia el cierre del dia en cada
/// login, lo que es exactamente lo que no debe pasar. Aqui se lee con un
/// SELECT TOP 1 sobre dbo.Constantes y, cuando aplica, otro sobre
/// [Usuarios Fechas]: dos lecturas y ninguna escritura.
/// </summary>
public sealed class ConstantesDeEmpresa
{
    public string Servidor { get; set; } = string.Empty;
    public string BaseDatos { get; set; } = string.Empty;

    public string Funcionalidad { get; set; } = string.Empty;
    public string EsquemaPago { get; set; } = string.Empty;
    public int EsCentroServicio { get; set; }
    public int EsAlcaldiaMC { get; set; }
    public int EsLuro { get; set; }
    public int EsAviacion { get; set; }
    public int EsAviacionServer { get; set; }
    public int AviacionIdCaja { get; set; }
    public int InventarioNegativo { get; set; }

    public decimal Iva { get; set; }
    public int NoDiasAtrasados { get; set; }
    public int NoDiasAdelantados { get; set; }
    public int NoDiasDevDeposito { get; set; }
    public int DiasFolioConcentrado { get; set; }
    public int DiasFolioCobranza { get; set; }
    public int NumDiasPedidosRechazados { get; set; }
    public decimal DiferenciaPagoVsTransCheq { get; set; }
    public decimal MaximaDiferenciaUsados { get; set; }
    public decimal FacturaPorcentajeDescMax { get; set; }
    public decimal PorDescMax { get; set; }
    public int MaxTiempoInactividadMinutos { get; set; }
    public int ChecadorHoraTarde { get; set; }
    public int HorasLaborales { get; set; }
    public string HoraEntrada { get; set; } = string.Empty;
    public string SalidaTarde { get; set; } = string.Empty;
    public int DefaultNumCopias { get; set; }
    public int MaxNumCopias { get; set; }
    public int IdAgenteAutorizaGarantias { get; set; }
    public int IdGrupoCategoriaServicios { get; set; }
    public int IdEmpresaCS { get; set; }
    public int IdEmpresaZara { get; set; }
    public int IdEmpresaLuro { get; set; }
    public int IdClienteCS { get; set; }
    public string IdsClienteZaragoza { get; set; } = string.Empty;
    public string NombreCentroServicio { get; set; } = string.Empty;
    public decimal ZaraSaldoLiquidarDineroMonto { get; set; }
    public decimal ZaraSaldoLiquidarCascosMonto { get; set; }
    public decimal PrecioCascoKilo { get; set; }

    /// <summary>
    /// Los clientes de servicio a domicilio de Zaragoza: el de matriz y los de los
    /// once centros de servicio. Van juntos porque Legacy los compara uno por uno
    /// en el mismo bloque (Ventas.cs:1972-1997) y para el front son una sola lista
    /// de excepciones.
    /// </summary>
    public List<int> IdsClienteServicioDomicilio { get; set; } = new();

    public DateOnly FechaOperacion { get; set; }

    /// <summary>
    /// La fecha de pagos de la EMPRESA (Constantes.FechaOperacionPagos). En Tauro
    /// es la que vale; en Zaragoza es solo el valor por omision y la de verdad es
    /// <see cref="FechaOperacionPagosDelUsuario"/>.
    /// </summary>
    public DateOnly FechaOperacionPagos { get; set; }

    /// <summary>
    /// La fila de [Usuarios Fechas] de este usuario, si existe. Solo se consulta
    /// cuando la funcionalidad es ZARA, porque solo ahi la escribe
    /// sp_n_ActualizarFechaOperacion.
    /// </summary>
    public DateOnly? FechaOperacionPagosDelUsuario { get; set; }

    public string UsuarioFechaOperacion { get; set; } = string.Empty;
    public DateTime? FechaCambioFechaOperacion { get; set; }
    public string EquipoFechaOperacion { get; set; } = string.Empty;

    public string UsuarioFechaOperacionPagos { get; set; } = string.Empty;
    public DateTime? FechaCambioFechaOperacionPagos { get; set; }
    public string EquipoFechaOperacionPagos { get; set; } = string.Empty;
}
