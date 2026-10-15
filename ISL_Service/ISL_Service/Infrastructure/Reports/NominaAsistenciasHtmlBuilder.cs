using System.Data;
using System.Globalization;

namespace ISL_Service.Infrastructure.Reports;

/*
  ══════════════════════════════════════════════════════════════════════
  LAS DOS ASISTENCIAS POR DIA (1025 ASISTENCIAS y 1024 SOLO ASISTENCIAS)
  ══════════════════════════════════════════════════════════════════════

  ESTO ES LO QUE ANTES ESTABA DECLARADO COMO IMPOSIBLE.

  La nota que vivia en el front y en ReportesCatalogo decia que estos dos
  reportes no se podian dar de alta porque las siete columnas de dias
  (#fechaDia1#..7, #fechaNombreDia1#..7, #asistenciaDia1#..7, #minsRetardo#,
  #totalPagado#) "no estan en lo que devuelve el procedimiento". Eso era cierto
  a medias y la conclusion era falsa: no estan en `sp_n_ConsultaEmpleadosTipoSueldoRecibo`
  —que trae al empleado y sus percepciones— pero SI estan, enteras, en otros dos
  procedimientos que existen en MacZ y en Produccion_svr:

    sp_n_ConsultaEmpleadosPeriodoAsistencia @IDPeriodoTipoSueldo, @IDEmpleado, @IDStatus
        -> idEmpleado, nombre, y por cada dia 1..7:
           fechaDiaN, diaEntradaN, diaSalidaN, diaHorasLaboradasN, diaMinutosLaboradosN
           + minsRetardo, asistencias, inasistencias, vacaciones
        (es el MISMO que ya usa la rejilla del modulo de Asistencias)

    sp_n_ConsultaPeriodosTipoSueldoDias @IDPeriodoTipoSueldo, 0, 0, 0
        -> los dias del periodo, con `fechaDiaFtm`, que son los encabezados

  La "rama dedicada" de legacy que la nota citaba es
  MacServicios2/Mac3Servicios/Models/Service/ReportesServ/ReporteAsistenciaService.cs,
  y esta clase es ese archivo portado: mismo juego de columnas, mismo
  FormateaFecha, misma agrupacion por IDAgente y misma paginacion por maxLineas.

  LO QUE HACE LEGACY Y AQUI SE REPITE TAL CUAL, aunque parezca raro:

  · El total pagado del pie (#totalPagado#) es UNO para todo el reporte, no por
    pagina ni por grupo: legacy lo acumula sobre nomina + comision y luego lo
    escribe en TODOS los renglones (ReporteAsistenciaService.cs:238-239).
  · Solo suma los netos POSITIVOS (`> 0`), asi que un recibo en negativo no
    resta (:183 y :227).
  · El empleado que esta en los recibos pero no en las asistencias se CAE del
    reporte en silencio: legacy lo envuelve en try/catch con el comentario "El
    empleado no trabajo esa semana" (:188). No se inventa un renglon vacio.
  · Los renglones van ordenados por nombre (:243).
  · #IDAgente# es siempre 0 (:181), asi que sale un solo juego de paginas. Se
    agrupa igual para que el dia que legacy lo llene el papel se parta solo.
*/
internal static class NominaAsistenciasHtmlBuilder
{
    /// <summary>
    /// Arma el HTML de los reportes 1024 y 1025 con las siete columnas de dias
    /// resueltas. Devuelve "" si no hubo ni un empleado que cruzara.
    /// </summary>
    /// <param name="incluirImportes">
    /// Si las columnas de dinero (`netoPagado` por empleado y `totalPagado` al
    /// pie) llevan cifra o van en blanco.
    ///
    /// VA EN false PARA EL 1024 ("SOLO ASISTENCIAS") Y EN true PARA EL 1025.
    ///
    /// Los dos reportes comparten este armador y los dos reciben los recibos
    /// completos de `sp_n_ConsultaEmpleadosTipoSueldoRecibo`, que trae
    /// `netoPagado`. Lo unico que hoy impide que el 1024 lo imprima es que su
    /// plantilla `detalle` tiene la celda de `#netoPagado#` COMENTADA en HTML, y
    /// una plantilla es un renglon de `Template_Html`: se edita sin pasar por el
    /// codigo y sin revision. Quien quitara ese comentario destapaba los sueldos
    /// a todo el que tuviera `reportes.ver_modulo`, porque el 1024 no pide la
    /// llave de sueldos.
    ///
    /// Con la columna en blanco desde el origen, descomentar la celda ya no
    /// ensena nada: el candado del 1024 deja de ser un comentario de HTML. El
    /// 1025 SI imprime el sueldo -es su columna SUELDO- y por eso el que pide la
    /// llave `empleados.sueldo.ver` es el 1025 (ver ReportesCatalogo).
    /// </param>
    internal static string Construir(
        string logo,
        string reporteNombre,
        DataTable nomina,
        DataTable comision,
        DataTable asistencias,
        DataTable dias,
        DataTable templates,
        int maxLineas,
        bool incluirImportes)
    {
        var cuerpoTemplate = LeerPlantilla(templates, "cuerpo_n", "cuerpo");
        var detalleTemplate = LeerPlantilla(templates, "detalle");
        var totalesTemplate = LeerPlantilla(templates, "totales");

        var encabezadoDias = ArmarEncabezadoDeDias(dias);
        var detalle = ArmarTablaDeDetalle(nomina, comision, asistencias, incluirImportes);

        if (detalle.Rows.Count == 0)
        {
            var vacio = cuerpoTemplate
                .Replace("#Logo#", logo)
                .Replace("#Titulo#", reporteNombre)
                .Replace("#NumPagina#", "1")
                .Replace("#TotalPaginas#", "1");
            vacio = ReemplazarFila(encabezadoDias, encabezadoDias.Rows[0], vacio);
            vacio = vacio.Replace(
                "#Detalle#",
                "<tr><td colspan='16' align='center'><br /><br /> <u> <h1> No se encontro información </h1> </u></td></tr>");
            return vacio.Replace("#Totales#", "");
        }

        var html = "";
        foreach (var grupo in AgruparPorAgente(detalle))
        {
            var totalPaginas = grupo.Length / maxLineas;
            if (grupo.Length % maxLineas != 0) totalPaginas++;

            var numPagina = 1;
            var linea = 0;
            var numRegistro = 0;
            var htmlCuerpo = "";
            var htmlDetalle = "";

            foreach (var fila in grupo)
            {
                numRegistro++;

                if (linea == 0)
                {
                    htmlCuerpo = "<p style='page-break-before: always'></p>";
                    htmlCuerpo += ArmarEncabezado(
                        cuerpoTemplate, logo, reporteNombre, numPagina, totalPaginas,
                        detalle, grupo[0], encabezadoDias);
                }

                htmlDetalle += ReemplazarFila(detalle, fila, detalleTemplate);

                /*
                  LA CUENTA DE LINEAS ES LA DE LEGACY, CON SU DESFASE DE UNO.

                  `if (linea >= maxLineas)` se evalua DESPUES de incrementar en el
                  else, asi que la primera hoja lleva maxLineas+1 renglones. No se
                  "arregla": corregirlo cambiaria donde cae el salto de pagina
                  contra el papel que hoy sale de Mac31.
                */
                if (linea >= maxLineas)
                {
                    linea = 0;
                    numPagina++;
                    htmlCuerpo = htmlCuerpo.Replace("#Detalle#", htmlDetalle);
                    htmlCuerpo = numRegistro == grupo.Length
                        ? htmlCuerpo.Replace("#Totales#", ReemplazarFila(detalle, grupo[0], totalesTemplate))
                        : htmlCuerpo.Replace("#Totales#", "");
                    html += htmlCuerpo;
                    htmlDetalle = "";
                }
                else
                {
                    linea++;
                }
            }

            htmlCuerpo = htmlCuerpo.Replace("#Detalle#", htmlDetalle);
            htmlCuerpo = htmlCuerpo.Replace("#Totales#", ReemplazarFila(detalle, grupo[0], totalesTemplate));
            html += htmlCuerpo;
            html += "<p style='page-break-before: always'></p>";
        }

        return html;
    }

    /*
      LOS SIETE ENCABEZADOS DE DIA.

      Legacy arma una tabla de una sola fila con fechaDia1..7 y fechaNombreDia1..7,
      y el nombre del dia lo saca de ToLongDateString() partido por la coma y en
      mayusculas (ReporteAsistenciaService.cs:96-102). Con es-ES eso da "LUNES".
    */
    private static DataTable ArmarEncabezadoDeDias(DataTable dias)
    {
        var tabla = new DataTable();
        for (var i = 1; i <= 7; i++)
        {
            tabla.Columns.Add("fechaDia" + i, typeof(string));
            tabla.Columns.Add("fechaNombreDia" + i, typeof(string));
        }

        var fila = tabla.NewRow();
        for (var i = 1; i <= 7; i++)
        {
            fila["fechaDia" + i] = "";
            fila["fechaNombreDia" + i] = "";
        }

        var cultura = new CultureInfo("es-ES");
        var tope = Math.Min(7, dias.Rows.Count);
        for (var i = 0; i < tope; i++)
        {
            var crudo = Convert.ToString(dias.Rows[i]["fechaDiaFtm"]) ?? "";
            fila["fechaDia" + (i + 1)] = crudo;

            if (DateTime.TryParse(crudo, cultura, DateTimeStyles.None, out var fecha))
                fila["fechaNombreDia" + (i + 1)] = cultura.DateTimeFormat.GetDayName(fecha.DayOfWeek).ToUpperInvariant();
        }

        tabla.Rows.Add(fila);
        return tabla;
    }

    /*
      EL CRUCE: RECIBO (nomina y comision) x ASISTENCIAS DEL PERIODO.

      Es el bucle de ReporteAsistenciaService.cs:150-239, los dos pases, con sus
      mismas columnas y en el mismo orden.
    */
    private static DataTable ArmarTablaDeDetalle(
        DataTable nomina, DataTable comision, DataTable asistencias, bool incluirImportes)
    {
        var tabla = new DataTable();
        tabla.Columns.Add("nombre", typeof(string));
        tabla.Columns.Add("netoPagado", typeof(decimal));
        for (var i = 1; i <= 7; i++)
            tabla.Columns.Add("asistenciaDia" + i, typeof(string));
        tabla.Columns.Add("minsRetardo", typeof(int));
        tabla.Columns.Add("inasistencias", typeof(decimal));
        tabla.Columns.Add("vacaciones", typeof(decimal));
        tabla.Columns.Add("totalPagado", typeof(decimal));
        tabla.Columns.Add("fechaInicial", typeof(string));
        tabla.Columns.Add("fechaFinal", typeof(string));
        tabla.Columns.Add("fechaImpresion", typeof(string));
        tabla.Columns.Add("numeroSemana", typeof(int));
        tabla.Columns.Add("IDAgente", typeof(int));

        // El indice de asistencias por empleado. Legacy lo resuelve con un
        // DataTable.Select por renglon; con 85 empleados eso son 85 barridos.
        var porEmpleado = new Dictionary<string, DataRow>(StringComparer.Ordinal);
        if (asistencias.Columns.Contains("idEmpleado"))
        {
            foreach (DataRow fila in asistencias.Rows)
            {
                var clave = Convert.ToString(fila["idEmpleado"]) ?? "";
                if (clave != "" && !porEmpleado.ContainsKey(clave))
                    porEmpleado.Add(clave, fila);
            }
        }

        // La cabecera (fechas, semana) sale del PRIMER renglon de nomina, igual
        // que en legacy: `dtEmpleadoNominaRecibo.Rows[0]` en los dos pases.
        var cabecera = nomina.Rows.Count > 0 ? nomina.Rows[0] : null;

        decimal totalPagado = 0;
        AgregarPase(nomina, tabla, porEmpleado, cabecera, ref totalPagado, incluirImportes);
        AgregarPase(comision, tabla, porEmpleado, cabecera, ref totalPagado, incluirImportes);

        /*
          SIN IMPORTES, LA COLUMNA SE QUEDA EN NULL, NO EN CERO.

          Un 0.00 impreso donde va el sueldo dice "a este empleado no se le pago",
          que es una afirmacion falsa; el null sale como celda vacia en
          ReemplazarFila (FormatoFechaLegacy.Celda da "" y FormateaNumero no toca
          la cadena vacia), que es "este papel no trae ese dato". Es la misma
          regla que ya sigue la rejilla de asistencias con sus nulos.
        */
        foreach (DataRow fila in tabla.Rows)
            fila["totalPagado"] = incluirImportes ? totalPagado : (object)DBNull.Value;

        if (tabla.Rows.Count == 0)
            return tabla;

        tabla.DefaultView.Sort = "nombre asc";
        return tabla.DefaultView.ToTable();
    }

    private static void AgregarPase(
        DataTable origen,
        DataTable destino,
        Dictionary<string, DataRow> porEmpleado,
        DataRow? cabecera,
        ref decimal totalPagado,
        bool incluirImportes)
    {
        if (origen.Rows.Count == 0 || !origen.Columns.Contains("idEmpleado"))
            return;

        foreach (DataRow recibo in origen.Rows)
        {
            var clave = Convert.ToString(recibo["idEmpleado"]) ?? "";
            if (!porEmpleado.TryGetValue(clave, out var asistencia))
                continue;   // "El empleado no trabajo esa semana" (legacy :188)

            var fila = destino.NewRow();
            fila["nombre"] = Convert.ToString(recibo["nombre"]) ?? "";
            // El sueldo del empleado solo se copia si este reporte lo imprime.
            // Ver el parametro `incluirImportes` de Construir.
            fila["netoPagado"] = incluirImportes ? ADecimal(recibo, "netoPagado") : (object)DBNull.Value;

            for (var i = 1; i <= 7; i++)
            {
                fila["asistenciaDia" + i] = FormateaFecha(
                    Convert.ToString(asistencia["diaEntrada" + i]) ?? "",
                    Convert.ToString(asistencia["diaSalida" + i]) ?? "",
                    i == 7);
            }

            fila["minsRetardo"] = AEntero(asistencia, "minsRetardo");
            fila["inasistencias"] = ADecimal(asistencia, "inasistencias");
            fila["vacaciones"] = ADecimal(asistencia, "vacaciones");

            fila["fechaInicial"] = cabecera is null ? "" : Convert.ToString(cabecera["fechaInicial"]) ?? "";
            fila["fechaFinal"] = cabecera is null ? "" : Convert.ToString(cabecera["fechaFinal"]) ?? "";
            fila["fechaImpresion"] = cabecera is null ? "" : Convert.ToString(cabecera["fechaImpresion"]) ?? "";
            fila["numeroSemana"] = cabecera is null ? 0 : AEntero(cabecera, "numeroSemana");
            fila["IDAgente"] = 0;
            fila["totalPagado"] = incluirImportes ? 0m : (object)DBNull.Value;

            var neto = ADecimal(recibo, "netoPagado");
            if (neto > 0) totalPagado += neto;

            destino.Rows.Add(fila);
        }
    }

    /*
      COMO SE ESCRIBE UN DIA EN LA COLUMNA, PALABRA POR PALABRA DE LEGACY
      (ReporteAsistenciaService.cs:419-441).

      · "FALTA" y "VACACION" salen en negritas.
      · "----" (sin captura) de lunes a sabado se imprime "SI VINO"; el DOMINGO
        se imprime "----" en negritas. Es el unico sitio donde el septimo dia se
        trata distinto, y de ahi sale el `esDomingo` del septimo parametro.
      · Con salida "----" se imprime solo la entrada; con salida vacia tampoco se
        pone el guion de separacion.
    */
    private static string FormateaFecha(string diaEntrada, string diaSalida, bool esDomingo)
    {
        if (diaEntrada == "FALTA" || diaEntrada == "VACACION")
            return "<b>" + diaEntrada + "</b>";

        if (diaEntrada == "----")
            return esDomingo ? "<b>" + diaEntrada + "</b>" : "SI VINO";

        if (diaSalida == "----")
            return diaEntrada;

        return diaEntrada + (diaSalida == "" ? "" : " - " + diaSalida);
    }

    private static IEnumerable<DataRow[]> AgruparPorAgente(DataTable tabla)
    {
        var orden = new List<string>();
        var grupos = new Dictionary<string, List<DataRow>>(StringComparer.Ordinal);

        foreach (DataRow fila in tabla.Rows)
        {
            var clave = Convert.ToString(fila["IDAgente"]) ?? "0";
            if (!grupos.TryGetValue(clave, out var lista))
            {
                lista = new List<DataRow>();
                grupos.Add(clave, lista);
                orden.Add(clave);
            }
            lista.Add(fila);
        }

        foreach (var clave in orden)
            yield return grupos[clave].ToArray();
    }

    private static string ArmarEncabezado(
        string cuerpoTemplate,
        string logo,
        string reporteNombre,
        int numPagina,
        int totalPaginas,
        DataTable detalle,
        DataRow primerRenglon,
        DataTable encabezadoDias)
    {
        var html = cuerpoTemplate
            .Replace("#Logo#", logo)
            .Replace("#Titulo#", reporteNombre)
            .Replace("#NumPagina#", numPagina.ToString(CultureInfo.InvariantCulture))
            .Replace("#TotalPaginas#", totalPaginas.ToString(CultureInfo.InvariantCulture));

        html = ReemplazarFila(detalle, primerRenglon, html);
        html = ReemplazarFila(encabezadoDias, encabezadoDias.Rows[0], html);
        return html;
    }

    private static string ReemplazarFila(DataTable tabla, DataRow fila, string html)
    {
        foreach (DataColumn columna in tabla.Columns)
        {
            var tag = "#" + columna.ColumnName + "#";
            var valor = FormatoFechaLegacy.Celda(fila[columna.ColumnName]);
            if (valor != "")
                valor = FormateaNumero(columna.DataType, valor);

            html = html.Replace(tag, valor);
            html = html.Replace(tag.ToLowerInvariant(), valor);
        }

        return html;
    }

    /*
      Legacy formatea en Funciones.FormateaNumero: decimal con dos cifras y
      separador de miles, entero a secas. Lo mismo que hace el motor de ventas
      para las celdas que no son totales.
    */
    private static string FormateaNumero(Type tipo, string valor)
    {
        if (tipo == typeof(decimal) || tipo == typeof(double) || tipo == typeof(float))
        {
            return decimal.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out var numero)
                ? numero.ToString("###,##0.00", CultureInfo.InvariantCulture)
                : valor;
        }

        return valor;
    }

    private static string LeerPlantilla(DataTable templates, params string[] descripciones)
    {
        foreach (var descripcion in descripciones)
        {
            var filas = templates.Select($"descripcion = '{descripcion.Replace("'", "''")}'");
            if (filas.Length > 0)
                return Convert.ToString(filas[0]["html"]) ?? "";
        }

        throw new InvalidOperationException($"Template HTML '{descripciones[0]}' no encontrado.");
    }

    private static decimal ADecimal(DataRow fila, string columna)
    {
        if (!fila.Table.Columns.Contains(columna)) return 0m;
        return decimal.TryParse(
            Convert.ToString(fila[columna]),
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out var valor) ? valor : 0m;
    }

    private static int AEntero(DataRow fila, string columna)
    {
        if (!fila.Table.Columns.Contains(columna)) return 0;
        return int.TryParse(
            Convert.ToString(fila[columna]),
            NumberStyles.Any,
            CultureInfo.InvariantCulture,
            out var valor) ? valor : 0;
    }
}
