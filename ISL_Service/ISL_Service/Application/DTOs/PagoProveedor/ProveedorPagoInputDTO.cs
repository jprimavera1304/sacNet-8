using System.ComponentModel.DataAnnotations;

namespace ISL_Service.Application.DTOs.ProveedoresPagos
{
    public class ProveedorPagoInputDTO
    {
        [Required]
        public string token { get; set; }

        [Required]
        public string correoElectronicoToken { get; set; }

        // ===== Consultar =====
        public int IDProveedorPago { get; set; }
        public int IDPersona { get; set; }
        public int IDStatus { get; set; }              // filtro opcional (0 = todos)
        public string FechaInicial { get; set; }
        public string FechaFinal { get; set; }
        public int IncluirCancelados { get; set; }     // 0/1

        // ===== Insertar / Actualizar =====
        public string Facturas { get; set; }
        public decimal? TotalFactura { get; set; }
        public string FechaPago { get; set; }
        public string Observaciones { get; set; }

        // NO requerido para insertar por Personas
        //public int IDProveedor { get; set; }

        // ===== Cancelar =====
        public string MotivoCancelacion { get; set; }

        // Auditoría (alta)
        public int IDUsuario { get; set; }
        public string Equipo { get; set; }

        // Auditoría (actualización)
        public int IDUsuarioModificacion { get; set; }
        public string EquipoModificacion { get; set; }

        // Auditoría (cancelación)
        public int IDUsuarioCancelacion { get; set; }
        public string EquipoCancelacion { get; set; }

        public ProveedorPagoInputDTO()
        {
            token = "";
            correoElectronicoToken = "";

            FechaInicial = "";
            FechaFinal = "";

            Facturas = "";
            FechaPago = "";
            Observaciones = "";

            MotivoCancelacion = "";

            Equipo = "";
            EquipoModificacion = "";
            EquipoCancelacion = "";
        }
    }
}
