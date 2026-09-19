using ISL_Service.Application.DTOs.VentasConsulta;
using ISL_Service.Application.Interfaces;

namespace ISL_Service.Application.Services;

/*
  QUIEN PUEDE SACAR EL PAPEL, Y CON QUE BANDERAS

  Todo lo de aqui esta copiado de tres metodos de Mac31, en
  Legacy/Mac31/Mac31/Forms/Ventas/ConsultarVentas.cs:

    btnImprimir_Click    :4213   Imprimir(primerImpresion: 1, reimpresion: 0)
    btnReimprimir_Click  :4221   Imprimir(primerImpresion: 0, reimpresion: 1)
    Imprimir(...)        :4228   el cuerpo comun de los dos
    btnPantalla_Click    :3771   y MostrarPantalla :3825

  Son el MISMO metodo con las banderas cambiadas, asi que aqui tambien es una
  sola clase y lo que cambia es el parametro.

  ─────────────────────────────────────────────────────────────────────────────
  POR QUE ESTO NO SE PODIA UNIFICAR ENTRE LAS DOS EMPRESAS

  Se intento y no sale: legacy no ramifica "un detalle" por empresa, ramifica el
  camino entero. En Tauro imprimir manda el papel a la impresora de mostrador
  con las banderas puestas; en Zaragoza imprimir abre el MISMO reporte que
  Pantalla, sin banderas. No es la misma operacion con un ajuste, son dos.

  El corte va por Variables.funcionalidad ("TAU" / "ZARA") como alla, y no por
  el logo ni por el id de empresa: la funcionalidad sale de Constantes y es la
  que de verdad decide en Mac31.
*/
public static class VentasImpresionReglas
{
    public const string AccionPantalla = "pantalla";
    public const string AccionImprimir = "imprimir";
    public const string AccionReimprimir = "reimprimir";

    /// La pestaña "Emitidas" de Mac31 (enumVentasFiltrar.Emitidas).
    public const string VistaEmitidas = "remisiones";

    /// enumTipoDocumento.RemisionAjuste (Legacy/Mac31/Mac31/Utils/Enums.cs:101).
    private const int RemisionAjuste = 6;

    public static string Normalizar(string? accion)
    {
        var a = (accion ?? "").Trim().ToLowerInvariant();
        return a is AccionImprimir or AccionReimprimir ? a : AccionPantalla;
    }

    public static bool EsZaragoza(string? funcionalidad)
        => (funcionalidad ?? "").ToUpperInvariant().Contains("ZARA");

    /*
      LAS BANDERAS QUE DE VERDAD SE MANDAN

      En Tauro son las del boton. En Zaragoza son SIEMPRE cero, y no es una
      simplificacion nuestra: es lo que hace el codigo. En Imprimir(), la rama
      de Zaragoza (ConsultarVentas.cs, dentro del bloque
      `funcionalidad.Contains("ZARA")` con EsCentroServicio == 0) llama

          Funciones.VerVenta(enumReportes.Remision, IDsVenta, 0, IDDescuento)

      y se sale con `return` antes de llegar al servicio de impresion, que es el
      unico que lleva PrimerImpresion/Reimpresion. Y la rama de Centro de
      Servicio declara `int PrimerImpresion = 0, Reimpresion = 0;` con todas sus
      letras antes de llamar al reporte.

      O sea: en Zaragoza, Imprimir y Reimprimir entregan exactamente el mismo
      papel que Pantalla. Lo unico que los distingue alla es la pregunta que
      sale antes. Si algun dia eso se quiere cambiar, se cambia en Mac31
      primero: poner aqui un 1 haria que el web rechazara remisiones que Mac31
      si imprime, y nadie entenderia por que.

      Y OJO CON LO QUE MARCAN ESTAS BANDERAS. sp_n_VentasInformacion no solo
      lee: con Reimpresion = 1 REESCRIBE en Ventas quien imprimio, cuando y
      desde que equipo, y marca Pedidos como reimpreso. Es un efecto real sobre
      datos de legacy, es el que hace que el papel salga con el sello "PRIMER
      IMPRESIÓN AUTORIZADA POR", y es justo para eso que el boton existe.
    */
    public static (int PrimerImpresion, int Reimpresion) Banderas(string accion, string? funcionalidad)
    {
        if (EsZaragoza(funcionalidad))
            return (0, 0);

        return Normalizar(accion) switch
        {
            AccionImprimir => (1, 0),
            AccionReimprimir => (0, 1),
            _ => (0, 0)
        };
    }

    /*
      CUANDO SE PIDE LA CONTRASENA ROTATORIA

      Imprimir / Reimprimir  (ConsultarVentas.cs:4324)

          if ((!Globales.CONTRASENA_IMPRIMIR) && (Variables.EsCentroServicio == 0))

        dentro de `if (opcionVentas == (int)enumVentasFiltrar.Emitidas)`. No
        ramifica por empresa: se pide en las DOS. Y CONTRASENA_IMPRIMIR nace en
        false (Utils/Globales.cs:75) y las dos lineas que lo pondrian en true
        estan comentadas (ConsultarVentas.cs:506 y :4346), asi que en la
        practica se pide SIEMPRE, no una vez por sesion.

      Pantalla  (ConsultarVentas.cs:3793)

          solo si funcionalidad contiene "ZARA" y EsCentroServicio == 0.
          En Tauro, Pantalla no pide nada.
    */
    public static bool RequiereContrasena(string accion, string? vista, string? funcionalidad, int esCentroServicio)
    {
        if (esCentroServicio != 0)
            return false;

        var a = Normalizar(accion);

        if (a is AccionImprimir or AccionReimprimir)
            return string.Equals(vista, VistaEmitidas, StringComparison.OrdinalIgnoreCase);

        return EsZaragoza(funcionalidad);
    }

    /// El dialogo de impresora y copias (ConfirmacionImpresion) se muestra solo
    /// en Tauro: ConsultarVentas.cs:4377-4400 lo encierra en
    /// `if (Variables.funcionalidad.ToUpper().Contains("TAU"))`.
    public static bool ConfirmaImpresora(string accion, string? funcionalidad)
        => Normalizar(accion) is AccionImprimir or AccionReimprimir && !EsZaragoza(funcionalidad);

    /// "SE HARÁ LA PRIMER IMPRESIÓN DE LA NOTA, ¿DESEA CONTINUAR?"
    /// (ConsultarVentas.cs:4355, dentro de `if (reimpresion == 1)`).
    public static bool ConfirmaReimpresion(string accion)
        => Normalizar(accion) == AccionReimprimir;

    /*
      LOS FOLIOS QUE NO SALEN POR AQUI

      Mac31 los deja fuera en silencio —hace `return` y el boton parece
      averiado—. Aqui se devuelve el motivo, que es la unica diferencia
      deliberada con legacy: la regla es la misma, lo que cambia es que se
      explica.

        CV-  cambio de vigencia. En Imprimir (ConsultarVentas.cs:4245) corta el
             proceso entero, y en Pantalla (:3787 y :3849) tambien. Va en las
             dos empresas: el comentario de legacy dice "Zaragoza" pero el `if`
             no pregunta por la funcionalidad.

        A- / G-  remisiones de ajuste de garantia. En Tauro se van por
             Imprimir_A_ConCosto (ConsultarVentas.cs:4263 y :4275), que es el
             reporte de garantias con costo; en Zaragoza, Pantalla las corta
             (:3783-3788) porque alla "solo se pueden imprimir desde el modulo
             de Garantias / Cambios de Vigencia".

        Ninguno de esos dos reportes esta portado todavia, asi que en vez de
        entregar el papel equivocado se dice de donde hay que sacarlo.
    */
    public static string? Bloqueo(VentaParaCancelar venta, string accion, string? funcionalidad)
    {
        var folio = venta.FolioFtm ?? "";

        if (folio.Contains("CV-"))
            return "Los cambios de vigencia se imprimen desde el módulo de Cambios de Vigencia.";

        var esAjuste = venta.IdTipoDocumento == RemisionAjuste;
        var esGarantia = folio.Contains("A-") || folio.Contains("G-");

        if (esAjuste && esGarantia)
            return "Las remisiones de garantía se imprimen desde el módulo de Garantías.";

        /* Zaragoza corta A- y G- en Pantalla aunque no sean RemisionAjuste. */
        if (EsZaragoza(funcionalidad) && Normalizar(accion) == AccionPantalla && esGarantia)
            return "Las remisiones de garantía se imprimen desde el módulo de Garantías.";

        return null;
    }
}
