using System.Globalization;

namespace ISL_Service.Infrastructure.Facturacion;

/*
  ══════════════════════════════════════════════════════════════════════════
  LAS REGLAS DE *PREPARAR* UN COMPROBANTE, SACADAS DEL NAVEGADOR
  ══════════════════════════════════════════════════════════════════════════

  POR QUE EXISTE ESTE ARCHIVO
  ---------------------------
  `POST /api/ventas/facturas` recibe el CFDI YA ARMADO: receptor, conceptos uno
  por uno, regimen fiscal, uso del CFDI, forma y metodo de pago, y los totales
  desglosados en dinero / cascos / dinero+cascos. Eso significa que hoy el que
  decide QUE dice el comprobante es el NAVEGADOR —`nucleo/facturarRemision.ts`
  y `paginas/facturacion/FacturaDeRemisionPage.tsx`—, no el servidor.

  Mientras hubo un solo cliente eso se aguantaba. Con el movil son dos, y dos
  copias de una regla fiscal son dos comprobantes distintos para la misma
  remision: la copia que se equivoca es siempre la segunda, y el error no se
  descubre hasta que alguien compara dos PDF del SAT. Asi que la regla se
  mueve AQUI, en funciones puras y probables, y los dos clientes piden
  `preparar` en vez de calcular.

  LO QUE ESTE ARCHIVO **NO** HACE
  -------------------------------
  No timbra, no habla con Facturapi y no escribe en la base. Son cuentas y
  comprobaciones sobre datos que ya se leyeron. Por eso se puede probar entero
  con xUnit sin base y sin emitir un solo CFDI — que es la unica forma de
  probar facturacion en este proyecto (ver el encabezado de
  `VentasFacturacionDtos.cs`).

  CADA REGLA CITA DE DONDE SALE
  -----------------------------
  Formato de las citas:
    web:  Web/QR_Web/docs2/src/... :linea
    mac:  Legacy/Mac31/Mac31/Forms/Ventas/VentasFacturacion.cs :linea
  Donde el web y Mac31 difieren, GANA MAC31 y la diferencia queda escrita con
  las dos cifras medidas. Donde cambiar el numero cambiaria un CFDI real, la
  diferencia se REPORTA y no se aplica por cuenta propia: ver
  `DivergenciasConLegacy`.
*/
public static class ReglasPreparacionFactura
{
    // ======================================================================
    // LECTURA TOLERANTE DE LOS RENGLONES DEL PROCEDIMIENTO
    // ======================================================================
    //
    // Los renglones llegan como `Dictionary<string, object?>` con los nombres
    // EXACTOS de las columnas del SP (`sp_n_ConsultaVentas`,
    // `sp_n_VentasDetalleMultiple`). El serializador de cada endpoint puede
    // cambiarles la caja, asi que se pregunta por los dos nombres y, si no
    // casa ninguno, se busca sin distinguir mayusculas. Es exactamente lo que
    // hace `crudo()` en el web (notaDeCredito.ts:217-230) y `_get()` en el
    // movil: apostar por una capitalizacion es una pantalla que se rompe el
    // dia que alguien toca un SP.

    public static object? Crudo(IReadOnlyDictionary<string, object?>? fila, params string[] nombres)
    {
        if (fila is null) return null;

        foreach (var n in nombres)
        {
            if (fila.TryGetValue(n, out var v) && v is not null) return v;
        }

        foreach (var llave in fila.Keys)
        {
            foreach (var n in nombres)
            {
                if (string.Equals(llave, n, StringComparison.OrdinalIgnoreCase))
                {
                    var v = fila[llave];
                    if (v is not null) return v;
                }
            }
        }

        return null;
    }

    public static string Texto(object? valor) => valor?.ToString()?.Trim() ?? string.Empty;

    /// <summary>
    /// El numero de un valor del SP, con la misma tolerancia que `numero()` del
    /// web (notaDeCredito.ts:236-240): se tiran los caracteres que no son
    /// digito, punto ni signo. Un "1,234.50" que llegue como texto se lee igual.
    /// </summary>
    public static decimal Numero(object? valor)
    {
        if (valor is null) return 0m;
        if (valor is decimal d) return d;
        if (valor is double db) return (decimal)db;
        if (valor is float f) return (decimal)f;
        if (valor is int i) return i;
        if (valor is long l) return l;
        if (valor is short s) return s;
        if (valor is bool b) return b ? 1m : 0m;

        var crudo = valor.ToString() ?? string.Empty;
        var limpio = new string(crudo.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());
        return decimal.TryParse(limpio, NumberStyles.Any, CultureInfo.InvariantCulture, out var n) ? n : 0m;
    }

    public static int Entero(object? valor) => (int)decimal.Truncate(Numero(valor));

    /// <summary>
    /// El formato de dinero de los MENSAJES de Mac31: `###,###,##0.00`
    /// (VentasFacturacion.cs:2457). Se respeta porque el mensaje entra en los
    /// avisos que la gente dicta por telefono, y "4480" y "4,480.00" no se
    /// dictan igual.
    /// </summary>
    public static string Dinero(decimal valor) =>
        valor.ToString("#,##0.00", CultureInfo.GetCultureInfo("es-MX"));

    // ======================================================================
    // LAS SEIS PUERTAS DEL LLAMADOR
    // ======================================================================

    /// Una remision, reducida a lo que deciden las puertas.
    public sealed class RemisionParaFacturar
    {
        public int IdVenta { get; set; }
        public int IdCliente { get; set; }
        public string FolioFtm { get; set; } = string.Empty;
        public decimal TotalPagar { get; set; }

        /// "SI" cuando esta cancelada, como lo devuelve la consulta.
        public string Cancelada { get; set; } = string.Empty;
    }

    /*
      LAS COMPROBACIONES DE `btnFacturar_Click` DE LA CONSULTA DE VENTAS, CON SU
      TEXTO EXACTO.

      web: nucleo/facturarRemision.ts:531-556  (`motivoQueImpideFacturar`)
      mac: ConsultarVentas.cs:4633-4760

      Se comprueban EN EL SERVIDOR y no solo en el llamador por la misma razon
      que el web las repite en la pantalla hija: una puerta que solo vive en
      quien abre la pantalla se salta escribiendo la direccion a mano — o, en el
      movil, llamando al endpoint con otros ids. Y aqui es la ultima vez que se
      puede comprobar antes de un documento irreversible.

      UNA DE LAS SEIS NO DICE NADA EN MAC31: un folio de CAMBIO DE VACIOS
      (`CV-`) hace `return` en silencio (ConsultarVentas.cs:4688 y :4745). Se
      respeta no inventarle un texto de Mac31, pero SI se contesta: un endpoint
      que no hace nada ni explica por que manda a la gente a preguntar. La frase
      es nuestra y se dice que lo es — es la misma que ya usa el web.
    */
    public static string MotivoQueImpideFacturar(IReadOnlyList<RemisionParaFacturar> filas)
    {
        if (filas.Count == 0) return "SELECCIONE UNA REMISIÓN.";

        // Un CFDI es de UN receptor: todas del mismo cliente
        // (ConsultarVentas.cs:4659).
        var primerCliente = filas[0].IdCliente;
        if (filas.Any(f => f.IdCliente != primerCliente))
            return "LAS REMISIONES DEBEN DE SER DEL MISMO CLIENTE.";

        foreach (var f in filas)
        {
            // El orden de estas tres es el de Mac31: total cero, luego CV-,
            // luego cancelada. Si faltan dos cosas se queja de la primera, y ese
            // orden es el que la gente ya aprendio.
            if (f.TotalPagar == 0m)
                return "NO SE PUEDE FACTURAR UNA REMISIÓN CON TOTAL A PAGAR CERO.";

            if (f.FolioFtm.Contains("CV-", StringComparison.OrdinalIgnoreCase))
                return $"El folio {f.FolioFtm} es un cambio de vacíos, no una remisión de venta: no se factura.";

            if (string.Equals(f.Cancelada.Trim(), "SI", StringComparison.OrdinalIgnoreCase))
                return $"LA REMISIÓN  {f.FolioFtm}  ESTA CANCELADA.";
        }

        return string.Empty;
    }

    // ======================================================================
    // CUANTO SE PUEDE Y CUANTO SE PROPONE FACTURAR
    // ======================================================================

    /*
      CUANTO FALTA POR FACTURAR DE ESTA REMISION.

          MontoPorFacturar = ImporteZ + Cascos - TotalFacturado

      web: nucleo/facturarRemision.ts:180-182
      mac: VentasFacturacion.cs:741-745

      `ImporteZ` es el DINERO y `Cascos` lo que se le cobro de cascos: en la reja
      de Mac31 son las columnas DINERO y CASCOS, y el TOTAL que ensena es la SUMA
      de las dos (alla `Importe` se REESCRIBE a `ImporteZ + Cascos`, :746-747).
      Comprobado contra Legacy: las dos ramas de empresa estan COMENTADAS en
      Mac31 (:739-752), o sea que hoy esta cuenta es la misma en Zaragoza y en
      Tauro. Por eso no lleva capacidad.
    */
    public static decimal PorFacturar(decimal importeZ, decimal cascos, decimal totalFacturado)
        => importeZ + cascos - totalFacturado;

    /*
      CUANTO PROPONE MAC31 FACTURAR DE ESTA REMISION, AL ABRIR.

      web: nucleo/facturarRemision.ts:199-206
      mac: VentasFacturacion.cs:257-279 — las cuatro ramas, en su orden, leidas
           y verificadas una por una contra el fuente.

          ImporteZ + Cascos == TotalFacturado  ->  0        ya se facturo todo
          ImporteZ == TotalFacturado           ->  Cascos   el dinero ya, los cascos no
          ImporteZ  >  TotalFacturado          ->  ImporteZ - TotalFacturado
          en otro caso                         ->  (ImporteZ + Cascos) - TotalFacturado

      Y OJO CON LA TERCERA, QUE SORPRENDE: con una remision NUEVA —nada
      facturado— `ImporteZ > TotalFacturado` es cierto, asi que cae en la tercera
      y propone SOLO EL DINERO, no el dinero mas los cascos. Los cascos de la
      remision quedan fuera de la propuesta inicial y hay que facturarlos en una
      segunda vuelta (que es cuando entra la segunda rama). Se comprobo contra el
      fuente porque parecia un error: el `else` que suma los cascos solo se
      alcanza cuando `ImporteZ < TotalFacturado`.

      LA SEGUNDA RAMA ES LA QUE NO SE ADIVINA, y es la razon de que esto no sea
      `PorFacturar`: cuando el dinero ya esta facturado, lo que se propone
      facturar son SOLO LOS CASCOS. Sin ella la pantalla abriria ofreciendo
      volver a facturar dinero ya facturado, y como el monto por remision es
      editable (ver `ElMontoSiempreEsEditable`) nadie lo notaria hasta tener dos
      CFDI por el mismo dinero.
    */
    public static decimal MontoQueProponeMac31(decimal importeZ, decimal cascos, decimal totalFacturado)
    {
        var conCascos = importeZ + cascos;

        if (conCascos == totalFacturado) return 0m;
        if (importeZ == totalFacturado) return cascos;
        if (importeZ > totalFacturado) return importeZ - totalFacturado;
        return conCascos - totalFacturado;
    }

    /*
      LA CASILLA "MODIFICAR MONTO" DE MAC31 ES INVISIBLE Y SIEMPRE ESTA MARCADA.

      mac: VentasFacturacion.cs:223 (`chkTotalModificado.Checked = true` en el
           Load) y su handler, que enciende el campo.
      web: FacturaDeRemisionPage.tsx:83-86, que ya tomo la misma decision.

      O sea: el monto por remision SIEMPRE se puede cambiar. Se deja escrito como
      constante y no como un `if` suelto para que nadie "arregle" el campo
      poniendolo de solo lectura creyendo que la casilla hacia algo.
    */
    public const bool ElMontoSiempreEsEditable = true;

    /*
      EL FACTOR DE COMISION DE TARJETA.

      mac: VentasFacturacion.cs:2438-2454
      web: FacturaDeRemisionPage.tsx:331

      Es el UNICO sitio donde el tope de lo que se puede facturar sube por encima
      de lo que falta: 3% con tarjeta de credito y 2% con debito, porque la
      comision del banco se factura tambien.

      SE COMPARA POR NUMERO, NO POR CADENA, Y ESO ES UNA CORRECCION MEDIDA.
      Mac31 compara `int.Parse(cboFormaPago.SelectedValue) == 4` / `== 28`. El
      web compara `formaPago === "4"` contra `idFeFormaPago`, que el DTO del
      backend declara como LA CLAVE DEL SAT y no un id numerico
      (ClientesAnexosDtos.cs:127-131). La clave del SAT de tarjeta de credito es
      "04", con cero delante, y `"04" !== "4"`: con un domicilio fiscal cuya
      forma de pago venga como "04" el web no aplica la comision y rechaza por
      el tope un total que Mac31 acepta. Aqui se parsea, asi que "4" y "04"
      valen lo mismo y las dos empresas se comportan como Mac31.
    */
    public static decimal FactorDeComision(string? formaPago)
    {
        var clave = Entero(formaPago);
        if (clave == 4) return 1.03m;   // Tarjeta de credito
        if (clave == 28) return 1.02m;  // Tarjeta de debito
        return 1m;
    }

    // ======================================================================
    // LOS CONCEPTOS DEL CFDI
    // ======================================================================

    /*
      ══════════════════════════════════════════════════════════════════════
      DIVERGENCIA MEDIDA ENTRE EL WEB Y MAC31: EL PRECIO QUE VIAJA AL CFDI
      ══════════════════════════════════════════════════════════════════════

      En la rama de Zaragoza, Mac31 lee el precio del concepto de la columna
      `FacturaPrecioListaConIva` (VentasFacturacion.cs:1411-1413) y lo manda en
      `PreciosListaConIvaRndLst` (:1429). El web lee `PrecioListaConIvaRnd`
      (nucleo/notaDeCredito.ts:289-296, usada por `conceptosDeLasPartidas`).

      NO SON LA MISMA COLUMNA. Medido en MacZ con
      `EXEC sp_n_VentasDetalleMultiple @IDsVenta='154134'`, partida 483747:

          PrecioListaConIvaRnd      =  2,388.68   (ya con el descuento aplicado)
          FacturaPrecioListaConIva  =  4,480.00   (precio de lista con IVA)
          MontoDescuento            =  1,747.86
          ImporteConIvaRnd          =  2,388.68
          FacturaSubtotalConIva     =  4,480.00

      O sea: el web manda el precio YA DESCONTADO *y ademas* manda el descuento
      en el mismo concepto. Mac31 manda el precio de lista y deja que el
      descuento lo aplique el comprobante. Son dos CFDI distintos para la misma
      remision.

      POR QUE NO SE CAMBIA AQUI POR CUENTA PROPIA
      -------------------------------------------
      Porque quien convierte esas listas en conceptos del CFDI es MacServicios2,
      que NO esta en este repositorio: desde aqui no se puede leer si aplica el
      descuento sobre el precio de lista, si usa `SubTotalesConUsadoConIvaLst`
      para cuadrar, ni como reparte el IVA. Elegir una de las dos columnas a
      ciegas cambia el importe de comprobantes fiscales REALES, y eso no es una
      decision que se tome sin ver el otro lado.

      QUE SE HACE ENTONCES
      --------------------
      `preparar` devuelve el concepto con la columna DEL WEB (para que el cuerpo
      sea identico al de hoy y nada cambie al conectar el movil) y ADEMAS, en
      cada concepto, `PrecioListaLegacy` y `SubtotalLegacy` con las columnas que
      lee Mac31, mas un AVISO que lo nombra. Asi la diferencia esta a la vista en
      la respuesta en vez de enterrada en dos archivos, y se resuelve con el dato
      delante cuando haya acceso a MacServicios2.
    */
    public static class DivergenciasConLegacy
    {
        public const string PrecioDelConcepto =
            "El precio del concepto sale de PrecioListaConIvaRnd (lo que manda el web). " +
            "Mac31 manda FacturaPrecioListaConIva, que en la partida medida vale 4,480.00 " +
            "contra 2,388.68. No se cambia desde aquí porque quien arma el CFDI con esas " +
            "listas es MacServicios2, que no está en este repositorio: cada concepto trae " +
            "las dos cifras (precioListaLegacy / subtotalLegacy) para poder decidirlo con " +
            "el dato delante.";

        public const string SumaDeListaDelDescuento =
            "La validación de descuento de Zaragoza divide entre la suma de " +
            "PrecioListaConIvaRnd; Mac31 divide entre la suma de " +
            "FacturaPrecioListaConIva (VentasFacturacion.cs:1411 y :1432). " +
            "Se calculan las dos y se bloquea solo si las DOS dan negativo: " +
            "bloquear con la del web sola rechazaría facturas que Mac31 acepta.";

        /*
          ESTE ES EL HALLAZGO GORDO DE LA VALIDACION DE DESCUENTO, y conviene
          leerlo entero antes de "arreglar" nada.

          Mac31 hace DOS pasadas (VentasFacturacion.cs:1496-1531). La primera
          divide `TotalFacturarDinero` entre la suma de los precios de lista; si
          sale descuento negativo, hace una SEGUNDA que divide
          `TotalFacturarDinero` entre `totalFacturarConCascos` (:1511-1512) — y
          es esa segunda la que decide (:1519).

          Haciendo la cuenta: `totalFacturarConCascos = dinero + cascos`, y
          `cascos >= 0`, asi que el cociente `dinero / conCascos` es SIEMPRE <= 1,
          el porcentaje <= 100, y el descuento real >= 0. O sea: EN MAC31 ESTA
          VALIDACION NUNCA BLOQUEA. El unico caso en que podria es con
          `conCascos == 0`, y entonces ya habia saltado antes "EL TOTAL A FACTURAR
          DEBE SER MAYOR DE CERO." (:1470).

          Comprobado leyendo el fuente linea por linea y clavado en
          `Con_cascos_el_segundo_intento_de_Mac31_casi_nunca_bloquea_y_eso_se_respeta`.

          El web, en cambio, divide el total CON CASCOS entre la suma de lista
          (FacturaDeRemisionPage.tsx:400-403) — otra cuenta — y encima usa la
          columna equivocada, asi que SI bloquea. Resultado: hay facturas que
          Mac31 emite y el web rechaza.

          QUE SE HACE: gana Mac31, asi que NO SE BLOQUEA. Pero no se tira el aviso:
          cuando la cuenta del web habria bloqueado, `preparar` lo dice en los
          `avisos`. Asi la persona se entera de que esta facturando por encima de
          los precios de lista —que es informacion util— sin que el telefono
          niegue algo que la computadora de la oficina concede.
        */
        public const string DescuentoNoBloqueaComoEnMac31 =
            "La validación de descuento de Zaragoza NO bloquea, porque en Mac31 tampoco " +
            "bloquea: su segunda pasada divide TotalFacturarDinero entre " +
            "totalFacturarConCascos (VentasFacturacion.cs:1511-1512) y ese cociente es " +
            "siempre <= 100%, así que el descuento real nunca sale negativo. El web usa otra " +
            "cuenta (el total CON cascos entre la suma de lista) y sí bloquea, o sea que hay " +
            "facturas que Mac31 emite y el web rechaza. Gana Mac31 y queda el aviso.";

        public static string SeFacturaPorEncimaDeLaLista(string porcentaje) =>
            $"Se está facturando por encima de los precios de lista: el descuento sale " +
            $"{porcentaje}%. Mac31 lo deja pasar (ver la nota de la segunda pasada) y aquí " +
            $"también, pero conviene revisar las piezas o el total antes de timbrar.";
    }

    /// Un concepto del CFDI tal y como lo arma `preparar`.
    public sealed class ConceptoPreparado
    {
        public string ClaveProdServ { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string ClaveUnidad { get; set; } = string.Empty;
        public decimal Cantidad { get; set; }
        public decimal PrecioUnitarioConIva { get; set; }
        public decimal Descuento { get; set; }
        public int IdProducto { get; set; }
        public string Codigo { get; set; } = string.Empty;

        /// La remision de la que sale la partida. No viaja al CFDI: sirve para
        /// que la pantalla pueda agrupar los conceptos por folio sin volver a
        /// pedir el detalle.
        public int IdVenta { get; set; }

        /// La partida de la remision. Es la llave con la que el cliente manda de
        /// vuelta la cantidad que se factura y si la partida va marcada.
        public int IdVentaDetalle { get; set; }

        /// Lo que trae la remision. Es el TOPE de `Cantidad` (ver
        /// `PrimerReclamoDeFactura`), y se publica para que la pantalla pueda
        /// decirlo sin calcularlo.
        public decimal CantidadOriginal { get; set; }

        /// <summary>
        /// El precio que leeria Mac31 (`FacturaPrecioListaConIva`). Ver
        /// <see cref="DivergenciasConLegacy.PrecioDelConcepto"/>.
        /// </summary>
        public decimal PrecioListaLegacy { get; set; }

        /// <summary>
        /// El subtotal que leeria Mac31 (`FacturaSubtotalConIva` en Zaragoza).
        /// </summary>
        public decimal SubtotalLegacy { get; set; }
    }

    /*
      LAS PARTIDAS DE LAS REMISIONES, TRADUCIDAS A CONCEPTOS.

      web: nucleo/notaDeCredito.ts:1322-1336 (`conceptosDeLasPartidas`) mas la
           sobreescritura de la cantidad de FacturaDeRemisionPage.tsx:465-471.
      mac: VentasFacturacion.cs:1398-1437 (el bucle de `gridDetalles`).

      NO SE CALCULA NINGUN PRECIO: se LEE la columna que ya viene calculada y
      redondeada por el procedimiento. Redondear por partida es justo donde se
      van los centavos, y hacerlo dos veces —una en la base y otra aqui— es
      tener dos reglas sobre el mismo peso.

      `conIva` es la capacidad `facturacionMuestraPreciosConIva`, que el backend
      ya deriva de `funcionalidad.Contains("ZARA")`
      (Application/Empresa/MapaDeCapacidades.cs:83). Con ella en false se leen
      `PrecioLista` e `Importe`, que es la rama que Mac31 tiene en el `else`
      (:1416-1420). Esa rama NO es verificable hoy: la unica empresa que no
      muestra precios con IVA es Tauro, y alli esta pantalla se alcanza igual —
      pero no hay una tercera instalacion con la que comprobar las cifras. Queda
      implementada porque es la que pide Mac31, y marcada como no medida.
    */
    public static List<ConceptoPreparado> ConceptosDeLasPartidas(
        IEnumerable<IReadOnlyDictionary<string, object?>> partidas,
        bool conIva,
        IReadOnlyDictionary<int, decimal>? cantidadesCapturadas = null,
        IReadOnlySet<int>? marcadas = null)
    {
        var salida = new List<ConceptoPreparado>();

        foreach (var p in partidas)
        {
            var idVentaDetalle = Entero(Crudo(p, "IDVentaDetalle", "idVentaDetalle"));

            // Solo las partidas MARCADAS van al CFDI (mac :1421, el
            // `if (bool.Parse(... "SELECCIONAR" ...))`). Sin lista de marcadas
            // van todas: es como abre la pantalla, con `chkSeleccionar.Checked =
            // true` en todas (mac :218, web FacturaDeRemisionPage.tsx:220-229).
            if (marcadas is not null && !marcadas.Contains(idVentaDetalle)) continue;

            var cantidadOriginal = Numero(Crudo(p, "Cantidad", "cantidad"));
            var cantidad = cantidadOriginal;
            if (cantidadesCapturadas is not null
                && cantidadesCapturadas.TryGetValue(idVentaDetalle, out var capturada))
            {
                // Aqui esta la facturacion PARCIAL por partida: la cantidad que
                // se teclea gana sobre la de la remision. El tope lo pone
                // `PrimerReclamoDeFactura`, no esto.
                cantidad = capturada;
            }

            salida.Add(new ConceptoPreparado
            {
                ClaveProdServ = Texto(Crudo(p, "ClaveSAT", "claveSAT", "ClaveProdServ")),
                Descripcion = Texto(Crudo(p, "Concepto", "concepto", "ConceptoFtm")),

                // "UnidadMedidaSAT" con las tres mayusculas al final es como lo
                // devuelve el SP (medido en MacZ). El web pide
                // "UnidadMedidaSat", que solo casa por la segunda pasada sin
                // distinguir caja; aqui se pregunta primero por el nombre real.
                ClaveUnidad = Texto(Crudo(p, "UnidadMedidaSAT", "UnidadMedidaSat", "unidadMedidaSat")),

                Cantidad = cantidad,
                PrecioUnitarioConIva = conIva
                    ? Numero(Crudo(p, "PrecioListaConIvaRnd", "precioListaConIvaRnd"))
                    : Numero(Crudo(p, "PrecioLista", "precioLista")),
                Descuento = Numero(Crudo(p, "MontoDescuento", "montoDescuento")),
                IdProducto = Entero(Crudo(p, "IDProducto", "idProducto")),
                Codigo = Texto(Crudo(p, "Codigo", "codigo")),

                IdVenta = Entero(Crudo(p, "IDVenta", "idVenta")),
                IdVentaDetalle = idVentaDetalle,
                CantidadOriginal = cantidadOriginal,

                PrecioListaLegacy = conIva
                    ? Numero(Crudo(p, "FacturaPrecioListaConIva"))
                    : Numero(Crudo(p, "PrecioLista", "precioLista")),
                SubtotalLegacy = conIva
                    ? Numero(Crudo(p, "FacturaSubtotalConIva"))
                    : Numero(Crudo(p, "ImportePartida"))
            });
        }

        return salida;
    }

    /*
      LOS CASCOS QUE SE *AGREGAN* A LA FACTURA.

      NO SON LOS CASCOS DE LA REMISION, y esto es lo que mas se confunde. En
      Mac31 hay un recuadro aparte, "CASCOS A FACTURAR", que se llena con el
      CATALOGO de tipos de usado y sus precios, con TODAS las cantidades en CERO
      (mac :325 y :338-342). La persona teclea cuantos cascos quiere facturar
      ademas de los productos, y cada tipo entra al CFDI como un concepto mas con
      su propia clave del SAT (`ClavesSatUsadoLst`, mac :1606).

      web: FacturaDeRemisionPage.tsx:473-486.

      `H87` es pieza, la unidad con la que viajan los acumuladores. El catalogo
      no trae unidad para el casco, y es lo que el web ya manda.

      El importe es `CostoVentaCreditoConIva * Cantidad`, igual que
      `CalculaTotalCascos` (mac :4042-4085). El precio ya viene CON IVA y
      redondeado por la base: multiplicar por 1.16 aqui daria centavos de
      diferencia contra Mac31.
    */
    public const string UnidadDelCasco = "H87";

    // ======================================================================
    // LOS TOTALES, DESGLOSADOS COMO EN MAC31
    // ======================================================================

    /*
      LOS TRES TOTALES VAN POR SEPARADO Y NO SE DERIVAN UNO DE OTRO.

      mac: VentasFacturacion.cs:1648-1650 manda `TotalFacturarDinero`,
           `TotalFacturarCascos` y `TotalFacturarDineroCascos` por separado,
           porque el renglon de factura los GUARDA y el documento los IMPRIME.
      web: nucleo/facturarRemision.ts:434-447, con el aviso de que
           `totalFacturarCascos` estuvo FIJO EN CERO y se vio capturando el
           cuerpo antes de timbrar: la factura se habria guardado diciendo que no
           se facturo nada de cascos.

      `TotalProductos` son PIEZAS de producto y `TotalUsados` son PIEZAS de casco
      —no importes—: es `TotalUsados` de Mac31 (:1600-1607).
    */
    public sealed class TotalesPreparados
    {
        public decimal TotalFacturarDinero { get; set; }
        public decimal TotalFacturarCascos { get; set; }
        public decimal TotalFacturarDineroCascos { get; set; }
        public int TotalProductos { get; set; }
        public int TotalUsados { get; set; }

        /// Lo que FALTA por facturar de todas las remisiones juntas, sin
        /// comision. Es el numero que la pantalla ensena como tope.
        public decimal TotalPorFacturar { get; set; }

        /// El tope de verdad: `TotalPorFacturar * FactorDeComision`.
        public decimal TopeDeFacturacion { get; set; }
    }

    /*
      LOS CASCOS SE SUMAN COMPLETOS A LA PRIMERA REMISION.

      No es un reparto nuestro: lo hace Mac31 con todas sus letras, y el
      comentario esta en el fuente — "A la primera remision se le suman los
      cascos" (VentasFacturacion.cs:1578-1582).
      web: FacturaDeRemisionPage.tsx:447-454.

      Si se repartieran proporcionalmente, la factura de Mac31 y la del web
      dirian cosas distintas sobre la MISMA remision, y el renglon de factura es
      lo que decide cuanto queda por facturar la proxima vez. Las remisiones que
      quedan en cero NO se mandan: el servidor se las salta igual, pero mandarlas
      hace que el contrato de las tres listas alineadas por posicion mienta.
    */
    public static List<(int IdVenta, string Folio, decimal Total)> RepartirPorRemision(
        IReadOnlyList<(int IdVenta, string Folio, decimal Monto)> remisiones,
        decimal totalCascos)
    {
        var salida = new List<(int, string, decimal)>();

        for (var i = 0; i < remisiones.Count; i++)
        {
            var r = remisiones[i];
            var total = r.Monto + (i == 0 ? totalCascos : 0m);
            if (total > 0m) salida.Add((r.IdVenta, r.Folio, total));
        }

        return salida;
    }

    // ======================================================================
    // LAS VALIDACIONES, EN EL ORDEN DE MAC31
    // ======================================================================

    /// Lo que hay que mirar para decidir si el comprobante se puede timbrar.
    public sealed class DatosDeLaFactura
    {
        /// Cuantos domicilios fiscales tiene el cliente.
        public int RfcsDisponibles { get; set; }

        /// Si se eligio uno.
        public bool HayRfcElegido { get; set; }

        public string FormaPago { get; set; } = string.Empty;
        public string MetodoPago { get; set; } = string.Empty;
        public string UsoCfdi { get; set; } = string.Empty;

        public decimal TotalDinero { get; set; }
        public decimal TotalCascos { get; set; }
        public decimal TopeDeFacturacion { get; set; }

        /// Las partidas marcadas con su cantidad capturada y la original.
        public List<(decimal Capturada, decimal Original)> Partidas { get; set; } = new();

        public int PiezasDeCascos { get; set; }

        public int CorreosDisponibles { get; set; }
        public int CorreosMarcados { get; set; }

        /// `facturacionMuestraPreciosConIva`: enciende la validacion de descuento.
        public bool EsZaragoza { get; set; }

        /// Σ (PrecioListaConIvaRnd × cantidad) de las marcadas — la del web.
        public decimal SumaDeListaWeb { get; set; }

        /// Σ (FacturaPrecioListaConIva × cantidad) de las marcadas — la de Mac31.
        public decimal SumaDeListaLegacy { get; set; }
    }

    /// Un rechazo, con el campo al que hay que llevar el foco.
    public sealed record Reclamo(string Mensaje, string Campo);

    /*
      `ValidacionesFacturacion` (mac :2379-2573) mas las dos de `btnFacturar_Click`
      (mac :1453 y :1470), en SU ORDEN, con SU TEXTO.
      web: FacturaDeRemisionPage.tsx:344-411.

      El texto se respeta palabra por palabra —incluido el "TIENER" de los
      correos, que es una errata de Mac31— porque es el que soporte reconoce al
      telefono. Donde Mac31 no dice nada se le pone destino al foco: un aviso que
      no lleva a ningun sitio obliga a buscar el campo a ojo en una pantalla con
      veinte.
    */
    public static Reclamo? PrimerReclamoDeFactura(DatosDeLaFactura d)
    {
        var totalConCascos = d.TotalDinero + d.TotalCascos;

        // mac :2379 y el "No tiene RFCs registrados." del Designer:1406. Sin
        // datos fiscales no hay receptor que meter en el CFDI.
        if (d.RfcsDisponibles == 0 || !d.HayRfcElegido)
            return new Reclamo("INGRESE EL RFC DEL CLIENTE.", "rfc");

        if (string.IsNullOrWhiteSpace(d.FormaPago))
            return new Reclamo("SELECCIONE LA FORMA DE PAGO.", "formaPago");

        if (string.IsNullOrWhiteSpace(d.MetodoPago))
            return new Reclamo("SELECCIONE EL MÉTODO DE PAGO.", "metodoPago");

        if (string.IsNullOrWhiteSpace(d.UsoCfdi))
            return new Reclamo("SELECCIONE EL USO DEL CFDI.", "usoCfdi");

        // mac :1468-1475, dentro del `if (chkTotalModificado.Checked)` — que
        // siempre es true, ver `ElMontoSiempreEsEditable`.
        if (totalConCascos == 0m)
            return new Reclamo("EL TOTAL A FACTURAR DEBE SER MAYOR DE CERO.", "monto");

        // mac :2456-2459. El importe va DENTRO del mensaje, con el formato de
        // Mac31: es lo que la persona compara contra lo que tecleo.
        if (totalConCascos > d.TopeDeFacturacion)
        {
            return new Reclamo(
                $"EL TOTAL A FACTURAR DEBE SER\nMENOR O IGUAL A $ {Dinero(d.TopeDeFacturacion)}",
                "monto");
        }

        // Por partida: lo capturado no puede pasar de lo que trae la remision
        // (mac :2512).
        foreach (var (capturada, original) in d.Partidas)
        {
            if (capturada > original)
            {
                return new Reclamo(
                    "LA CANTIDAD DE PRODUCTOS A FACTURAR\nNO PUEDE SER MAYOR A LA CANTIDAD DE PRODUCTOS DE LA REMISIÓN.",
                    "cantidades");
            }
        }

        // mac :1453 — sin partidas marcadas Y sin cascos no hay nada que
        // facturar. Las dos condiciones van juntas: se puede facturar SOLO
        // cascos, que es el caso de la segunda rama de `MontoQueProponeMac31`.
        if (d.Partidas.Count == 0 && d.PiezasDeCascos == 0)
            return new Reclamo("SELECCIONE LOS PRODUCTOS A FACTURAR.", "partidas");

        var piezas = d.Partidas.Sum(p => p.Capturada);
        if (piezas == 0m && d.PiezasDeCascos == 0)
        {
            return new Reclamo(
                "LA CANTIDAD DE PRODUCTOS A FACTURAR\nDEBE SER MAYOR DE CERO.",
                "cantidades");
        }

        // mac :2540 — la errata "TIENER" es de Mac31 y se conserva aqui porque
        // esta es la pantalla donde la tiene. (La de nota de credito lo escribe
        // bien; alli se usa el bueno.)
        if (d.CorreosDisponibles == 0)
            return new Reclamo("NECESITA TIENER AL MENOS UN CORREO ELECTRÓNICO.", "correos");

        if (d.CorreosMarcados == 0)
            return new Reclamo("SELECCIONE UN CORREO.", "correos");

        /*
          LA VALIDACION DE DESCUENTO DE ZARAGOZA: LAS DOS PASADAS DE MAC31, TAL
          CUAL, INCLUIDO EL HECHO DE QUE NO BLOQUEA.

          mac: VentasFacturacion.cs:1496-1531
          web: FacturaDeRemisionPage.tsx:388-408

          Ver `DivergenciasConLegacy.DescuentoNoBloqueaComoEnMac31`: la segunda
          pasada de Mac31 divide el dinero entre el total CON cascos, y ese
          cociente nunca pasa del 100%, asi que el descuento real nunca sale
          negativo. Se respeta: si aqui se bloqueara, el telefono negaria una
          factura que Mac31 emite, y eso no se le puede explicar a nadie.

          Lo que NO se copia porque en Mac31 esta COMENTADO: el tope por parametro
          (`FacturaPorcentajeDescMax`) y la ventana de contrasena para saltarlo
          (mac :1532-1546). Hoy solo se mira el SOBREPRECIO, no el descuento
          excesivo. Poner el tope seria inventar una regla que Mac31 no aplica.
        */
        if (d.EsZaragoza && DescuentoRealDeMac31(d.SumaDeListaLegacy, d.TotalDinero, totalConCascos) < 0m)
        {
            return new Reclamo(
                $"EL PORCENTAJE DE DESCUENTO " +
                $"{Dinero(DescuentoRealDeMac31(d.SumaDeListaLegacy, d.TotalDinero, totalConCascos))}%\n" +
                "ES MAYOR AL PERMITIDO.\n\nDEBE AJUSTAR EL NÚMERO DE PIEZAS O EL TOTAL A FACTURAR.",
                "monto");
        }

        return null;
    }

    /// <summary>
    /// Las dos pasadas del descuento de Mac31. Negativo = sobreprecio.
    /// </summary>
    /// <remarks>
    /// El redondeo a dos decimales con `Math.Round` por pasada es el de Mac31
    /// (:1500 y :1513). No es cosmetico: redondear ANTES de restar de 100 es lo
    /// que decide si un caso al filo sale en -0.01 o en 0.00.
    /// </remarks>
    public static decimal DescuentoRealDeMac31(
        decimal sumaDeLista, decimal totalDinero, decimal totalConCascos)
    {
        if (sumaDeLista <= 0m) return 0m;   // mac :1497, el `if (... > 0)`

        var porcentaje = Math.Round(totalDinero * 100m / sumaDeLista, 2, MidpointRounding.AwayFromZero);
        var real = 100m - porcentaje;

        if (real < 0m && totalConCascos > 0m)
        {
            // mac :1511-1512: el SEGUNDO intento divide entre el total CON
            // cascos, NO entre la suma de lista. Y esta es la que decide (:1519).
            porcentaje = Math.Round(totalDinero * 100m / totalConCascos, 2, MidpointRounding.AwayFromZero);
            real = 100m - porcentaje;
        }

        return real;
    }

    /// <summary>
    /// El AVISO (no bloqueo) de que se esta facturando por encima de la lista.
    /// Vacio = no hay nada que decir.
    /// </summary>
    /// <remarks>
    /// Usa la PRIMERA pasada de Mac31 —la que si detecta el sobreprecio— sin la
    /// segunda, que es la que lo tapa. O sea: se queda con la deteccion y se tira
    /// el bloqueo, que es lo unico que Mac31 no hace. Asi la persona se entera
    /// de un dato util sin que el telefono le niegue lo que la computadora de la
    /// oficina le concede.
    /// </remarks>
    public static string AvisoDeSobreprecio(bool esZaragoza, decimal sumaDeLista, decimal totalDinero)
    {
        if (!esZaragoza || sumaDeLista <= 0m) return string.Empty;

        var porcentaje = Math.Round(totalDinero * 100m / sumaDeLista, 2, MidpointRounding.AwayFromZero);
        var real = 100m - porcentaje;

        return real < 0m
            ? DivergenciasConLegacy.SeFacturaPorEncimaDeLaLista(Dinero(real))
            : string.Empty;
    }

    // ======================================================================
    // COMPLEMENTO DE PAGO
    // ======================================================================

    /*
      EL SALDO DE UNA FACTURA.

          Saldo = TotalPagarFactura - Abonos - NotasCredito

      web: nucleo/complementoDePago.ts:151-153
      mac: VentasComplementoPago.cs:146

      Es la unica cuenta de dinero que el cliente hacia, y se hace aqui por lo
      mismo que el web la centralizo en una funcion: la columna `Saldo` tambien
      viene del procedimiento, pero NO siempre —cuando la pantalla recibe varias
      facturas el encabezado suma los tres conceptos por separado y resta—, asi
      que con dos restas distintas el encabezado y el renglon pueden discrepar.
    */
    public static decimal SaldoDeFactura(decimal totalPagarFactura, decimal abonos, decimal notasCredito)
        => totalPagarFactura - abonos - notasCredito;

    public sealed class DatosDelComplemento
    {
        public int Facturas { get; set; }
        public HashSet<int> Clientes { get; set; } = new();

        /// La primera cancelada que se encuentre, con su folio. Vacio = ninguna.
        public string FolioCancelado { get; set; } = string.Empty;

        /// El primer folio sin saldo. Vacio = todas tienen saldo.
        public string FolioSinSaldo { get; set; } = string.Empty;

        public string FormaPago { get; set; } = string.Empty;

        /// Σ de lo que se escribe en cada factura.
        public decimal APagar { get; set; }

        /// Σ de los saldos.
        public decimal Saldo { get; set; }

        /// Lo que se teclea por factura, con su saldo y su folio.
        public List<(decimal Monto, decimal Saldo, string Folio)> Renglones { get; set; } = new();

        public int CorreosDisponibles { get; set; }
        public int CorreosMarcados { get; set; }
    }

    /*
      LAS PUERTAS DEL LLAMADOR Y LAS VALIDACIONES DEL COMPLEMENTO, EN SU ORDEN.

      web: paginas/facturacion/ComplementoDePagoPage.tsx:211-243 y los textos de
           nucleo/complementoDePago.ts:196-233.
      mac: VentasComplementoPago.cs (1,310 lineas, 8 acciones) y
           ConsultarVentasFacturas.cs para las cuatro puertas.

      LA PARCIALIDAD, EL SALDO ANTERIOR Y EL SALDO INSOLUTO —los tres datos que
      el SAT exige DENTRO del complemento— NO se calculan aqui ni se mandan. El
      DTO de Mac31 tiene campos para ellos (`InstallmentsLst`, `LastBalancesLst`,
      `UuidsLst`, `NumComplementosPago`) y la pantalla de alla NO LLENA NINGUNO:
      los calcula el procedimiento a partir de lo que se paga y de los abonos que
      la factura ya tiene. Repetir ese calculo seria tener dos reglas sobre un
      dato fiscal, y la copia es la que se equivoca.

      La errata de Mac31 que SI se corrige: "NECESITA TIENER..." del complemento.
      La pantalla de nota de credito lo escribe bien, asi que aqui se usa el
      bueno — es la misma decision que tomo el web
      (complementoDePago.ts:199-204).
    */
    public static Reclamo? PrimerReclamoDeComplemento(DatosDelComplemento d)
    {
        if (d.Facturas == 0)
            return new Reclamo("SELECCIONE UNA FACTURA.", "facturas");

        if (d.Clientes.Count > 1)
            return new Reclamo("LAS FACTURAS DEBEN DE PERTENECER AL MISMO CLIENTE.", "facturas");

        if (!string.IsNullOrEmpty(d.FolioCancelado))
            return new Reclamo($"LA FACTURA ESTA CANCELADA, FOLIO: {d.FolioCancelado}", "facturas");

        if (!string.IsNullOrEmpty(d.FolioSinSaldo))
            return new Reclamo($"LA FACTURA YA ESTA PAGADA, FOLIO: {d.FolioSinSaldo}", "facturas");

        if (string.IsNullOrWhiteSpace(d.FormaPago))
            return new Reclamo("SELECCIONE LA FORMA DE PAGO.", "formaPago");

        if (d.APagar <= 0m)
            return new Reclamo("INGRESE EL MONTO DEL PAGO.", "montos");

        if (d.APagar > d.Saldo)
        {
            return new Reclamo(
                $"EL MONTO DEL PAGO NO PUEDE\nSER MAYOR DE  $ {Dinero(d.Saldo)}",
                "montos");
        }

        // Por factura, y con el folio dentro: con cinco facturas marcadas, un
        // mensaje sin folio no dice cual hay que corregir.
        foreach (var (monto, saldo, folio) in d.Renglones)
        {
            if (monto > saldo)
            {
                return new Reclamo(
                    $"EL MONTO DEL PAGO $ {Dinero(monto)}\n  DEL FOLIO {folio}\n  NO PUEDE SER MAYOR AL SALDO $ {Dinero(saldo)}",
                    "montos");
            }
        }

        if (d.CorreosDisponibles == 0)
            return new Reclamo("NECESITA TENER AL MENOS UN CORREO ELECTRÓNICO.", "correos");

        if (d.CorreosMarcados == 0)
            return new Reclamo("SELECCIONE UN CORREO.", "correos");

        return null;
    }

    // ======================================================================
    // CORREOS
    // ======================================================================

    /*
      LOS CINCO CORREOS, PARTIDOS Y SIN HUECOS NI REPETIDOS.

      Son CINCO COLUMNAS en la base (`Email`..`Email5`), no una lista, y cada una
      puede traer varios separados por coma — Mac31 los concatena asi para llenar
      su rejilla (Ventas.cs:6485+). Mac31 los deja TODOS marcados
      (VentasFacturacion.cs:617-619 y :652-653), y eso se respeta: el correo de
      la factura va a todos los que el cliente tiene dados de alta, y desmarcar
      es la decision de quien factura.

      web: nucleo/facturarRemision.ts:232-238 y notaDeCredito.ts:543-548.
    */
    public static List<string> CorreosDe(params string?[] columnas)
    {
        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var salida = new List<string>();

        foreach (var columna in columnas)
        {
            if (string.IsNullOrWhiteSpace(columna)) continue;

            foreach (var trozo in columna.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var correo = trozo.Trim();
                if (correo.Length == 0) continue;
                if (vistos.Add(correo)) salida.Add(correo);
            }
        }

        return salida;
    }
}
