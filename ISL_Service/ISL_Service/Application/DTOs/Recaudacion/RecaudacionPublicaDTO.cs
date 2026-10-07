#nullable disable
namespace ISL_Service.Application.DTOs.Recaudacion
{
    /// <summary>
    /// Lo UNICO que sale por el endpoint publico del QR.
    ///
    /// POR QUE EXISTE
    /// --------------
    /// <see cref="RecaudacionDTO"/> tiene mas de 60 campos: ids internos de la
    /// base, el plan de cuentas (NumCuenta / ImporteCuenta), observaciones,
    /// tipo de pago, monto de transferencia, los datos del corte y —lo peor—
    /// DV1 y DV2, que son los digitos verificadores con los que se arma el
    /// propio codigo QR. Devolver todo eso a quien solo escaneo un ticket
    /// impreso es regalar material para fabricar tickets.
    ///
    /// Aqui estan EXACTAMENTE los campos que pinta la pantalla publica
    /// (Web/QR_Web/docs/modules/custom/aviacion/qr/js/main.js), ni uno mas.
    /// Si manana la pantalla necesita otro dato, se agrega aqui a proposito,
    /// que es justo lo que no pasaba antes.
    /// </summary>
    public class RecaudacionPublicaDTO
    {
        /// <summary>2 = cancelada. La pantalla tacha el ticket con esto.</summary>
        public int IDStatus { get; set; }

        public string FolioFtm { get; set; }
        public DateTime Fecha { get; set; }
        public string FechaFtm { get; set; }

        public decimal Importe { get; set; }
        public decimal ImporteTotal { get; set; }

        /// <summary>El concepto que se cobro, tal como va en el ticket.</summary>
        public string NombreCuentaDetalle { get; set; }

        public string NombreEconomico { get; set; }
        public string NumEconomicoFtm { get; set; }
        public string NombrePermisionario { get; set; }

        /// <summary>Quien cobro. Va impreso en el ticket de papel.</summary>
        public string Usuario { get; set; }
        public string Equipo { get; set; }

        public int NumNomina { get; set; }
        public string NumNominaFtm { get; set; }
        public string OperadorNombreCompleto { get; set; }

        /// <summary>Cadena de legacy con fecha, hora, usuario y equipo de la cancelacion.</summary>
        public string Cancelada { get; set; }

        public static RecaudacionPublicaDTO Desde(RecaudacionDTO origen) => new()
        {
            IDStatus = origen.IDStatus,
            FolioFtm = origen.FolioFtm,
            Fecha = origen.Fecha,
            FechaFtm = origen.FechaFtm,
            Importe = origen.Importe,
            ImporteTotal = origen.ImporteTotal,
            NombreCuentaDetalle = origen.NombreCuentaDetalle,
            NombreEconomico = origen.NombreEconomico,
            NumEconomicoFtm = origen.NumEconomicoFtm,
            NombrePermisionario = origen.NombrePermisionario,
            Usuario = origen.Usuario,
            Equipo = origen.Equipo,
            NumNomina = origen.NumNomina,
            NumNominaFtm = origen.NumNominaFtm,
            OperadorNombreCompleto = origen.OperadorNombreCompleto,
            Cancelada = origen.Cancelada
        };
    }
}
