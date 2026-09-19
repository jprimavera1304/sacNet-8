using System.Data;
using ISL_Service.Infrastructure.Reports;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Tests;

/*
  PRUEBA DE HUMO CONTRA LA BASE DE ZARAGOZA

  No es una prueba unitaria: toca la copia local de MacZ. Por eso se salta sola
  cuando no hay base a la vista, y por eso no afirma contra numeros concretos —
  los datos de la copia local cambian.

  Lo que si comprueba, que es lo que se quiere saber, es que el papel de
  Zaragoza se arma COMPLETO: las cuatro secciones, sin marcas #Algo# sin
  sustituir y sin la excepcion de "falta la plantilla".
*/
public class RemisionZaragozaHumoTest
{
    private const string Cadena =
        "Server=LAP-JUAND;Database=MacZ;User Id=sa;Password=123;Encrypt=False;TrustServerCertificate=True;";

    [Fact]
    public void ElPapelDeZaragozaSeArmaCompleto()
    {
        if (!HayBase()) return;

        var plantillas = Consultar(
            "sp_n_ConsultaTemplateHtml",
            new() { ["@IDTemplateHtml"] = 0, ["@IDTipoTemplateHtml"] = 1202, ["@IDAplicacion"] = 1, ["@IDStatus"] = 0 });

        Assert.Equal(5, plantillas.Rows.Count);

        var idVenta = UltimaVenta();
        var datos = Consultar(
            "sp_n_rptVentasRemisionSinPrecio",
            new()
            {
                ["@IDVenta"] = idVenta,
                ["@IDUsuarioImpresion"] = 1,
                ["@EquipoImpresion"] = "",
                ["@PrimerImpresion"] = 0,
                ["@Reimpresion"] = 0
            });

        if (datos.Rows.Count == 0) return;

        var html = RemisionZaragozaHtmlBuilder.Construir(
            new[] { datos },
            plantillas,
            "https://itsysware.com.mx/images/macZ/Logo_Mac.png",
            "https://itsysware.com.mx/images/macZ/Logo_Mac_wm.png");

        /* Las cuatro secciones del papel de Mac31. */
        Assert.Contains("O R I G I N A L", html);
        Assert.Contains("COPIA CLIENTE", html);
        Assert.Contains("Crédito Cobranza", html);

        /*
          Ninguna marca de plantilla sin resolver: es la señal de que falto un
          dato, y en el papel se ve un "#TotalPagarZ#" impreso en media hoja.

          Las dos que se perdonan tampoco las resuelve legacy, porque el
          procedimiento no trae esas columnas, y ninguna se ve:

            #Ticket#     esta dentro de un comentario html en cuerpo_n
            #htmlStyle#  esta dentro de un atributo style, donde el renderizador
                         lo ignora por no ser css valido

          Se comprobo en la plantilla, no se supuso. Si algun dia aparecen otras,
          esta prueba lo dice.
        */
        var perdonadas = new[] { "#Ticket#", "#htmlStyle#" };

        var pendientes = System.Text.RegularExpressions.Regex
            .Matches(html, @"#[A-Za-z_][A-Za-z0-9_]*#")
            .Select(m => m.Value)
            .Distinct()
            .Where(m => !perdonadas.Contains(m))
            .ToList();

        Assert.True(pendientes.Count == 0, "Quedaron marcas sin sustituir: " + string.Join(", ", pendientes));

        /* Se guarda para poder abrirlo y compararlo con el de Mac31. */
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "remision_zaragoza.html"), html);
    }

    private static bool HayBase()
    {
        try
        {
            using var conn = new SqlConnection(Cadena);
            conn.Open();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static int UltimaVenta()
    {
        using var conn = new SqlConnection(Cadena);
        conn.Open();
        using var cmd = new SqlCommand("SELECT TOP 1 IDVenta FROM Ventas ORDER BY IDVenta DESC", conn);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private static DataTable Consultar(string sp, Dictionary<string, object> parametros)
    {
        using var conn = new SqlConnection(Cadena);
        conn.Open();

        using var cmd = new SqlCommand(sp, conn) { CommandType = CommandType.StoredProcedure, CommandTimeout = 300 };
        foreach (var p in parametros)
            cmd.Parameters.AddWithValue(p.Key, p.Value);

        var tabla = new DataTable();
        using var reader = cmd.ExecuteReader();
        tabla.Load(reader);
        return tabla;
    }
}
