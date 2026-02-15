using System;

namespace ISL_Service.Application.DTOs.ProveedoresPagos
{
    public class ProveedorPagoDTO
    {
        public int IDProveedorPago { get; set; }
        public int IDPersona { get; set; }

        /// <summary>
        /// 1 = Activo, 2 = Cancelado
        /// </summary>
        public int IDStatus { get; set; }

        public string Facturas { get; set; }
        public decimal? TotalFactura { get; set; }
        public DateTime FechaPago { get; set; }

        public string Observaciones { get; set; }
        public string MotivoCancelacion { get; set; }

        // ===== Auditoria Alta =====
        public int IDUsuario { get; set; }
        public DateTime Fecha { get; set; }
        public string Equipo { get; set; }

        // ===== Auditoria Actualizacion =====
        public int? IDUsuarioModificacion { get; set; }
        public DateTime? FechaModificacion { get; set; }
        public string EquipoModificacion { get; set; }

        // ===== Auditoria Cancelacion =====
        public int? IDUsuarioCancelacion { get; set; }
        public string UsuarioCancelacion { get; set; } = string.Empty;
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
