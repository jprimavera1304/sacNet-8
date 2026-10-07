namespace ISL_Service.Application.DTOs.Aviacion;

/*
  RECAUDACIONES DE AVIACION: EL COBRO DE LA CAJA.

  Es el movimiento central del modulo: la caja le cobra a un economico las
  cuentas que le toquen, y el renglon queda con su folio. Viven en la base LOCAL
  de la caja (los SP de recaudacion no reciben servidor), y de ahi suben al
  central por la sincronizacion.

  EL FILTRO ES ASI DE GRANDE PORQUE LA PANTALLA ES ASI
  ---------------------------------------------------
  Se busca por caja, concepto, cuenta, economico, permisionario u operador, y
  cada uno de los seis acepta UN id, una LISTA de ids o un RANGO de numeros. Son
  treinta y un parametros. Legacy los mandaba todos y armaba las seis listas
  pegandolas con "~"; se conserva, con las listas armadas aqui.

  Y AQUI ESTABA EL PEOR DEFECTO DEL LOTE
  --------------------------------------
  Legacy, antes de consultar, hacia esto (RecaudacionService.cs:51-57):

      if ((IDsRecaudacionLst.Count > 0) || (IDRecaudacion > 0)) {
          string IDsRecaudacion = string.Join("~", IDsRecaudacionLst);
          recaudacionInputDTO = new RecaudacionInputDTO();   // todo al suelo
          recaudacionInputDTO.IDsRecaudacion = IDsRecaudacion;
      }

  Si venia IDRecaudacion (un solo folio) y la LISTA venia vacia —que es el caso
  normal de "dame esta recaudacion"— la condicion se cumple, el DTO se
  reconstruye, y IDsRecaudacion queda en CADENA VACIA: se pierde el id que se
  pidio Y todos los demas filtros. El SP recibe todo en cero y devuelve TODAS las
  recaudaciones de la base. Pedir una, recibir todas.

  Aqui no hay modos: todos los filtros se aplican juntos, y pedir un id devuelve
  ese id.
*/

/// <summary>Filtros de la consulta de recaudaciones.</summary>
public class AviacionRecaudacionesFiltro
{
    public int IdRecaudacion { get; set; }
    public List<int> IdsRecaudacion { get; set; } = new();
    public int IdStatus { get; set; }

    public int IdCaja { get; set; }
    public List<int> IdsCaja { get; set; } = new();
    public int NumCajaInicial { get; set; }
    public int NumCajaFinal { get; set; }

    public int IdConcepto { get; set; }
    public List<int> IdsConcepto { get; set; } = new();
    public int NumConceptoInicial { get; set; }
    public int NumConceptoFinal { get; set; }

    public int IdCuenta { get; set; }
    public List<int> IdsCuenta { get; set; } = new();
    public int NumCuentaInicial { get; set; }
    public int NumCuentaFinal { get; set; }

    public int IdEconomico { get; set; }
    public List<int> IdsEconomico { get; set; } = new();
    public int NumEconomicoInicial { get; set; }
    public int NumEconomicoFinal { get; set; }

    public int IdPermisionario { get; set; }
    public List<int> IdsPermisionario { get; set; } = new();
    public int NumPermisionarioInicial { get; set; }
    public int NumPermisionarioFinal { get; set; }

    public int IdOperador { get; set; }
    public List<int> IdsOperador { get; set; } = new();
    public int NumNominaInicial { get; set; }
    public int NumNominaFinal { get; set; }

    public int FolioInicial { get; set; }
    public int FolioFinal { get; set; }

    public DateOnly? FechaInicial { get; set; }
    public DateOnly? FechaFinal { get; set; }
}

/// <summary>
/// Filtros de la consulta de recaudaciones recurrentes (las que se cobran cada
/// periodo sin que nadie las capture).
///
/// Es el mismo filtro menos caja, concepto, status y folio: su SP no los recibe.
/// </summary>
public class AviacionRecaudacionesRecurrentesFiltro
{
    public int IdRecaudacion { get; set; }

    public int IdCuenta { get; set; }
    public List<int> IdsCuenta { get; set; } = new();
    public int NumCuentaInicial { get; set; }
    public int NumCuentaFinal { get; set; }

    public int IdEconomico { get; set; }
    public List<int> IdsEconomico { get; set; } = new();
    public int NumEconomicoInicial { get; set; }
    public int NumEconomicoFinal { get; set; }

    public int IdPermisionario { get; set; }
    public List<int> IdsPermisionario { get; set; } = new();
    public int NumPermisionarioInicial { get; set; }
    public int NumPermisionarioFinal { get; set; }

    public int IdOperador { get; set; }
    public List<int> IdsOperador { get; set; } = new();
    public int NumNominaInicial { get; set; }
    public int NumNominaFinal { get; set; }

    public DateOnly? FechaInicial { get; set; }
    public DateOnly? FechaFinal { get; set; }
}

/// <summary>Filtros de la consulta de cortes de recaudacion.</summary>
public class AviacionRecaudacionesCortesFiltro
{
    public int IdRecaudacionCorte { get; set; }
    public int IdStatus { get; set; }

    /// <summary>El dia del corte.</summary>
    public DateOnly? FechaCorte { get; set; }

    /// <summary>1 = pregunta si existe el corte del dia anterior.</summary>
    public int ExisteCorteDiaAnterior { get; set; }

    /// <summary>1 = pide el ultimo dia con corte.</summary>
    public int UltimoDiaCorte { get; set; }
}

/// <summary>
/// Un cobro de caja.
///
/// Las dos listas paralelas (cuentas e importes) son las cuentas que se le
/// cobran al economico en este movimiento. El SP las recibe pegadas con "~" y las
/// recorre a la vez.
/// </summary>
public class CrearAviacionRecaudacionRequest
{
    /// <summary>
    /// Si este cobro es el reverso de otro, el folio original. 0 = no es reverso.
    /// </summary>
    public int IdRecaudacionOriginal { get; set; }

    /// <summary>El reverso que corrige este cobro. 0 = ninguno.</summary>
    public int IdRecaudacionReverso { get; set; }

    /// <summary>El economico al que se le cobra. Requerido.</summary>
    public int IdEconomico { get; set; }

    /// <summary>La cuenta principal del cobro.</summary>
    public int IdCuenta { get; set; }

    /// <summary>El operador que paga.</summary>
    public int IdOperador { get; set; }

    /// <summary>El motivo de fianza, cuando el cobro es un movimiento de fianza.</summary>
    public int IdMotivoFianza { get; set; }

    /// <summary>La caja que cobra. Requerido.</summary>
    public int IdCaja { get; set; }

    /// <summary>Cuantas cuentas trae el cobro.</summary>
    public int NumCuentas { get; set; }

    /// <summary>La suma de los importes de las cuentas.</summary>
    public decimal ImporteCuentas { get; set; }

    /// <summary>El total cobrado.</summary>
    public decimal ImporteTotal { get; set; }

    /// <summary>Las cuentas que se cobran. Requerido, al menos una.</summary>
    public List<int> IdsCuenta { get; set; } = new();

    /// <summary>El importe de cada cuenta. Mismo largo que la lista de cuentas.</summary>
    public List<decimal> ImportesCuenta { get; set; } = new();

    /// <summary>
    /// Para el cobro con tarjeta fuera del horario administrativo: la fecha con la
    /// que entra.
    /// </summary>
    public DateOnly? FechaTarjetaAdminFueraHorario { get; set; }

    public string? Observaciones { get; set; }

    /// <summary>El folio de la llanta, cuando aplica.</summary>
    public string? FolioLlanta { get; set; }

    /// <summary>Como paga: efectivo, tarjeta, transferencia.</summary>
    public string? TipoPago { get; set; }

    /// <summary>El monto que viene por transferencia, cuando el pago es mixto.</summary>
    public decimal MontoTransferencia { get; set; }
}

/// <summary>Cierra el dia de recaudacion.</summary>
public class CrearAviacionRecaudacionCorteRequest
{
    /// <summary>El dia que se cierra. Requerido.</summary>
    public DateOnly? FechaCorte { get; set; }
}
