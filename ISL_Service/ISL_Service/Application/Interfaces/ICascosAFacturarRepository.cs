namespace ISL_Service.Application.Interfaces;

/*
  EL CATALOGO DE CASCOS QUE SE PUEDEN AGREGAR A UNA FACTURA

  En `VentasFacturacion` de Mac31 hay un recuadro aparte, "CASCOS A FACTURAR"
  (Designer:1999), que NO sale de la remision: sale del catalogo de tipos de
  usado con su precio, y la persona teclea cuantos cascos quiere facturar
  ademas de los productos.

  Como lo pide alla: `ConsultarVentaUsadosCredito()` llama al catalogo con
  **IDVenta = 0** (VentasFacturacion.cs:325) y despues pone TODAS las
  cantidades y los importes en cero (:338-342). O sea: no pregunta por los
  cascos de esa remision, pregunta por la lista de tipos y sus precios.

  POR QUE NO SE REUSA `POST api/ventas/usados/consultar`
  -----------------------------------------------------
  Tres razones, las tres medidas:

    1. ese endpoint EXIGE una remision (`idVenta <= 0` es 400) y aqui el
       catalogo se pide con 0;
    2. aplica las reglas de la pantalla de usados —se niega con "EL FOLIO NO
       TIENE USADOS." cuando no hay cargos—, que no tienen nada que ver con
       facturar;
    3. su DTO NO trae `ClaveSat`, y para facturar un casco esa clave es
       obligatoria: va dentro del CFDI como la clave del producto o servicio
       del catalogo del SAT. Es el unico dato que el form de Mac31 SI manda
       para los cascos (`ClavesSatUsadoLst`, VentasFacturacion.cs:1606).

  Medido en MacZ: `sp_n_VentasUsadosCredito @Accion = 2, @IDVenta = 0` devuelve
  `IDTipoUsado`, `TipoUsadoCredito`, `CostoVentaCreditoConIva`, `ClaveSat`
  (80151500 en los ocho tipos) y `Orden`.
*/
public interface ICascosAFacturarRepository
{
    Task<List<CascoAFacturar>> ConsultarCatalogoAsync(CancellationToken ct);
}

/// Un tipo de casco con lo que hace falta para meterlo en un CFDI.
public sealed class CascoAFacturar
{
    public int IdTipoUsado { get; set; }

    /// Como se llama el tipo ("G1", "MINI CHICO(1)"): es la DESCRIPCION del
    /// concepto en el comprobante, tal cual, sin traducir.
    public string Tipo { get; set; } = string.Empty;

    /// El precio CON IVA ya redondeado por la base. Es el que multiplica la
    /// cantidad, igual que en Mac31 (`CalculaTotalCascos`, :4042-4085).
    public decimal CostoConIva { get; set; }

    /// La clave del SAT del casco. Sin ella el concepto no se puede timbrar.
    public string ClaveSat { get; set; } = string.Empty;

    public int Orden { get; set; }
}
