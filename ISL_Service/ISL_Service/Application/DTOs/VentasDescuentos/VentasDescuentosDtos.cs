namespace ISL_Service.Application.DTOs.VentasDescuentos;

/*
  DESCUENTOS: EL CATALOGO, SU DETALLE Y LOS AJUSTES DE UNA VENTA

  Tres cosas distintas que en legacy se parecen:

    * El CATALOGO de descuentos: una lista de esquemas con nombre.
    * El DETALLE: por cada esquema, que porcentaje aplica a que categoria y
      marca.
    * Los AJUSTES de una venta: un descuento que alguien pide sobre una remision
      ya emitida y que otro tiene que autorizar. Esto es dinero y lleva firma de
      quien autoriza.

  Tres rarezas del catalogo que conviene conocer antes de leer el codigo, porque
  explican por que los endpoints se ven mas pobres de lo que uno esperaria:

    1) sp_n_InsertarDescuentos NO RECIBE NINGUN PARAMETRO. En legacy la linea que
       mandaba @IDDescuento esta comentada. Crea el renglon en blanco y el nombre
       se pone despues.
    2) sp_n_ActualizarDescuentos recibe SOLO @IDDescuento: el texto del descuento
       que el controlador valida nunca llega a la base.
    3) Los tres procedimientos del catalogo —actualizar, eliminar y consultar—
       NO EXISTEN en Produccion_svr. Legacy falla igual al llamarlos.
*/

/// <summary>Crea un esquema de descuento.</summary>
public class DescuentoCrearRequest
{
    /// <summary>
    /// El nombre del esquema. Se acepta y se guarda en el request para que el
    /// contrato sea honesto, pero el procedimiento de legacy no lo recibe: crea
    /// el renglon en blanco. Queda anotado en la respuesta.
    /// </summary>
    public string? Descuento { get; set; }
}

/// <summary>Modifica un esquema de descuento.</summary>
public class DescuentoActualizarRequest
{
    public int IdDescuento { get; set; }
    public string? Descuento { get; set; }
}

/// <summary>Identifica un esquema de descuento: eliminar y consultar.</summary>
public class DescuentoIdRequest
{
    public int IdDescuento { get; set; }
}

/// <summary>
/// Simula un descuento con el usuario y el equipo del token.
///
/// Es una ESCRITURA, aunque se llame simular: sp_n_SimularDescuento hace
/// INSERT INTO Descuentos cuando no encuentra el renglon de simulacion de ese
/// usuario y ese equipo. Es su manera de tener un esquema temporal por puesto de
/// trabajo.
/// </summary>
public class DescuentoSimularRequest
{
    /// <summary>El texto del descuento a simular.</summary>
    public string? Descuento { get; set; }
}

/// <summary>Agrega un renglon al detalle de un esquema de descuento.</summary>
public class DescuentoDetalleCrearRequest
{
    public int IdDescuento { get; set; }
    public int IdCategoria { get; set; }
    public int IdMarca { get; set; }

    /// <summary>
    /// El porcentaje, como texto. El procedimiento lo declara varchar(50) y lo
    /// convierte el: se respeta el tipo para no cambiar como redondea.
    /// </summary>
    public string? Porcentaje { get; set; }

    /// <summary>1 para aplicarlo a todas las marcas de la categoria.</summary>
    public int AplicarTodos { get; set; }
}

/// <summary>Modifica un renglon del detalle.</summary>
public class DescuentoDetalleActualizarRequest : DescuentoDetalleCrearRequest
{
    public int IdDescuentoDetalle { get; set; }

    /// <summary>
    /// El grupo de categoria. Solo el procedimiento de ACTUALIZAR lo recibe; el
    /// de insertar no lo declara.
    /// </summary>
    public int IdGrupoCategoria { get; set; }
}

/// <summary>Elimina un renglon del detalle.</summary>
public class DescuentoDetalleEliminarRequest
{
    public int IdDescuentoDetalle { get; set; }
    public int IdDescuento { get; set; }
    public int IdCategoria { get; set; }
    public int IdMarca { get; set; }
}

/// <summary>
/// Solicita un descuento o ajuste sobre una remision ya emitida.
///
/// Los seis campos de descuentos viajan como cadenas delimitadas con virgulilla
/// porque el procedimiento los cruza por posicion con los renglones de la
/// remision. Van como listas aqui y se pegan en el repositorio.
/// </summary>
public class VentaDescuentoAjusteCrearRequest
{
    public int IdTipoLiquidacion { get; set; }
    public int IdVenta { get; set; }
    public int IdEmpresa { get; set; }
    public int Folio { get; set; }

    /// <summary>El descuento global que se pide, por renglon de la remision.</summary>
    public List<decimal> DescuentoGlobal { get; set; } = new();

    /// <summary>Los renglones de la remision a los que se aplica.</summary>
    public List<int> IdsVentaDetalle { get; set; } = new();

    /// <summary>Los tres descuentos en cascada que admite un renglon.</summary>
    public List<decimal> Descuento1 { get; set; } = new();
    public List<decimal> Descuento2 { get; set; } = new();
    public List<decimal> Descuento3 { get; set; } = new();

    /// <summary>El descuento adicional, por renglon.</summary>
    public List<decimal> DescuentoAdicional { get; set; } = new();

    /// <summary>La fecha de la solicitud. Obligatoria.</summary>
    public DateTime? Fecha { get; set; }

    public string? Observaciones { get; set; }

    /// <summary>El total que quedaria despues del ajuste.</summary>
    public decimal NuevoTotalPagar { get; set; }

    public DateTime? FechaLiquidacion { get; set; }

    /// <summary>El texto que se imprime en el documento, armado por la pantalla.</summary>
    public string? TextoDescuentos { get; set; }

    public string? TextoDescuentosPorLinea { get; set; }

    public decimal DescuentoAplicar { get; set; }

    /// <summary>La diferencia contra el total original.</summary>
    public decimal Diferencia { get; set; }
}

/// <summary>
/// Autoriza o rechaza un ajuste de descuento.
///
/// En legacy, el usuario que autoriza y el que rechaza viajan en el CUERPO
/// (IDUsuarioAutorizacion, IDUsuarioRechazo): cualquiera podia autorizar un
/// descuento a nombre de otro, y ese es el campo que despues se audita. Aqui el
/// cuerpo solo dice QUE se hace; quien lo hace sale del token.
/// </summary>
public class VentaDescuentoAjusteStatusRequest
{
    public int IdVentasDescuentosAjustes { get; set; }

    /// <summary>true autoriza, false rechaza.</summary>
    public bool Autorizar { get; set; }

    /// <summary>La fecha del movimiento. Obligatoria.</summary>
    public DateTime? Fecha { get; set; }

    /// <summary>Obligatorio cuando se rechaza.</summary>
    public string? MotivoRechazo { get; set; }
}

/// <summary>Simula el ajuste sin guardarlo.</summary>
public class VentaDescuentoAjusteSimularRequest
{
    public int IdVentasDescuentosAjustes { get; set; }
    public int IdVenta { get; set; }
    public int IdEmpresa { get; set; }
    public int Folio { get; set; }
    public List<decimal> DescuentoGlobal { get; set; } = new();
    public List<int> IdsVentaDetalle { get; set; } = new();
    public List<decimal> Descuento1 { get; set; } = new();
    public List<decimal> Descuento2 { get; set; } = new();
    public List<decimal> Descuento3 { get; set; } = new();
    public List<decimal> DescuentoAdicional { get; set; } = new();

    /// <summary>1 para que devuelva el total en vez del detalle.</summary>
    public int Totalizado { get; set; }
}

/// <summary>Filtros de la consulta de ajustes de descuento.</summary>
public class VentaDescuentoAjusteConsultaRequest
{
    public List<int> IdsVentasDescuentosAjustes { get; set; } = new();
    public List<int> IdsTipoLiquidacion { get; set; } = new();
    public List<int> IdsVenta { get; set; } = new();
    public List<int> IdsCliente { get; set; } = new();
    public List<int> IdsAgente { get; set; } = new();
    public int FolioInicial { get; set; }
    public int FolioFinal { get; set; }
    public DateTime? FechaInicial { get; set; }
    public DateTime? FechaFinal { get; set; }

    /// <summary>0 todos, 1 pendientes, y el resto segun tipifica legacy.</summary>
    public int Status { get; set; }
}

/*
  LOS DOS DE LECTURA, QUE VIENEN DE LA OLA 3.

  Dos agentes escribieron este archivo a la vez: uno las lecturas del
  detalle de descuentos y su bitacora, otro las escrituras del catalogo.
  No se pisan, son cosas distintas, asi que se conservan los dos juegos.
*/
/// <summary>
/// El renglonaje de un descuento: que porcentaje aplica a que
/// grupo-categoria / categoria / marca.
/// </summary>
/// <remarks>
/// Es el filtro de `sp_n_ConsultaDescuentosDetalle`. Los tres primeros son
/// listas porque la pantalla de Mac31 permite marcar varias casillas a la vez;
/// `IDVenta` sirve para traer el descuento tal como quedo aplicado en una
/// remision concreta.
/// </remarks>
public class DescuentoDetalleRequest
{
    public List<int> IDsMarca { get; set; } = new();
    public List<int> IDsCategoria { get; set; } = new();
    public List<int> IDsGrupoCategoria { get; set; } = new();

    /// Un renglon concreto del descuento.
    public int IDDescuentoDetalle { get; set; }

    /// El descuento (la cabecera).
    public int IDDescuento { get; set; }

    /// La remision, para ver el descuento como se aplico ahi.
    public int IDVenta { get; set; }
}

/// <summary>
/// La bitacora de cambios de un descuento: quien movio que porcentaje, cuando
/// y desde que equipo.
/// </summary>
public class DescuentoDetalleLogRequest
{
    public int IDDescuento { get; set; }
    public int IDCategoria { get; set; }
    public int IDMarca { get; set; }
}
