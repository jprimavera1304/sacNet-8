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

        /*
          LOS SEIS CAMPOS DE AUDITORIA LOS PISA EL CONTROLADOR CON EL TOKEN.

          Siguen aqui porque son los parametros que el repositorio le pasa a los
          sp_n_, pero lo que mande el cliente se DESCARTA: ProveedoresPagosController
          los sobreescribe con IdUsuarioLegacy() y Equipo() antes de llamar al
          servicio.

          Antes se EXIGIAN del cuerpo ("IDUsuario es requerido."), asi que la
          firma de un pago a proveedor -quien lo dio de alta, quien lo modifico,
          quien lo cancelo- la elegia quien llamaba. Un pago a proveedor es
          dinero que sale; que su autoria sea falsificable no es un detalle.
        */
        // Auditoria (alta)
        public int IDUsuario { get; set; }
        public string Equipo { get; set; }

        // Auditoría (actualización)
        public int IDUsuarioModificacion { get; set; }
        public string EquipoModificacion { get; set; }

        // Auditoria (cancelacion)
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
