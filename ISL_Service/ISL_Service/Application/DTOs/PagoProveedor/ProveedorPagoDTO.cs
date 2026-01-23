using System;

namespace ISL_Service.Application.DTOs.ProveedoresPagos
{
    public class ProveedorPagoDTO
    {
        public int IDProveedorPago { get; set; }
        public int IDProveedor { get; set; }

        /// <summary>
        /// 1 = Activo, 2 = Cancelado
        /// </summary>
        public int IDStatus { get; set; }

        public string Facturas { get; set; }
        public decimal? TotalFactura { get; set; }
        public DateTime FechaPago { get; set; }

        public string Observaciones { get; set; }
        public string MotivoCancelacion { get; set; }

        // ===== Auditoría Alta =====
        public int IDUsuario { get; set; }
        public DateTime Fecha { get; set; }
        public string Equipo { get; set; }

        // ===== Auditoría Actualización =====
        public int? IDUsuarioModificacion { get; set; }
        public DateTime? FechaModificacion { get; set; }
        public string EquipoModificacion { get; set; }

        // ===== Auditoría Cancelación =====
        public int? IDUsuarioCancelacion { get; set; }
        public DateTime? FechaCancelacion { get; set; }
        public string EquipoCancelacion { get; set; }

        /// <summary>
        /// Campo derivado desde SQL
        /// 0 = No cancelado
        /// 1 = Cancelado
        /// </summary>
        public int Cancelado { get; set; }
    }
}
