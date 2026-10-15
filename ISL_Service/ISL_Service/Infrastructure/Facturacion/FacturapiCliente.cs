using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ISL_Service.Application.DTOs.VentasFacturacion;

namespace ISL_Service.Infrastructure.Facturacion;

/*
  EL CLIENTE DE FACTURAPI, POR HTTP Y NO POR SDK

  Legacy usa Facturapi.dll (3.2.0), un ensamblado de .NET Framework que no se
  puede referenciar desde net8.0. Lo que ese SDK hace es una llamada REST:
  `POST https://www.facturapi.io/v2/invoices` con autenticacion Basic y el secreto
  como usuario. Aqui se hace esa misma llamada con HttpClient.

  Ventaja practica de no depender del SDK: la version del SDK estaba congelada en
  2021 y arrastraba su propia cadena de dependencias; la API REST de Facturapi es
  estable y versionada en la URL.

  LOS MENSAJES DE ERROR SE TRADUCEN IGUAL QUE EN LEGACY, porque son los que la
  gente de mostrador reconoce:

    tax_system        -> "EL REGIMEN FISCAL ES INCORRECTO."
    NOT AUTHENTICATED -> "KEY PARA FACTURAR NO COINCIDE."
    CUSTOMER.EMAIL    -> "EL CORREO ELECTRONICO ES INCORRECTO."
    cualquier otro    -> "NO SE PUDO GENERAR LA FACTURA." + el texto de Facturapi

  NO SE REINTENTA NUNCA. Un timbrado que no se sabe si llego no se repite: el
  reintento emitiria un segundo CFDI real ante el SAT. Si la llamada falla, falla,
  y quien atiende revisa en Facturapi antes de volver a intentarlo.
*/
public class FacturapiCliente
{
    /// <summary>
    /// La base de la API. Es configurable para poder apuntar a un entorno de
    /// pruebas sin tocar codigo, pero por defecto es la de produccion, que es la
    /// unica que usa legacy.
    /// </summary>
    private readonly string _baseUrl;

    private readonly IHttpClientFactory _httpClientFactory;

    public FacturapiCliente(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _baseUrl = configuration["Facturapi:BaseUrl"]?.TrimEnd('/') ?? "https://www.facturapi.io/v2";
    }

    /// <summary>El resultado de una llamada: o el comprobante, o el motivo.</summary>
    public sealed record Respuesta(FacturaTimbradaDto? Comprobante, string? Error)
    {
        public bool Ok => Error is null;
    }

    /// <summary>
    /// Timbra una factura o nota de credito.
    /// `tipoCfdi` es el del SAT: null para ingreso (factura), "E" para egreso
    /// (nota de credito), "P" para pago.
    /// </summary>
    public async Task<Respuesta> TimbrarAsync(
        string secreto,
        string? tipoCfdi,
        string serie,
        int folio,
        FacturaTimbrarRequest request,
        CancellationToken ct)
    {
        var cuerpo = new Dictionary<string, object?>
        {
            ["customer"] = Receptor(request.Receptor),
            ["items"] = request.Conceptos.Select(Concepto).ToList(),
            ["payment_form"] = ReglasFacturacion.FormaPagoSat(request.FormaPago),
            ["payment_method"] = ReglasFacturacion.ClaveSat(request.MetodoPago),
            ["use"] = ReglasFacturacion.ClaveSat(request.UsoCfdi)
        };

        if (!string.IsNullOrWhiteSpace(request.Observaciones))
            cuerpo["pdf_custom_section"] = request.Observaciones;

        if (tipoCfdi is null)
        {
            /*
              SOLO LA FACTURA MANDA SERIE Y FOLIO.

              En la nota de credito legacy deja el folio comentado y en el
              complemento no manda ninguno de los dos: en esos dos casos la
              numeracion la pone Facturapi. Mandarlos cambiaria la numeracion
              fiscal de documentos que ya existen con otra.
            */
            cuerpo["series"] = serie;
            cuerpo["folio_number"] = folio;
        }
        else
        {
            cuerpo["type"] = tipoCfdi;
            if (tipoCfdi == "E")
            {
                cuerpo["series"] = serie;
                if (!string.IsNullOrWhiteSpace(request.RelacionFactura))
                {
                    cuerpo["relation"] = ReglasFacturacion.ClaveSat(request.RelacionFactura);
                    if (!string.IsNullOrWhiteSpace(request.IdFacturapiRelacionada))
                        cuerpo["related"] = new[] { request.IdFacturapiRelacionada };
                }
            }
        }

        return await EnviarAsync(secreto, "invoices", cuerpo, ct);
    }

    /// <summary>Timbra un complemento de pago.</summary>
    public async Task<Respuesta> TimbrarComplementoAsync(
        string secreto,
        ComplementoPagoTimbrarRequest request,
        IReadOnlyList<(string Uuid, int Parcialidad, decimal SaldoAnterior)> relacionados,
        decimal porcentajeIva,
        CancellationToken ct)
    {
        var documentos = new List<Dictionary<string, object?>>();
        for (var i = 0; i < request.Documentos.Count && i < relacionados.Count; i++)
        {
            var importe = request.Documentos[i].Importe;
            var baseSinIva = porcentajeIva > 0 ? importe / (1 + porcentajeIva) : importe;

            documentos.Add(new Dictionary<string, object?>
            {
                ["uuid"] = relacionados[i].Uuid,
                ["amount"] = Numero(importe),
                ["installment"] = relacionados[i].Parcialidad,
                ["last_balance"] = Numero(relacionados[i].SaldoAnterior),
                ["taxes"] = new[]
                {
                    new Dictionary<string, object?>
                    {
                        ["base"] = Numero(baseSinIva),
                        ["type"] = "IVA",
                        ["rate"] = Numero(porcentajeIva)
                    }
                }
            });
        }

        var cuerpo = new Dictionary<string, object?>
        {
            ["type"] = "P",
            ["customer"] = Receptor(request.Receptor),
            ["complements"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["type"] = "pago",
                    ["data"] = new[]
                    {
                        new Dictionary<string, object?>
                        {
                            ["payment_form"] = ReglasFacturacion.FormaPagoSat(request.FormaPago),
                            ["date"] = ReglasFacturacion.FechaPagoSat(request.FechaPago),
                            ["related_documents"] = documentos
                        }
                    }
                }
            }
        };

        if (!string.IsNullOrWhiteSpace(request.Observaciones))
            cuerpo["pdf_custom_section"] = request.Observaciones;

        return await EnviarAsync(secreto, "invoices", cuerpo, ct);
    }

    /// <summary>Cancela un comprobante.</summary>
    public async Task<Respuesta> CancelarAsync(
        string secreto, string idFacturapi, string? motivoSat, string? uuidSustituto, CancellationToken ct)
    {
        var ruta = $"invoices/{Uri.EscapeDataString(idFacturapi)}?motive={Uri.EscapeDataString(motivoSat ?? string.Empty)}";
        if (!string.IsNullOrWhiteSpace(uuidSustituto))
            ruta += "&substitution=" + Uri.EscapeDataString(uuidSustituto);

        return await EnviarAsync(secreto, ruta, cuerpo: null, ct, HttpMethod.Delete);
    }

    /// <summary>Lee el estado actual de un comprobante en Facturapi.</summary>
    public async Task<Respuesta> ConsultarAsync(string secreto, string idFacturapi, CancellationToken ct)
        => await EnviarAsync(secreto, $"invoices/{Uri.EscapeDataString(idFacturapi)}", cuerpo: null, ct, HttpMethod.Get);

    /// <summary>El PDF o el XML del comprobante, tal como los guarda Facturapi.</summary>
    /// <remarks>
    /// POR QUE HACE FALTA: la ruta que da `sp_n_rptFacturaElectronica` es un disco
    /// de la OFICINA (`C:\macZ\Facturas\...`), donde Mac31 genera el archivo. La
    /// API corre en el servidor y ese disco no existe alla, asi que leerlo nunca
    /// podia funcionar en produccion. El comprobante bueno es el de Facturapi.
    ///
    /// Devuelve los bytes, o el motivo. No se mezcla con <see cref="Respuesta"/>
    /// porque esa trae un comprobante en JSON y aqui lo que vuelve es un archivo.
    /// </remarks>
    public async Task<(byte[]? Contenido, string? Error)> DescargarAsync(
        string secreto, string idFacturapi, string formato, CancellationToken ct)
    {
        var tipo = (formato ?? string.Empty).Trim().ToLowerInvariant();
        if (tipo is not ("pdf" or "xml"))
            return (null, "El formato tiene que ser pdf o xml.");

        var cliente = _httpClientFactory.CreateClient("facturapi");
        using var peticion = new HttpRequestMessage(
            HttpMethod.Get,
            $"{_baseUrl}/invoices/{Uri.EscapeDataString(idFacturapi)}/{tipo}");

        var credencial = Convert.ToBase64String(Encoding.UTF8.GetBytes(secreto + ":"));
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Basic", credencial);

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await cliente.SendAsync(peticion, ct);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return (null, "Facturapi no contesto a tiempo al pedir el " + tipo.ToUpperInvariant() + ".");
        }
        catch (HttpRequestException ex)
        {
            return (null, "No se pudo pedir el " + tipo.ToUpperInvariant() + " a Facturapi: " + ex.Message);
        }

        if (!respuesta.IsSuccessStatusCode)
        {
            var texto = await respuesta.Content.ReadAsStringAsync(ct);
            return (null, Traducir(texto, respuesta.StatusCode));
        }

        return (await respuesta.Content.ReadAsByteArrayAsync(ct), null);
    }

    /* ---------------------------------------------------------------- */

    private async Task<Respuesta> EnviarAsync(
        string secreto, string ruta, Dictionary<string, object?>? cuerpo, CancellationToken ct,
        HttpMethod? metodo = null)
    {
        var cliente = _httpClientFactory.CreateClient("facturapi");
        using var peticion = new HttpRequestMessage(metodo ?? HttpMethod.Post, $"{_baseUrl}/{ruta}");

        /*
          AUTENTICACION BASIC CON EL SECRETO COMO USUARIO Y CONTRASENA VACIA.
          Es como lo hace el SDK y como lo documenta Facturapi.
        */
        var credencial = Convert.ToBase64String(Encoding.UTF8.GetBytes(secreto + ":"));
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Basic", credencial);

        if (cuerpo is not null)
        {
            var json = JsonSerializer.Serialize(cuerpo);
            peticion.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        HttpResponseMessage respuesta;
        try
        {
            respuesta = await cliente.SendAsync(peticion, ct);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            /*
              UN TIMEOUT NO SE REINTENTA Y SE DICE CLARO.

              Puede que el CFDI se haya emitido y la respuesta se perdiera. Quien
              atiende tiene que mirar en Facturapi antes de volver a intentar, y el
              mensaje se lo dice.
            */
            return new Respuesta(null, "NO SE PUDO GENERAR LA FACTURA." + Environment.NewLine + Environment.NewLine
                + "La peticion a Facturapi no contesto a tiempo. REVISA EN FACTURAPI si el comprobante se emitio ANTES de volver a intentarlo.");
        }
        catch (HttpRequestException ex)
        {
            return new Respuesta(null, "NO SE PUDO GENERAR LA FACTURA." + Environment.NewLine + Environment.NewLine + ex.Message);
        }

        var texto = await respuesta.Content.ReadAsStringAsync(ct);

        if (!respuesta.IsSuccessStatusCode)
            return new Respuesta(null, Traducir(texto, respuesta.StatusCode));

        try
        {
            return new Respuesta(Leer(texto), null);
        }
        catch (JsonException ex)
        {
            return new Respuesta(null, "NO SE PUDO GENERAR LA FACTURA." + Environment.NewLine + Environment.NewLine
                + "Facturapi contesto algo que no se pudo leer: " + ex.Message);
        }
    }

    /// <summary>
    /// Los tres mensajes que legacy traduce, con su texto literal, porque son los
    /// que la gente de mostrador reconoce y busca en sus notas.
    /// </summary>
    public static string Traducir(string? respuestaFacturapi, System.Net.HttpStatusCode status)
    {
        var texto = respuestaFacturapi ?? string.Empty;
        var mayusculas = texto.ToUpperInvariant();

        if (status == System.Net.HttpStatusCode.Unauthorized || mayusculas.Contains("NOT AUTHENTICATED"))
            return ReglasFacturacion.MensajeKeyNoCoincide;

        if (texto.Contains("tax_system", StringComparison.OrdinalIgnoreCase))
            return "EL REGIMEN FISCAL ES INCORRECTO.";

        if (mayusculas.Contains("CUSTOMER.EMAIL"))
            return "EL CORREO ELECTRONICO ES INCORRECTO.";

        return "NO SE PUDO GENERAR LA FACTURA." + Environment.NewLine + Environment.NewLine + texto;
    }

    private static FacturaTimbradaDto Leer(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var raiz = doc.RootElement;

        var dto = new FacturaTimbradaDto
        {
            IdFacturapi = Cadena(raiz, "id"),
            Uuid = Cadena(raiz, "uuid"),
            Serie = Cadena(raiz, "series"),
            Status = Cadena(raiz, "status"),
            CancellationStatus = Cadena(raiz, "cancellation_status"),
            UrlVerificacion = Cadena(raiz, "verification_url"),
            Total = Decimal(raiz, "total"),
            Folio = Entero(raiz, "folio_number"),
            FechaCreacion = Fecha(raiz, "created_at"),
            FechaComprobante = Fecha(raiz, "date")
        };

        return dto;
    }

    private static Dictionary<string, object?> Receptor(FacturaReceptor receptor) => new()
    {
        ["legal_name"] = receptor.RazonSocial ?? string.Empty,
        ["tax_id"] = receptor.Rfc ?? string.Empty,
        ["tax_system"] = ReglasFacturacion.ClaveSat(receptor.RegimenFiscal),
        ["email"] = receptor.Correo ?? string.Empty,
        ["address"] = new Dictionary<string, object?>
        {
            ["zip"] = receptor.CodigoPostal ?? string.Empty,
            ["street"] = receptor.Calle ?? string.Empty,
            ["exterior"] = receptor.NumeroExterior ?? string.Empty,
            ["interior"] = receptor.NumeroInterior ?? string.Empty,
            ["neighborhood"] = receptor.Colonia ?? string.Empty,
            ["city"] = receptor.Ciudad ?? string.Empty,
            ["municipality"] = receptor.Municipio ?? string.Empty,
            ["state"] = receptor.Estado ?? string.Empty,
            ["country"] = string.IsNullOrWhiteSpace(receptor.Pais) ? "MEX" : receptor.Pais
        }
    };

    private static Dictionary<string, object?> Concepto(FacturaConcepto concepto) => new()
    {
        ["quantity"] = Numero(concepto.Cantidad),
        ["discount"] = Numero(concepto.Descuento),
        ["product"] = new Dictionary<string, object?>
        {
            ["description"] = concepto.Descripcion ?? string.Empty,
            ["product_key"] = concepto.ClaveProdServ ?? string.Empty,
            ["unit_key"] = concepto.ClaveUnidad ?? string.Empty,
            /*
              EL PRECIO VA CON IVA INCLUIDO, Y HAY QUE DECIRSELO.

              Mac31 maneja los precios con IVA dentro. Si no se manda
              `price_includes_tax`, Facturapi le suma el IVA otra vez y el total
              del CFDI sale un 16% mas alto que el de la remision.
            */
            ["price"] = Numero(concepto.PrecioUnitarioConIva),
            ["price_includes_tax"] = true
        }
    };

    private static decimal Numero(decimal valor) => decimal.Round(valor, 6, MidpointRounding.AwayFromZero);

    private static string? Cadena(JsonElement raiz, string nombre)
        => raiz.TryGetProperty(nombre, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static decimal Decimal(JsonElement raiz, string nombre)
        => raiz.TryGetProperty(nombre, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : 0m;

    private static int Entero(JsonElement raiz, string nombre)
    {
        if (!raiz.TryGetProperty(nombre, out var v)) return 0;
        return v.ValueKind switch
        {
            JsonValueKind.Number => v.GetInt32(),
            JsonValueKind.String => int.TryParse(v.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) ? n : 0,
            _ => 0
        };
    }

    private static DateTime? Fecha(JsonElement raiz, string nombre)
    {
        if (!raiz.TryGetProperty(nombre, out var v) || v.ValueKind != JsonValueKind.String) return null;
        return DateTime.TryParse(v.GetString(), CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var fecha)
            ? fecha.ToLocalTime()
            : null;
    }
}
