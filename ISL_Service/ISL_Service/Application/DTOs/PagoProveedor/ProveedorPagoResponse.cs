using System.Collections.Generic;

namespace ISL_Service.Application.DTOs.ProveedoresPagos
{
    public class ProveedorPagoResponse
    {
        public List<ProveedorPagoDTO> pagos { get; set; }

        public ProveedorPagoResponse()
        {
            pagos = new List<ProveedorPagoDTO>();
        }

        public ProveedorPagoResponse(List<ProveedorPagoDTO> pagos)
        {
            this.pagos = pagos;
        }
    }
}
