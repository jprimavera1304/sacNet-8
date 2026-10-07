namespace ISL_Service.Application.DTOs.Catalogos;

/// <summary>
/// Item del catálogo [Catalogo Repartidores]. Para dropdown en Almacén de Cascos.
/// </summary>
public class RepartidorItemDto
{
    public int IdRepartidor { get; set; }
    public string Repartidor { get; set; } = string.Empty;
    /*
      LAS TRES COLUMNAS QUE LE FALTABAN PARA PODER ADMINISTRARLO.

      La pantalla CONSULTA DE REPARTIDORES de Mac31 tiene tres columnas: NOMBRE
      DE REPARTIDOR, NUM. DE ZONA y ESTATUS. De las tres, este DTO solo traia el
      nombre, porque nacio para llenar un desplegable. Agregarlas no afecta a
      quien ya lo consume: un cliente que lee idRepartidor y repartidor ignora
      lo que no conoce.
    */
    public int IdStatus { get; set; }
    public int IdZona { get; set; }
    public string Zona { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;

}
