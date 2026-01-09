namespace ISL_Service.Application.DTOs.Recaudacion
{
    public class RecaudacionDTO
    {
        public int IDRecaudacionOriginal { get; set; }
        public int IDRecaudacionReverso { get; set; }
        public int IDRecaudacion { get; set; }

        public int IDEconomicoAuxiliar { get; set; }
        public string EconomicoAuxiliar { get; set; }

        public int IDStatus { get; set; }
        public string Estatus { get; set; }

        public int Folio { get; set; }
        public string FolioFtm { get; set; }

        public decimal Importe { get; set; }
        public decimal ImporteTotal { get; set; }
        public int NumCuentas { get; set; }

        public int IDUsuario { get; set; }
        public string Usuario { get; set; }
        public DateTime Fecha { get; set; }
        public string FechaFtm { get; set; }
        public string FechaCortoFtm { get; set; }
        public string FechaMMDDYYYY { get; set; }
        public string Equipo { get; set; }

        public int IDPermisionario { get; set; }
        public string NumPermisionario { get; set; }
        public string NombrePermisionario { get; set; }

        public int IDMotivoFianza { get; set; }
        public int NumMotivoFianza { get; set; }
        public string MotivoFianza { get; set; }

        public int IDEconomico { get; set; }
        public int NumEconomico { get; set; }
        public string NumEconomicoFtm { get; set; }
        public string NombreEconomico { get; set; }

        public int IDCuenta { get; set; }
        public int IDConcepto { get; set; }
        public string NumConcepto { get; set; }
        public string NombreConcepto { get; set; }
        public string NumCuenta { get; set; }
        public string NumCuentaFtm { get; set; }
        public string NombreCuenta { get; set; }
        public string NombreCuentaDetalle { get; set; }
        public decimal ImporteCuenta { get; set; }
        public int ImporteCuentaFijo { get; set; }
        public string DebeHaber { get; set; }
        public string Enlace { get; set; }
        public string SRojo { get; set; }

        public int Auxiliar { get; set; }
        public string AuxiliarFtm { get; set; }

        public int IDOperador { get; set; }
        public int NumNomina { get; set; }
        public string NumNominaFtm { get; set; }
        public string OperadorNombre { get; set; }
        public string OperadorApellidoPaterno { get; set; }
        public string OperadorApellidoMaterno { get; set; }
        public string OperadorNombreCompleto { get; set; }

        public int IDCaja { get; set; }
        public int NumCaja { get; set; }
        public string DireccionCaja { get; set; }

        public string DV1 { get; set; }
        public string DV2 { get; set; }

        public int EsPagoExtraordinario { get; set; }

        public int IDUsuarioCancelacion { get; set; }
        public DateTime FechaCancelacion { get; set; }
        public string FechaCancelacionFtm { get; set; }
        public string EquipCancelacion { get; set; }
        public string Cancelada { get; set; }

        public string html { get; set; }

        public decimal ImportePagado { get; set; }
        public string ImportePagadoFtm { get; set; }

        public string Observaciones { get; set; }

        public string TipoPago { get; set; }
        public DateTime FechaTarjAdmFueraHorario { get; set; }
        public string FechaTarjAdmFueraHorarioFtm { get; set; }
        public string FolioLLanta { get; set; }
        public decimal MontoTransferencia { get; set; }

        public int EsPagoLlanta { get; set; }
        public int EsReverso { get; set; }

        public int IDRecaudacionCorte { get; set; }
        public DateTime FechaCorte { get; set; }
        public string FechaCorteFtm { get; set; }
        public string FechaCorteMMDDYYYY { get; set; }
        public DateTime FechaCorteSiguienteDia { get; set; }
        public string FechaCorteSiguienteDiaMMDDYYYY { get; set; }
        public DateTime FechaActual { get; set; }
        public string FechaActualFtm { get; set; }
    }
}