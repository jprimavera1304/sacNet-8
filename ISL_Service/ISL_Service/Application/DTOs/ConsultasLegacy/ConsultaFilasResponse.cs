namespace ISL_Service.Application.DTOs.ConsultasLegacy;

/*
  LAS CONSULTAS DE LEGACY DEVUELVEN RENGLONES CRUDOS, NO UN DTO TIPADO

  Esta es la forma de salida de las 40 consultas de ventas que se migraron de
  MacServicios2 (cobros, cobranzas, garantias, usados, descuentos, folios...).
  Es la MISMA que ya usaba `VentasConsultaRowsResponse` para las remisiones, y
  se repite aqui a proposito en vez de reusar aquella clase: aquella vive en el
  modulo de la pantalla "Consultar ventas" y arrastra sus propios campos; esto
  es el contrato comun de todas las consultas nuevas.

  POR QUE UN DICCIONARIO Y NO UNA CLASE POR CONSULTA

  Porque los `sp_n_Consultar*` arman su SELECT con SQL dinamico y porque Legacy
  mete cada tabla en un DTO tipado con `DataTableToList`, que empareja por
  nombre EXACTO de propiedad. Lo que el procedimiento devuelve y el DTO no
  declara, Legacy lo TIRA; y lo que el DTO declara y el procedimiento no
  devuelve sale en 0/null aunque no sea un dato. Comprobado en
  `Utils/Funciones.cs:101-131` de MacServicios2.

  Dos ejemplos reales de lo que se perdia:
    * `ConsultaVentasPedidosDTO` escribe `IDDomilicio` (sin la c) y Legacy
      entrega 0 en todos los renglones.
    * `ConsultaCobrosDetalleDTO` esta VACIO: no declara ni una propiedad, asi
      que Legacy ya pasaba el DataTable crudo por `tablaLst`.

  Pasar los renglones tal como los da el procedimiento es mas fiel, no menos:
  el dia que alguien agregue una columna al `sp_n_`, el web la ve sin recompilar
  y sin que nadie tenga que acordarse de un DTO.
*/
public class ConsultaFilasResponse
{
    public List<Dictionary<string, object?>> Rows { get; set; } = new();

    /// Cuantos renglones trae `Rows`. Lo pide la pantalla para el contador, y
    /// asi no tiene que contar un arreglo que ya puede venir recortado.
    public int Total { get; set; }
}
