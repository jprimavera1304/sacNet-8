namespace ISL_Service.Application.DTOs.VentasUsados;

/*
  MODIFICACION DE USADOS  —  LO QUE VIAJA ENTRE EL WEB Y EL SERVICIO

  Es la pantalla "Modificación de Usados" de Mac31
  (Legacy/Mac31/Mac31/Forms/Ventas/VentasUsados.cs, titulo en el Designer
  linea 2146). Se llega a ella con btnUsados desde Consultar ventas
  (ConsultarVentas.cs:3529).

  QUE SE MODIFICA Y QUE NO
  Lo unico editable de toda la pantalla es la CANTIDAD de cada renglon de
  CREDITOS (los cascos que el cliente SI entrego). Todo lo demas —el costo, los
  cargos, el importe, los descuentos, los abonos— es de solo lectura porque en
  Mac31 tambien lo es: el grid de cargos y el de creditos se recorren poniendo
  `Visible = false` y `ReadOnly = true` en TODAS las columnas antes de encender
  a mano las que se ven (VentasUsados.cs:269-273 y 423-427), y el unico control
  de captura de la forma es txtCantidadUsadoCredito.

  POR QUE LOS IMPORTES NO VIAJAN DE REGRESO AL GUARDAR
  El cliente manda cantidades y nada mas. Los pesos los vuelve a sacar el
  procedimiento de legacy a partir del catalogo
  (sp_n_VentasUsadosCreditoAjuste: `@TotalCreditos = SUM(CostoVentaCreditoConIva
  * CantidadUsadoCredito)`), asi que un importe mandado desde el navegador seria
  un numero que nadie usa —o peor, que alguien podria usar. Los totales que
  devuelve la consulta son para PINTAR la pantalla, no para escribir.
*/

/* ------------------------------------------------------------------ */
/* Peticiones                                                          */
/* ------------------------------------------------------------------ */

public class VentasUsadosConsultarRequest
{
    public int IdVenta { get; set; }
}

public class VentasUsadosGuardarRequest
{
    public int IdVenta { get; set; }

    /*
      TODOS los renglones del grid de creditos, no solo los que cambiaron.
      Mac31 recorre el grid entero y arma las cadenas con TODOS los renglones
      (VentasUsados.cs:920-941), incluidos los que quedan en cero: el
      procedimiento necesita ver el cero para dar de baja ese tipo de usado.
      Mandar solo los cambiados haria que los demas se quedaran como estaban.
    */
    public List<VentasUsadosRenglonRequest> Renglones { get; set; } = new();
}

public class VentasUsadosRenglonRequest
{
    public int IdTipoUsado { get; set; }
    public int Cantidad { get; set; }
}

/* ------------------------------------------------------------------ */
/* Respuesta de la consulta: todo lo que pinta la pantalla             */
/* ------------------------------------------------------------------ */

public class VentasUsadosPantallaDto
{
    /* Cabecera: los mismos campos que VentasUsados.cs:166-199. */
    public int IdVenta { get; set; }
    public string Folio { get; set; } = string.Empty;
    public string Cliente { get; set; } = string.Empty;
    public string Empresa { get; set; } = string.Empty;
    public string Agente { get; set; } = string.Empty;

    /*
      Si la venta YA tiene fecha de pago, Mac31 esconde el boton de guardar, el
      de actualizar y la caja de cantidad (VentasUsados.cs:82-94): la pantalla
      queda de consulta. Aqui viaja la fecha para poder decirlo con letras, y
      PuedeGuardar para no obligar al front a repetir la regla.
    */
    public string FechaPago { get; set; } = string.Empty;
    public bool PuedeGuardar { get; set; }

    /*
      Constantes.MaximaDiferenciaUsados. Es el tope de tolerancia CON IVA que la
      forma enseña arriba a la derecha ("MÁXIMO DIFERENCIA DE USADOS CON IVA: $",
      Designer linea 1610) y contra el que compara antes de dejar guardar
      (VentasUsados.cs:643 y 717). Viaja porque es un dato de la empresa, no una
      constante del programa: cada base tiene el suyo.
    */
    public decimal MaximaDiferenciaUsados { get; set; }

    /// Constantes.IVA en tanto por uno (0.16), para recalcular en el navegador.
    public decimal PorcentajeIva { get; set; }

    public VentasUsadosTotalesVentaDto Totales { get; set; } = new();
    public VentasUsadosBloqueDto Cargos { get; set; } = new();
    public VentasUsadosBloqueDto Creditos { get; set; } = new();

    /// Los creditos dados de baja (IDStatus = 2): quien y cuando los cambio.
    public List<VentasUsadosAnteriorDto> Anteriores { get; set; } = new();
}

/*
  Los seis numeros del recuadro "TOTALES ACTUAL". Son tal cual los que Mac31
  pone en las etiquetas (VentasUsados.cs:177-191), sin recalcular nada aqui:
  si el web hiciera su propia cuenta y la base dijera otra cosa, ganaria la
  pantalla equivocada.
*/
public class VentasUsadosTotalesVentaDto
{
    public decimal Importe { get; set; }
    public decimal Cargos { get; set; }
    /// Creditos + IvaCreditos, que es como se enseña (VentasUsados.cs:181).
    public decimal Creditos { get; set; }
    /// TotalPagar + Cargos (VentasUsados.cs:183).
    public decimal TotalPagar { get; set; }
    public decimal Descuentos { get; set; }
    public decimal Abonos { get; set; }
    public decimal Saldo { get; set; }
}

public class VentasUsadosBloqueDto
{
    public List<VentasUsadosRenglonDto> Renglones { get; set; } = new();
    public int TotalCantidad { get; set; }
    public decimal TotalImporte { get; set; }
    public decimal TotalIva { get; set; }
    public decimal TotalTotal { get; set; }
}

public class VentasUsadosRenglonDto
{
    public int IdTipoUsado { get; set; }
    public string Tipo { get; set; } = string.Empty;
    /// Costo SIN iva. Solo informativo; los totales salen del costo con iva.
    public decimal Costo { get; set; }
    /*
      El costo CON iva redondeado a pesos enteros que calcula legacy. Es el
      numero con el que se multiplica la cantidad, tanto en el grid de Mac31
      (VentasUsados.cs:604) como dentro del procedimiento que guarda. Si el web
      multiplicara por Costo*(1+IVA) daria centavos de diferencia.
    */
    public decimal CostoConIva { get; set; }
    public int Cantidad { get; set; }
    public decimal Total { get; set; }
}

public class VentasUsadosAnteriorDto
{
    public string Fecha { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public int Cantidad { get; set; }
    public string Usuario { get; set; } = string.Empty;
}

/* ------------------------------------------------------------------ */
/* Respuesta al guardar                                                */
/* ------------------------------------------------------------------ */

public class VentasUsadosGuardarResultadoDto
{
    public bool Aplicado { get; set; }
    public string Mensaje { get; set; } = string.Empty;
    public int IdVenta { get; set; }
    public string Folio { get; set; } = string.Empty;
}
