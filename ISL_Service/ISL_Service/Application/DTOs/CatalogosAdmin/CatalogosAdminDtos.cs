namespace ISL_Service.Application.DTOs.CatalogosAdmin;

/*
  LAS ESCRITURAS DE LOS CATALOGOS DE MAC31.

  La ola 1 migro las LECTURAS de estos catalogos (GET api/catalogos/...). Esto
  es la otra mitad: las altas, las modificaciones y las bajas. Va en su propio
  modulo, como lo dejo anotado la ola 1: "si alguna pantalla del web necesita
  escribir en uno de estos catalogos, se crea su modulo con su permiso .crear".

  TRES COSAS QUE SE DECIDIERON AQUI Y VALEN PARA LOS 21 CATALOGOS
  --------------------------------------------------------------
  1. LA BAJA NO BORRA. Los sp_n_Actualizar* ponen IDStatus = 2 y nada mas. Eso
     se respeta: no hay DELETE fisico en ningun catalogo.

  2. LA BAJA DE LEGACY BORRA LOS DATOS DEL RENGLON, Y ESO NO SE HEREDA.
     El DELETE de MacServicios2 llamaba al MISMO updateAsync con el cuerpo que
     le mandaran, y su controlador solo exigia el id. Como el UPDATE del SP pisa
     TODAS las columnas, un DELETE que solo trajera el id dejaba el renglon con
     Nombre = '' y los numeros en cero: la baja logica se convertia en un
     borrado de datos con el id intacto. Aqui la baja es
     POST {id}/inhabilitar sin cuerpo: el backend LEE los valores actuales del
     renglon y se los devuelve al SP junto con IDStatus = 2. Lo que cambia es el
     estatus y nada mas.

  3. NO HAY BAJA EN CIUDADES, MUNICIPIOS NI COLONIAS. Sus tres tablas no tienen
     IDStatus y sus sp_n_Actualizar* tampoco reciben el parametro, asi que el
     DELETE de legacy sobre ellas no daba de baja nada: dejaba el renglon con el
     nombre vacio. No se expone. Si una colonia no debe usarse, se corrige el
     renglon, no se vacia.

  El saneo del apostrofo tambien cambia de sitio: legacy hacia
  Replace("'", "") en cada service — O'BRIEN quedaba OBRIEN para siempre, y no
  por seguridad sino porque los sp_n_Consulta* concatenan el filtro dentro de un
  EXEC(@Sql). En una ESCRITURA el parametro no se concatena: va como
  SqlParameter al INSERT/UPDATE. Asi que aqui el apostrofo se guarda tal cual y
  O'BRIEN se llama O'BRIEN. Lo unico que se recorta es el espacio de los
  extremos.
*/

/// <summary>El par result/mensaje de legacy, ya traducido a algo que el front entiende.</summary>
public class CatalogoEscrituraResponse
{
    /// <summary>true = legacy contesto 1. false = legacy rechazo con una regla de negocio.</summary>
    public bool Guardo { get; set; }

    /// <summary>El texto literal de legacy ("EL AGENTE YA ESTA REGISTRADO"). Vacio en exito.</summary>
    public string Mensaje { get; set; } = string.Empty;

    /// <summary>El id del renglon cuando la operacion lo conoce; 0 cuando el SP no lo devuelve.</summary>
    public int Id { get; set; }
}

// ---------------------------------------------------------------------------
// Catalogos de un solo nombre
// ---------------------------------------------------------------------------

/// <summary>Almacenes, bancos, grupos de categoria, unidades de medida, tipos de gasto, tipos de domicilio.</summary>
public class CatalogoNombreRequest
{
    public string Nombre { get; set; } = string.Empty;
}

/// <summary>Lo mismo, mas el estatus que el UPDATE de legacy exige.</summary>
public class CatalogoNombreUpdateRequest
{
    public string Nombre { get; set; } = string.Empty;

    /// <summary>1 = Activo, 2 = Cancelado.</summary>
    public int IdStatus { get; set; } = 1;
}

// ---------------------------------------------------------------------------
// Agentes
// ---------------------------------------------------------------------------

public class CrearAgenteRequest
{
    /// <summary>El numero visible del agente (columna Agente). No es el IDAgente.</summary>
    public int NumeroAgente { get; set; }
    public string NombreAgente { get; set; } = string.Empty;
}

public class ActualizarAgenteRequest
{
    public int NumeroAgente { get; set; }
    public string NombreAgente { get; set; } = string.Empty;
    public int IdStatus { get; set; } = 1;
}

// ---------------------------------------------------------------------------
// Condiciones de credito
// ---------------------------------------------------------------------------

public class CrearCondicionCreditoRequest
{
    public string CondicionCredito { get; set; } = string.Empty;
    public int DiasCredito { get; set; }
}

public class ActualizarCondicionCreditoRequest
{
    public string CondicionCredito { get; set; } = string.Empty;
    public int DiasCredito { get; set; }
    public int IdStatus { get; set; } = 1;
}

// ---------------------------------------------------------------------------
// Documentos
// ---------------------------------------------------------------------------

public class CrearDocumentoRequest
{
    public string Documento { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Obligatorio { get; set; }
}

public class ActualizarDocumentoRequest
{
    public string Documento { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public bool Obligatorio { get; set; }
    public int IdStatus { get; set; } = 1;
}

// ---------------------------------------------------------------------------
// Galleros
// ---------------------------------------------------------------------------

/// <summary>
/// El gallero trae SALDO, que es dinero. Por eso su permiso es aparte.
/// Aviso que importa: sp_n_ActualizarGallero tiene el "Saldo = @Saldo"
/// COMENTADO, asi que la modificacion NO cambia el saldo aunque reciba el
/// parametro. El saldo solo se fija en el alta.
/// </summary>
public class CrearGalleroRequest
{
    public string Gallero { get; set; } = string.Empty;
    public decimal Saldo { get; set; }
}

public class ActualizarGalleroRequest
{
    public string Gallero { get; set; } = string.Empty;
    public int IdStatus { get; set; } = 1;
}

// ---------------------------------------------------------------------------
// Repartidores
// ---------------------------------------------------------------------------

public class CrearRepartidorRequest
{
    public int IdZona { get; set; }
    public string Repartidor { get; set; } = string.Empty;
}

public class ActualizarRepartidorRequest
{
    public int IdZona { get; set; }
    public string Repartidor { get; set; } = string.Empty;
    public int IdStatus { get; set; } = 1;
}

// ---------------------------------------------------------------------------
// Tipos de recordatorio
// ---------------------------------------------------------------------------

public class CrearTipoRecordatorioRequest
{
    public string TipoRecordatorio { get; set; } = string.Empty;
    public bool EsUrgente { get; set; }
    public bool EsImportante { get; set; }
}

public class ActualizarTipoRecordatorioRequest
{
    public string TipoRecordatorio { get; set; } = string.Empty;
    public bool EsUrgente { get; set; }
    public bool EsImportante { get; set; }
    public int IdStatus { get; set; } = 1;
}

// ---------------------------------------------------------------------------
// Dias festivos
// ---------------------------------------------------------------------------

/// <summary>
/// El dia festivo ES la fecha: la tabla guarda una fila por dia.
///
/// Cuidado con la modificacion: sp_n_ActualizarDiaFestivo, si la fecha que
/// recibe NO existe todavia en DiasFestivos, da de BAJA el renglon del id y
/// mete uno NUEVO con la fecha nueva. O sea que "modificar el dia" no cambia el
/// renglon, lo reemplaza. Esta documentado aqui porque el id que devuelva una
/// consulta posterior no sera el mismo que se mando.
/// </summary>
public class CrearDiaFestivoRequest
{
    public DateTime DiaFestivo { get; set; }
}

public class ActualizarDiaFestivoRequest
{
    public DateTime DiaFestivo { get; set; }
    public int IdStatus { get; set; } = 1;
}

public class DiasFestivosFiltro
{
    public int IdDiaFestivo { get; set; }
    public int IdStatus { get; set; }
    public DateTime? DiaFestivoInicial { get; set; }
    public DateTime? DiaFestivoFinal { get; set; }
}

// ---------------------------------------------------------------------------
// Territorial
// ---------------------------------------------------------------------------

public class CrearCiudadRequest
{
    /// <summary>Clave del estado, texto de dos posiciones ("01".."32").</summary>
    public string IdEstado { get; set; } = string.Empty;
    public string Ciudad { get; set; } = string.Empty;
}

public class ActualizarCiudadRequest
{
    public string IdEstado { get; set; } = string.Empty;
    public string Ciudad { get; set; } = string.Empty;
}

public class CrearMunicipioRequest
{
    public string IdEstado { get; set; } = string.Empty;
    public string Municipio { get; set; } = string.Empty;
}

public class ActualizarMunicipioRequest
{
    public string IdEstado { get; set; } = string.Empty;
    public string Municipio { get; set; } = string.Empty;
}

public class CrearColoniaRequest
{
    public string IdEstado { get; set; } = string.Empty;
    public string IdCiudad { get; set; } = string.Empty;
    public string IdMunicipio { get; set; } = string.Empty;
    public string Colonia { get; set; } = string.Empty;
    public string CodigoPostal { get; set; } = string.Empty;
}

public class ActualizarColoniaRequest
{
    public string IdEstado { get; set; } = string.Empty;
    public string IdCiudad { get; set; } = string.Empty;
    public string IdMunicipio { get; set; } = string.Empty;
    public string Colonia { get; set; } = string.Empty;
    public string CodigoPostal { get; set; } = string.Empty;
}

/// <summary>
/// Unidad territorial. Los dos SP (sp_n_InsertarCatUnidadTerritorial y
/// sp_n_ActualizarCatUnidadTerritorial) NO estan en esta base: el modulo es de
/// otra instalacion. Se implementa igual y la llamada falla con el mensaje de
/// SQL Server, exactamente como falla Legacy.
/// </summary>
public class UnidadTerritorialRequest
{
    public string Clave { get; set; } = string.Empty;
    public string UnidadTerritorial { get; set; } = string.Empty;
    public int IdStatus { get; set; } = 1;
}

/// <summary>
/// Centro de servicio. Igual que la unidad territorial: sus dos SP no estan en
/// esta base.
/// </summary>
public class CentroServicioRequest
{
    public int Numero { get; set; }
    public string CentroServicio { get; set; } = string.Empty;
    public string? BaseDatos { get; set; }
    public int IdStatus { get; set; } = 1;
}

// ---------------------------------------------------------------------------
// Categorias y marcas
// ---------------------------------------------------------------------------

/// <summary>
/// Categoria de producto de Mac31 (tabla Categorias). No confundir con el
/// modulo Categorias del web, que es de otra aplicacion y usa Guid.
/// </summary>
public class CrearCategoriaMacRequest
{
    public int IdGrupoCategoria { get; set; }
    public string Categoria { get; set; } = string.Empty;
}

public class ActualizarCategoriaMacRequest
{
    public int IdGrupoCategoria { get; set; }
    public string Categoria { get; set; } = string.Empty;
    public int IdStatus { get; set; } = 1;
}

public class CrearMarcaRequest
{
    public string Marca { get; set; } = string.Empty;
    public string MarcaCorto { get; set; } = string.Empty;

    /// <summary>En que columna del concentrado sale la marca. 0 = en ninguna.</summary>
    public int ColumnaConcentrado { get; set; }
    public int ColumnaInventario { get; set; }
    public int ColumnaTemplate { get; set; }
    public int OrdenListaPrecios { get; set; }
}

public class ActualizarMarcaRequest
{
    public string Marca { get; set; } = string.Empty;
    public string MarcaCorto { get; set; } = string.Empty;
    public int ColumnaConcentrado { get; set; }
    public int ColumnaInventario { get; set; }
    public int ColumnaTemplate { get; set; }
    public int OrdenListaPrecios { get; set; }
    public int IdStatus { get; set; } = 1;
}

/// <summary>
/// Relacion categoria - marca (tabla [Categorias Marcas]).
///
/// El SP es un interruptor: con Marcado = 1 inserta el par si no existe, con
/// Marcado = 0 lo borra. Es el UNICO borrado fisico de este modulo y lo es
/// porque la tabla no tiene mas que las dos llaves: no hay nada que dar de baja.
/// </summary>
public class RelacionCategoriaMarcaRequest
{
    public int IdCategoria { get; set; }
    public int IdMarca { get; set; }
    public bool Marcado { get; set; }
}

// ---------------------------------------------------------------------------
// Autos
// ---------------------------------------------------------------------------

/// <summary>
/// Catalogo de autos. Las cuatro fechas (verificacion y vigencia del seguro)
/// viajan como fecha de verdad y el backend las pasa a texto 'MM-dd-yyyy', que
/// es lo que espera legacy. Mandar la cadena ya formateada desde el navegador
/// seria confiar en la cultura del cliente: un 03-04 leido al reves cambia el
/// mes por el dia en la vigencia de una poliza.
/// </summary>
public class CrearAutoRequest
{
    public string Nombre { get; set; } = string.Empty;
    public int Modelo { get; set; }
    public string Placas { get; set; } = string.Empty;
    public bool Verifica { get; set; }
    public DateTime? VerificacionFecha1erSemestre { get; set; }
    public DateTime? VerificacionFecha2doSemestre { get; set; }
    public bool TieneSeguro { get; set; }
    public DateTime? SeguroFechaInicio { get; set; }
    public DateTime? SeguroFechaTermino { get; set; }
    public string? NumeroSerie { get; set; }
    public string? NumeroMotor { get; set; }
    public string? NombreTarjetaCirculacion { get; set; }
    public string? NombreAseguradora { get; set; }
    public string? NumeroPoliza { get; set; }
    public decimal MontoPoliza { get; set; }
    public decimal KmPorLitroMinimo { get; set; }
    public decimal KmPorLitroMaximo { get; set; }
    public decimal TotalTanqueLleno { get; set; }
}

public class ActualizarAutoRequest : CrearAutoRequest
{
    public int IdStatus { get; set; } = 1;
}

// ---------------------------------------------------------------------------
// Empresas de Mac31 y sus series
// ---------------------------------------------------------------------------

/// <summary>
/// Empresa de Mac31 (tabla Empresas): la razon social que factura, no la
/// EmpresaWeb del modelo de permisos. Son dos cosas distintas con el mismo
/// nombre y aqui se habla de la de Mac31.
///
/// El SP arma [Calle y Numero] internamente pegando calle + exterior +
/// interior, asi que ese campo no se manda ni se puede mandar.
/// </summary>
public class CrearEmpresaMacRequest
{
    public int IdColonia { get; set; }
    public string NombreCorto { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string? Descripcion1 { get; set; }
    public string? Descripcion2 { get; set; }
    public string RFC { get; set; } = string.Empty;
    public string? Calle { get; set; }
    public string? NumeroExterior { get; set; }
    public string? NumeroInterior { get; set; }
    public string? Telefono1 { get; set; }
    public string? Telefono2 { get; set; }
    public string? Email { get; set; }
    public string? IdFacturama { get; set; }
    public int IdBanco { get; set; }
    public string? NoCuenta { get; set; }
    public string? Clabe { get; set; }
    public int IdFERegimenFiscal { get; set; }

    /// <summary>Solo en el alta: sp_n_ActualizarEmpresa no lo recibe.</summary>
    public bool EsCentroServicio { get; set; }
}

public class ActualizarEmpresaMacRequest
{
    public int IdColonia { get; set; }
    public string NombreCorto { get; set; } = string.Empty;
    public string RazonSocial { get; set; } = string.Empty;
    public string? Descripcion1 { get; set; }
    public string? Descripcion2 { get; set; }
    public string RFC { get; set; } = string.Empty;
    public string? Calle { get; set; }
    public string? NumeroExterior { get; set; }
    public string? NumeroInterior { get; set; }
    public string? Telefono1 { get; set; }
    public string? Telefono2 { get; set; }
    public string? Email { get; set; }
    public string? IdFacturama { get; set; }
    public int IdBanco { get; set; }
    public string? NoCuenta { get; set; }
    public string? Clabe { get; set; }
    public int IdFERegimenFiscal { get; set; }
    public int IdStatus { get; set; } = 1;
}

/// <summary>Serie de folios de una empresa (tabla EmpresasSeries).</summary>
public class CrearEmpresaSerieRequest
{
    public int IdEmpresa { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }

    /// <summary>El folio con el que arranca la serie.</summary>
    public int Folio { get; set; }
}

public class ActualizarEmpresaSerieRequest
{
    public int IdEmpresa { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public int Folio { get; set; }
    public int IdStatus { get; set; } = 1;
}
