using System.ComponentModel.DataAnnotations;

namespace ISL_Service.Application.DTOs.Recaudacion
{
    public class RecaudacionInputDTO
    {

        [Required]
        public string token { get; set; }

        [Required]
        public string correoElectronicoToken { get; set; }


        public int IDRecaudacionOriginal { get; set; }
        public int IDRecaudacionReverso { get; set; }
        public int IDRecaudacion { get; set; }
        public string IDsRecaudacion { get; set; }
        public List<int> IDsRecaudacionLst { get; set; }

        public int IDStatus { get; set; }

        public int IDCaja { get; set; }
        public string IDsCaja { get; set; }
        public List<int> IDsCajaLst { get; set; }
        public int NumCajaInicial { get; set; }
        public int NumCajaFinal { get; set; }

        public int IDConcepto { get; set; }
        public string IDsConcepto { get; set; }
        public List<int> IDsConceptoLst { get; set; }
        public int NumConceptoInicial { get; set; }
        public int NumConceptoFinal { get; set; }

        public int IDCuenta { get; set; }
        public string IDsCuenta { get; set; }
        public List<int> IDsCuentaLst { get; set; }
        public int NumCuentaInicial { get; set; }
        public int NumCuentaFinal { get; set; }

        public int IDEconomico { get; set; }
        public string IDsEconomico { get; set; }
        public List<int> IDsEconomicoLst { get; set; }
        public int NumEconomicoInicial { get; set; }
        public int NumEconomicoFinal { get; set; }

        public int IDPermisionario { get; set; }
        public string IDsPermisionario { get; set; }
        public List<int> IDsPermisionarioLst { get; set; }
        public int NumPermisionarioInicial { get; set; }
        public int NumPermisionarioFinal { get; set; }

        public int IDOperador { get; set; }
        public string IDsOperador { get; set; }
        public List<int> IDsOperadorLst { get; set; }
        public int NumNominaInicial { get; set; }
        public int NumNominaFinal { get; set; }

        public int FolioInicial { get; set; }
        public int FolioFinal { get; set; }

        public string FechaInicial { get; set; }
        public string FechaFinal { get; set; }

        public int NumCuentas { get; set; }
        public decimal ImporteCuentas { get; set; }
        public decimal ImporteTotal { get; set; }

        public List<decimal> ImportesCuentaLst { get; set; }
        public string ImportesCuenta { get; set; }

        public int IDMotivoFianza { get; set; }

        public string FechaTarjAdmFueraHorario { get; set; }
        public string Observaciones { get; set; }

        public string FolioLlanta { get; set; }
        public string TipoPago { get; set; }
        public decimal MontoTransferencia { get; set; }

        public int IDParametros { get; set; }


        public int IDRecaudacionCorte { get; set; }
        public string FechaCorte { get; set; }
        public int ExisteCorteDiaAnterior { get; set; }
        public int UltimoDiaCorte { get; set; }

        public int Dv1 { get; set; }

        public int IDUsuario { get; set; }
        public string Equipo { get; set; }


        public RecaudacionInputDTO()
        {
            this.IDsRecaudacion = "";
            this.IDsRecaudacionLst = new List<int>();

            this.IDsCajaLst = new List<int>();
            this.IDsCaja = "";

            this.IDsConceptoLst = new List<int>();
            this.IDsConcepto = "";

            this.IDsCuentaLst = new List<int>();
            this.IDsCuenta = "";

            this.IDsEconomicoLst = new List<int>();
            this.IDsEconomico = "";

            this.IDsPermisionarioLst = new List<int>();
            this.IDsPermisionario = "";

            this.IDsOperadorLst = new List<int>();
            this.IDsOperador = "";

            this.ImportesCuentaLst = new List<decimal>();
            this.ImportesCuenta = "";

            this.FechaInicial = "";
            this.FechaFinal = "";

            this.FechaTarjAdmFueraHorario = "";
            this.Observaciones = "";

            this.FolioLlanta = "";
            this.TipoPago = "";

            this.FechaCorte = "";
        }

    }
}