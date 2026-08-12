// Conector del lector de huellas del checador.
//
// QUE ES
// ------
// Un programa chico que se instala en la computadora que tiene el lector USB y
// que escucha en 127.0.0.1. La pagina web le pide la huella a el, porque un
// navegador no le puede hablar a un lector USB. Sin este programa, el modulo
// del checador se ve pero no puede checar.
//
// DOS COSAS QUE NO SON OBVIAS Y SON LAS QUE HACEN QUE ESTO FUNCIONE
// -----------------------------------------------------------------
// 1) EL SDK ENTREGA LA HUELLA POR MENSAJES DE WINDOWS. CaptureAsync no llama al
//    codigo directo: manda la captura por la cola de mensajes del hilo. Un
//    programa sin bucle de mensajes NUNCA recibe el evento: el usuario pone el
//    dedo, el lector parpadea y no pasa nada. Por eso el hilo principal corre un
//    Application.Run con una ventana invisible y el servidor HTTP vive aparte.
//    (Se descubrio peleandose con esto: la primera version se quedaba esperando
//    para siempre aunque el lector si capturaba.)
//
// 2) HAY QUE PRECALCULAR LOS TEMPLATES. En la base las huellas se guardan como
//    imagen cruda; el template (FMD) se arma aparte y ESO es lo caro. Medido con
//    las 831 huellas reales: reconstruirlas tarda 37.7 s y compararlas 0.27 s.
//    El checador viejo las reconstruye DENTRO del ciclo en cada escaneo, y por
//    eso identificar tarda ~38 segundos. Aqui se hace una sola vez al arrancar y
//    cada dedo se resuelve en milisegundos.
//
// SEGURIDAD
// ---------
// Escucha SOLO en 127.0.0.1: no acepta nada de fuera de esta computadora.
// Ademas genera un token nuevo en cada arranque que la pagina debe mandar en
// toda operacion; /estado es lo unico abierto, para poder descubrirlo.
//
// OJO AL DISTRIBUIRLO
// -------------------
// Sin FIRMA DE CODIGO los antivirus lo bloquean: es un ejecutable desconocido
// que abre un puerto y lee una base, que es justo el perfil de un troyano.
// Verificado con McAfee, que impide ejecutarlo. Para instalarlo en varias
// maquinas hay que firmarlo con un certificado, o excluir su carpeta en cada
// una. No hay forma de escribirlo distinto para evitarlo.
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using DPUruNet;

namespace Mac.Checador.Conector
{
    internal static class Programa
    {
        const int PROBABILITY_ONE = 0x7FFFFFFF;

        // Mismo umbral que usa el checador viejo: coincide si la disimilitud
        // queda por debajo. Cambiarlo afecta a quien entra y quien no.
        static readonly int UMBRAL = PROBABILITY_ONE / 100000;

        // Vienen en la cabecera ISO de las huellas guardadas y son constantes.
        // El checador viejo los saca de la captura en vivo, lo que lo obliga a
        // tener un dedo puesto solo para poder leer lo que ya esta en la base.
        const int CBEFF_ID = 0x0033FEFF;
        const int RESOLUCION = 500;

        static readonly int[] PUERTOS = { 47800, 47801, 47802 };

        static string _token;
        static string _cadenaConexion;
        static Reader _lector;
        static Form _bomba;            // ventana invisible: solo bombea mensajes
        static readonly List<Registro> _padron = new List<Registro>();
        static readonly object _candado = new object();

        sealed class Registro
        {
            public int IdHuella, IdEmpleado, Mano, Dedo;
            public string Empleado;
            public Fmd Template;
        }

        // --------------------------------------------------------------- padron

        static Fmd AFmd(byte[] crudo)
        {
            var r = FeatureExtraction.CreateFmdFromRaw(
                crudo, 1, CBEFF_ID, 400, 500, RESOLUCION, Constants.Formats.Fmd.ISO);
            return r.ResultCode == Constants.ResultCode.DP_SUCCESS ? r.Data : null;
        }

        static int CargarPadron()
        {
            var nuevo = new List<Registro>();
            using (var cn = new SqlConnection(_cadenaConexion))
            {
                cn.Open();
                const string sql = @"
SELECT h.IDEmpleadoHuella, h.IDEmpleado, h.IDMano, h.IDDedo,
       ISNULL(e.Nombre,'') AS Empleado, h.Huella
FROM EmpleadosHuellas h
LEFT JOIN Empleados e ON e.IDEmpleado = h.IDEmpleado
WHERE h.Huella IS NOT NULL AND h.IDStatus = 1;";
                using (var cmd = new SqlCommand(sql, cn))
                using (var rd = cmd.ExecuteReader())
                    while (rd.Read())
                    {
                        var f = AFmd((byte[])rd["Huella"]);
                        if (f == null) continue;   // huella ilegible: se salta
                        nuevo.Add(new Registro
                        {
                            IdHuella = (int)rd["IDEmpleadoHuella"],
                            IdEmpleado = (int)rd["IDEmpleado"],
                            Mano = (int)rd["IDMano"],
                            Dedo = (int)rd["IDDedo"],
                            Empleado = Convert.ToString(rd["Empleado"]),
                            Template = f
                        });
                    }
            }
            lock (_candado) { _padron.Clear(); _padron.AddRange(nuevo); }
            return nuevo.Count;
        }

        // -------------------------------------------------------------- captura

        /// <summary>
        /// Pide una captura y espera. Se llama desde el hilo del servidor HTTP,
        /// pero la captura se arranca EN EL HILO DE LA BOMBA de mensajes; si no,
        /// el evento del SDK no llega nunca.
        /// </summary>
        static CaptureResult Capturar(int segundos)
        {
            CaptureResult resultado = null;
            var listo = new ManualResetEventSlim(false);

            Reader.CaptureCallback alCapturar = delegate (CaptureResult cr)
            {
                resultado = cr;
                listo.Set();
            };

            _bomba.Invoke((MethodInvoker)delegate
            {
                _lector.On_Captured += alCapturar;
                if (_lector.GetStatus() != Constants.ResultCode.DP_SUCCESS) { listo.Set(); return; }
                var rc = _lector.CaptureAsync(Constants.Formats.Fid.ANSI,
                                              Constants.CaptureProcessing.DP_IMG_PROC_DEFAULT,
                                              _lector.Capabilities.Resolutions[0]);
                if (rc != Constants.ResultCode.DP_SUCCESS) listo.Set();
            });

            bool llego = listo.Wait(segundos * 1000);

            _bomba.Invoke((MethodInvoker)delegate
            {
                _lector.On_Captured -= alCapturar;
                if (!llego) { try { _lector.CancelCapture(); } catch { } }
            });

            return llego ? resultado : null;
        }

        // ----------------------------------------------------------------- http

        static void Responder(HttpListenerContext ctx, int codigo, string json)
        {
            var b = Encoding.UTF8.GetBytes(json);
            ctx.Response.StatusCode = codigo;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            // La pagina vive en otro origen; sin CORS el navegador ni siquiera
            // deja leer la respuesta.
            ctx.Response.AddHeader("Access-Control-Allow-Origin", "*");
            ctx.Response.AddHeader("Access-Control-Allow-Headers", "X-Checador-Token, Content-Type");
            ctx.Response.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            ctx.Response.ContentLength64 = b.Length;
            ctx.Response.OutputStream.Write(b, 0, b.Length);
            ctx.Response.OutputStream.Close();
        }

        static string Esc(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ");
        }

        static bool TokenValido(HttpListenerContext ctx)
        {
            return string.Equals(ctx.Request.Headers["X-Checador-Token"], _token, StringComparison.Ordinal);
        }

        /// <summary>
        /// Saca un campo base64 del cuerpo JSON sin arrastrar una libreria de
        /// serializacion: el conector se compila solo con lo que trae el .NET
        /// Framework para que instalarlo sea copiar un archivo. El cuerpo lo
        /// arma nuestra propia pagina y solo trae base64, asi que basta con
        /// encontrar "campo":"...".
        /// </summary>
        static byte[] LeerCampoBase64(string json, string campo)
        {
            if (string.IsNullOrEmpty(json)) return null;

            string marca = "\"" + campo + "\"";
            int i = json.IndexOf(marca, StringComparison.Ordinal);
            if (i < 0) return null;

            i = json.IndexOf('"', json.IndexOf(':', i + marca.Length) + 1);
            if (i < 0) return null;
            int fin = json.IndexOf('"', i + 1);
            if (fin < 0) return null;

            try { return Convert.FromBase64String(json.Substring(i + 1, fin - i - 1)); }
            catch { return null; }
        }

        static int LeerSegundos(HttpListenerContext ctx)
        {
            int s;
            if (!int.TryParse(ctx.Request.QueryString["segundos"], out s)) s = 20;
            return Math.Max(5, Math.Min(60, s));
        }

        static void Atender(HttpListenerContext ctx)
        {
            string ruta = ctx.Request.Url.AbsolutePath.TrimEnd('/').ToLowerInvariant();

            if (ctx.Request.HttpMethod == "OPTIONS") { Responder(ctx, 204, ""); return; }

            if (ruta == "/estado")
            {
                bool hayLector = _lector != null;
                int cuantas; lock (_candado) cuantas = _padron.Count;
                Responder(ctx, 200,
                    "{\"version\":\"1.0.0\",\"lectorConectado\":" + (hayLector ? "true" : "false") +
                    ",\"huellasEnCache\":" + cuantas +
                    ",\"equipo\":\"" + Esc(Environment.MachineName) + "\"" +
                    ",\"token\":\"" + _token + "\"}");
                return;
            }

            if (!TokenValido(ctx)) { Responder(ctx, 401, "{\"ok\":false,\"motivo\":\"Token invalido.\"}"); return; }

            if (ruta == "/recargar")
            {
                Responder(ctx, 200, "{\"ok\":true,\"huellas\":" + CargarPadron() + "}");
                return;
            }

            // El alta pide el dedo DOS veces, porque asi guarda el sistema cada
            // dedo. Aqui se confirma que las dos capturas sean de la MISMA
            // huella antes de guardarlas: si la persona cambio de dedo entre una
            // y otra, el registro queda mal casado y despues falla al identificar
            // sin que nadie sepa por que. Vale mas pedirla de nuevo en el momento.
            if (ruta == "/comparar")
            {
                string cuerpo;
                using (var sr = new StreamReader(ctx.Request.InputStream, Encoding.UTF8))
                    cuerpo = sr.ReadToEnd();

                byte[] a = LeerCampoBase64(cuerpo, "a");
                byte[] b = LeerCampoBase64(cuerpo, "b");
                if (a == null || b == null)
                { Responder(ctx, 400, "{\"ok\":false,\"motivo\":\"Faltan las dos capturas.\"}"); return; }

                var fa = AFmd(a);
                var fb = AFmd(b);
                if (fa == null || fb == null)
                { Responder(ctx, 200, "{\"ok\":false,\"motivo\":\"No se pudo procesar alguna de las dos capturas.\"}"); return; }

                var comp = Comparison.Compare(fa, 0, fb, 0);
                if (comp.ResultCode != Constants.ResultCode.DP_SUCCESS)
                { Responder(ctx, 200, "{\"ok\":false,\"motivo\":\"No se pudieron comparar las capturas.\"}"); return; }

                bool mismoDedo = comp.Score < UMBRAL;
                Responder(ctx, 200,
                    "{\"ok\":true,\"mismoDedo\":" + (mismoDedo ? "true" : "false") +
                    ",\"score\":" + comp.Score + "}");
                return;
            }

            if (ruta == "/escanear" || ruta == "/capturar")
            {
                if (_lector == null)
                { Responder(ctx, 200, "{\"ok\":false,\"motivo\":\"No hay lector conectado.\"}"); return; }

                var cap = Capturar(LeerSegundos(ctx));
                if (cap == null || cap.ResultCode != Constants.ResultCode.DP_SUCCESS || cap.Data == null)
                { Responder(ctx, 200, "{\"ok\":false,\"motivo\":\"No se puso el dedo a tiempo.\"}"); return; }

                if (ruta == "/capturar")
                {
                    // Alta: se devuelve la imagen tal cual, en el mismo formato
                    // que ya usa legacy, para que el checador viejo la reconozca.
                    Responder(ctx, 200,
                        "{\"ok\":true,\"equipo\":\"" + Esc(Environment.MachineName) +
                        "\",\"huellaBase64\":\"" + Convert.ToBase64String(cap.Data.Bytes) + "\"}");
                    return;
                }

                var f = FeatureExtraction.CreateFmdFromFid(cap.Data, Constants.Formats.Fmd.ANSI);
                if (f.ResultCode != Constants.ResultCode.DP_SUCCESS)
                { Responder(ctx, 200, "{\"ok\":false,\"motivo\":\"No se pudo procesar la huella.\"}"); return; }

                Registro mejor = null; int mejorScore = int.MaxValue;
                var reloj = Stopwatch.StartNew();
                lock (_candado)
                    foreach (var reg in _padron)
                    {
                        var cr = Comparison.Compare(reg.Template, 0, f.Data, 0);
                        if (cr.ResultCode != Constants.ResultCode.DP_SUCCESS) continue;
                        if (cr.Score < UMBRAL && cr.Score < mejorScore)
                        { mejorScore = cr.Score; mejor = reg; }
                    }
                reloj.Stop();

                if (mejor == null)
                { Responder(ctx, 200, "{\"ok\":true,\"empleado\":null,\"ms\":" + reloj.ElapsedMilliseconds + "}"); return; }

                Responder(ctx, 200,
                    "{\"ok\":true,\"ms\":" + reloj.ElapsedMilliseconds +
                    ",\"equipo\":\"" + Esc(Environment.MachineName) + "\"" +
                    ",\"empleado\":{\"idEmpleado\":" + mejor.IdEmpleado +
                    ",\"nombre\":\"" + Esc(mejor.Empleado) + "\"}" +
                    ",\"huella\":{\"idEmpleadoHuella\":" + mejor.IdHuella +
                    ",\"idMano\":" + mejor.Mano + ",\"idDedo\":" + mejor.Dedo + "}}");
                return;
            }

            Responder(ctx, 404, "{\"ok\":false}");
        }

        static void Servir(HttpListener oyente)
        {
            while (oyente.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = oyente.GetContext(); }
                catch { return; }   // se cerro el oyente
                try { Atender(ctx); }
                catch (Exception e)
                {
                    try { Responder(ctx, 500, "{\"ok\":false,\"motivo\":\"" + Esc(e.Message) + "\"}"); } catch { }
                }
            }
        }

        // ------------------------------------------------------------- arranque

        static string LeerCadenaConexion()
        {
            // En archivo junto al .exe, para no recompilar por equipo.
            string ruta = Path.Combine(
                Path.GetDirectoryName(Application.ExecutablePath) ?? ".", "conector.config");
            if (File.Exists(ruta))
                foreach (var linea in File.ReadAllLines(ruta))
                {
                    var l = linea.Trim();
                    if (l.StartsWith("conexion=", StringComparison.OrdinalIgnoreCase))
                        return l.Substring("conexion=".Length).Trim();
                }

            return "Server=LAP-JUAND;Database=MacZ;Integrated Security=true;TrustServerCertificate=True;";
        }

        [STAThread]
        static int Main()
        {
            Console.Title = "Conector del checador";
            Console.WriteLine("=== Conector del checador ===\n");

            _token = Guid.NewGuid().ToString("N");
            _cadenaConexion = LeerCadenaConexion();

            try
            {
                Console.Write("Cargando huellas... ");
                Console.WriteLine(CargarPadron() + " listas.");
            }
            catch (Exception e)
            {
                Console.WriteLine("\nNo se pudo leer la base: " + e.Message);
                Console.WriteLine("Revisa 'conexion=' en conector.config.");
                Console.ReadKey();
                return 1;
            }

            var lectores = ReaderCollection.GetReaders();
            if (lectores.Count == 0) Console.WriteLine("AVISO: no hay lector conectado.");
            else
            {
                _lector = lectores[0];
                if (_lector.Open(Constants.CapturePriority.DP_PRIORITY_COOPERATIVE)
                    != Constants.ResultCode.DP_SUCCESS)
                { Console.WriteLine("No se pudo abrir el lector."); _lector = null; }
                else Console.WriteLine("Lector listo.");
            }

            HttpListener oyente = null;
            foreach (int puerto in PUERTOS)
                try
                {
                    var o = new HttpListener();
                    o.Prefixes.Add("http://127.0.0.1:" + puerto + "/");
                    o.Start();
                    oyente = o;
                    Console.WriteLine("Escuchando en http://127.0.0.1:" + puerto);
                    break;
                }
                catch { /* ocupado: se prueba el siguiente */ }

            if (oyente == null)
            {
                Console.WriteLine("Todos los puertos ocupados. Puede que ya haya otro conector abierto.");
                Console.ReadKey();
                return 1;
            }

            new Thread(() => Servir(oyente)) { IsBackground = true }.Start();
            Console.WriteLine("\nListo. Deja esta ventana abierta.\n");

            // La ventana invisible existe SOLO para que el SDK pueda entregar las
            // capturas. Ver la nota 1 de arriba.
            _bomba = new Form { ShowInTaskbar = false, WindowState = FormWindowState.Minimized };
            _bomba.Load += delegate { _bomba.Visible = false; };
            Application.Run(_bomba);

            try { oyente.Stop(); } catch { }
            try { if (_lector != null) _lector.Dispose(); } catch { }
            return 0;
        }
    }
}
