namespace ISL_Service.Application.DTOs.VentasCancelacion;

/*
  CANCELAR UNA REMISION

  Todo lo que hay aqui sale de Legacy/Mac31/Forms/Ventas/ConsultarVentas.cs
  (btnCancelarVenta_Click y ValidaCancelarVenta) y del procedimiento
  sp_n_CancelarVenta. Ninguna regla es nuestra: lo que decide si una remision se
  puede cancelar lo decide legacy, aqui solo se repite.

  POR QUE SON DOS PASOS Y NO UNO

  Mac31 hace lo mismo sin darse cuenta: primero recorre TODO lo marcado
  validando, y solo si todo pasa pregunta "¿desea cancelar?". Si algo no pasa,
  enseña el motivo y no cancela NADA —ni siquiera las que si podian—. Ese primer
  recorrido es /verificar; el segundo, el que ya escribe, es /cancelar.

  El paso de verificar no es un adorno para la pantalla: /cancelar lo vuelve a
  correr entero antes de tocar la base. El cliente no es de fiar y entre que se
  pinta el dialogo y se confirma, alguien mas pudo cancelar, pagar o facturar la
  misma remision desde Mac31.
*/

public class VentasCancelacionRequest
{
    public List<int> IdsVenta { get; set; } = new();

    /*
      La contrasena de supervisor. Solo hace falta cuando alguna de las
      remisiones sigue dentro de un concentrado abierto —ver ConcentradoFolio—.
      Va vacia el resto de las veces.
    */
    public string? Contrasena { get; set; }
}

public class VentasCancelacionVerificarRequest
{
    public List<int> IdsVenta { get; set; } = new();
}

/*
  EL VEREDICTO DE UNA REMISION ANTES DE TOCARLA

  Motivo trae el texto de legacy TAL CUAL cuando no se puede cancelar. No se
  reescribe "mas bonito": es el que la gente lleva años leyendo y el que repite
  por telefono cuando llama a preguntar.
*/
public class VentasCancelacionCandidato
{
    public int IdVenta { get; set; }
    public string FolioFtm { get; set; } = string.Empty;
    public string NombreCliente { get; set; } = string.Empty;

    public bool PuedeCancelar { get; set; }
    /// Vacio cuando si se puede. Si no, el mensaje de Mac31 sin tocar.
    public string Motivo { get; set; } = string.Empty;

    /*
      La remision sigue colgada de un concentrado que no esta cancelado. Mac31
      no la bloquea: ofrece quitarla del concentrado, y para eso pide la
      contrasena de supervisor. Por eso esto NO apaga PuedeCancelar.
    */
    public bool RequiereContrasena { get; set; }
    public int FolioConcentrado { get; set; }
}

public class VentasCancelacionVerificarResponse
{
    public List<VentasCancelacionCandidato> Candidatos { get; set; } = new();
    /// Ninguna marcada tiene impedimento: la pantalla ya puede preguntar.
    public bool PuedeContinuar { get; set; }
    /// Al menos una sigue en un concentrado abierto: hay que pedir la contrasena.
    public bool RequiereContrasena { get; set; }
}

/*
  EL RESULTADO DE CADA FOLIO, NO UN SI O NO PARA TODO

  sp_n_CancelarVenta se llama una vez por remision y cada llamada puede salir
  distinta: la primera cancela y la segunda se topa con que no hay usados en
  inventario. Mac31 arma un texto con un renglon por folio y lo enseña completo.
  Aqui se devuelve esa misma lista, ya separada, para que la pantalla la pinte
  como quiera.
*/
public class VentasCancelacionResultado
{
    public int IdVenta { get; set; }
    public string Folio { get; set; } = string.Empty;
    public bool Cancelada { get; set; }
    /// El mensaje del procedimiento, palabra por palabra.
    public string Mensaje { get; set; } = string.Empty;
}

public class VentasCancelacionResponse
{
    public List<VentasCancelacionResultado> Resultados { get; set; } = new();
    public int Canceladas { get; set; }
    public int Fallidas { get; set; }
}
