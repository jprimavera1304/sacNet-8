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
        static Mutex _instanciaUnica;  // impide que se abran dos conectores
        static readonly List<Registro> _padron = new List<Registro>();
        static readonly object _candado = new object();
        static readonly object _candadoLog = new object();
        static readonly object _candadoLector = new object();   // una captura a la vez

        // ---------------------------------------------------------------- log
        //
        // Cuando "no pasa nada" al poner el dedo hay cuatro sospechosos y desde
        // fuera se ven igual: no llego la peticion, el lector no arranco la
        // captura, el dedo no se leyo, o se leyo y no se reconocio. El log
        // separa esos cuatro casos; sin el, solo queda adivinar.
        //
        // Se escribe junto al programa y se abre y cierra en cada linea, para
        // poder leerlo mientras el conector sigue corriendo.
        /// <summary>
        /// Carpeta del conector.
        ///
        /// No se puede dar por hecho que sea la del ejecutable: en desarrollo el
        /// conector se compila EN MEMORIA dentro de PowerShell, y entonces
        /// Application.ExecutablePath apunta a la carpeta de PowerShell. Eso ya
        /// habia hecho que el conector ignorara conector.config en silencio y se
        /// fuera a la cadena de reserva, que es de las cosas mas dificiles de
        /// notar: funciona, pero contra la base equivocada.
        ///
        /// Se busca la carpeta que REALMENTE tenga la configuracion.
        /// </summary>
        static string CarpetaBase()
        {
            foreach (var candidata in new[]
                     {
                         Environment.GetEnvironmentVariable("MAC_CONECTOR_DIR"),
                         Path.GetDirectoryName(Application.ExecutablePath),
                         Directory.GetCurrentDirectory()
                     })
            {
                if (string.IsNullOrEmpty(candidata)) continue;
                if (File.Exists(Path.Combine(candidata, "conector.config")) ||
                    File.Exists(Path.Combine(candidata, "conector.config.ejemplo")))
                    return candidata;
            }
            return Directory.GetCurrentDirectory();
        }

        static void Log(string mensaje)
        {
            try
            {
                string ruta = Path.Combine(CarpetaBase(), "conector.log");
                string linea = DateTime.Now.ToString("HH:mm:ss.fff") + "  " + mensaje;
                lock (_candadoLog) File.AppendAllText(ruta, linea + Environment.NewLine);
                Console.WriteLine(linea);
            }
            catch { /* el log nunca debe tumbar al conector */ }
        }

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
        /// <summary>
        /// Deja el lector en READY antes de pedirle otra captura.
        ///
        /// El SDK NO se libera solo: despues de entregar una huella el lector se
        /// queda BUSY, y toda captura siguiente devuelve DP_DEVICE_BUSY al
        /// instante. Visto desde la pantalla eso parece "no se puso el dedo a
        /// tiempo", aunque conteste en milisegundos y el dedo ya estuviera
        /// puesto. El sintoma tipico es que el conector sirve UNA sola vez por
        /// arranque.
        ///
        /// El sondeo se hace desde el hilo del servidor y cada paso entra y sale
        /// de la bomba de mensajes, para no dejarla bloqueada: si se congela, el
        /// SDK no puede entregar nada y el lector no volveria a estar listo
        /// nunca.
        /// </summary>
        static bool EsperarLectorListo(int intentos = 20)
        {
            for (int i = 0; i < intentos; i++)
            {
                bool listo = false;

                _bomba.Invoke((MethodInvoker)delegate
                {
                    if (_lector.GetStatus() == Constants.ResultCode.DP_SUCCESS &&
                        _lector.Status != null &&
                        _lector.Status.Status == Constants.ReaderStatuses.DP_STATUS_READY)
                    {
                        listo = true;
                        return;
                    }
                    try { _lector.CancelCapture(); } catch { }
                });

                if (listo)
                {
                    if (i > 0) Log("    lector liberado tras " + (i * 100) + " ms.");
                    return true;
                }

                Thread.Sleep(100);
            }

            Log("    AVISO: el lector sigue ocupado despues de 2 s.");
            return false;
        }

        static CaptureResult Capturar(int segundos)
        {
            CaptureResult resultado = null;
            var listo = new ManualResetEventSlim(false);

            EsperarLectorListo();

            Reader.CaptureCallback alCapturar = delegate (CaptureResult cr)
            {
                // Ojo: que llegue el evento NO quiere decir que la huella sirva.
                // El lector avisa igual cuando el dedo salio mal puesto o muy
                // seco; por eso se registra el resultado y no solo "llego".
                Log("    <- el lector entrego una captura: resultado=" +
                    (cr == null ? "null" : cr.ResultCode.ToString()) +
                    ", calidad=" + (cr == null ? "-" : cr.Quality.ToString()) +
                    ", bytes=" + (cr == null || cr.Data == null ? "0" : cr.Data.Bytes.Length.ToString()));
                resultado = cr;
                listo.Set();
            };

            _bomba.Invoke((MethodInvoker)delegate
            {
                _lector.On_Captured += alCapturar;

                var estado = _lector.GetStatus();
                Log("    lector: consulta=" + estado + ", estado=" +
                    (_lector.Status == null ? "?" : _lector.Status.Status.ToString()));

                if (estado != Constants.ResultCode.DP_SUCCESS)
                {
                    Log("    ABORTA: no se pudo consultar el lector.");
                    listo.Set();
                    return;
                }

                var rc = _lector.CaptureAsync(Constants.Formats.Fid.ANSI,
                                              Constants.CaptureProcessing.DP_IMG_PROC_DEFAULT,
                                              _lector.Capabilities.Resolutions[0]);
                Log("    CaptureAsync -> " + rc + (rc == Constants.ResultCode.DP_SUCCESS
                    ? "  (esperando el dedo...)" : "  ABORTA"));
                if (rc != Constants.ResultCode.DP_SUCCESS) listo.Set();
            });

            bool llego = listo.Wait(segundos * 1000);
            if (!llego) Log("    se acabo el tiempo (" + segundos + "s) sin que el lector entregara nada.");

            _bomba.Invoke((MethodInvoker)delegate
            {
                _lector.On_Captured -= alCapturar;

                // SIEMPRE, tambien cuando la captura salio bien. Si solo se
                // cancelara al agotarse el tiempo, el lector se quedaria ocupado
                // despues de cada huella buena y el conector serviria una sola
                // vez por arranque.
                try { _lector.CancelCapture(); } catch { }
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

            // Corta la espera que este en curso. La pagina la usa antes de dar de
            // alta una huella: el lector es uno solo, y sin esto habria que
            // aguantar hasta 15 segundos a que se venciera la escucha, que desde
            // fuera se ve como que el boton no hace nada.
            if (ruta == "/cancelar")
            {
                Log("PETICION /cancelar");
                try { _bomba.Invoke((MethodInvoker)delegate { _lector.CancelCapture(); }); } catch { }
                Responder(ctx, 200, "{\"ok\":true}");
                return;
            }

            if (ruta == "/escanear" || ruta == "/capturar")
            {
                Log("PETICION " + ruta + "  (la pagina si llego hasta aqui)");

                if (_lector == null)
                {
                    Log("    ABORTA: el conector arranco sin lector.");
                    Responder(ctx, 200, "{\"ok\":false,\"motivo\":\"No hay lector conectado.\"}"); return;
                }

                // El lector es uno: dos capturas a la vez se pisan. La que llega
                // despues espera un poco y, si sigue ocupado, lo dice claro en vez
                // de quedarse colgada.
                if (!Monitor.TryEnter(_candadoLector, TimeSpan.FromSeconds(20)))
                {
                    Log("    ABORTA: el lector esta ocupado con otra captura.");
                    Responder(ctx, 200, "{\"ok\":false,\"motivo\":\"El lector está ocupado.\"}"); return;
                }

                try
                {
                    AtenderCaptura(ctx, ruta);
                }
                finally { Monitor.Exit(_candadoLector); }
                return;
            }

            Responder(ctx, 404, "{\"ok\":false}");
        }

        /// <summary>
        /// Traduce por que salio mal una lectura, en palabras que le sirvan a
        /// quien esta parado frente al lector.
        ///
        /// Importa distinguirlo: no es lo mismo que nadie haya puesto el dedo a
        /// que si lo haya puesto y la lectura saliera mal. Decir "no se puso el
        /// dedo a tiempo" en el segundo caso hace que la persona se quede
        /// esperando, cuando lo que tiene que hacer es volver a intentar.
        /// </summary>
        static string PorQueFallo(Constants.CaptureQuality calidad)
        {
            switch (calidad)
            {
                // El dedo quedo fuera de lugar. Es lo mas comun de lejos, y se
                // resuelve solo con decir donde ponerlo.
                case Constants.CaptureQuality.DP_QUALITY_FINGER_TOO_LEFT:
                case Constants.CaptureQuality.DP_QUALITY_FINGER_TOO_RIGHT:
                case Constants.CaptureQuality.DP_QUALITY_FINGER_TOO_HIGH:
                case Constants.CaptureQuality.DP_QUALITY_FINGER_TOO_LOW:
                case Constants.CaptureQuality.DP_QUALITY_FINGER_OFF_CENTER:
                case Constants.CaptureQuality.DP_QUALITY_SCAN_SKEWED:
                    return "El dedo quedo corrido. Ponlo plano y al centro del lector.";

                // Lo levanto antes de tiempo o lo movio.
                case Constants.CaptureQuality.DP_QUALITY_SCAN_TOO_SHORT:
                case Constants.CaptureQuality.DP_QUALITY_SCAN_TOO_FAST:
                    return "Se levanto el dedo muy rapido. Dejalo puesto un momento.";
                case Constants.CaptureQuality.DP_QUALITY_SCAN_TOO_LONG:
                case Constants.CaptureQuality.DP_QUALITY_SCAN_TOO_SLOW:
                case Constants.CaptureQuality.DP_QUALITY_SCAN_WRONG_DIRECTION:
                    return "No se leyo bien. Pon el dedo y mantenlo quieto.";

                case Constants.CaptureQuality.DP_QUALITY_CANCELED:
                    return "Se corto la lectura. Vuelve a poner el dedo.";
                case Constants.CaptureQuality.DP_QUALITY_FAKE_FINGER:
                    return "No se reconocio como un dedo. Vuelve a intentar.";

                // Estas dos no las arregla la persona: son del aparato, y por eso
                // se dicen distinto, para que avise en vez de seguir insistiendo.
                case Constants.CaptureQuality.DP_QUALITY_READER_DIRTY:
                    return "El lector esta sucio. Limpialo con un paño seco y vuelve a intentar.";
                case Constants.CaptureQuality.DP_QUALITY_READER_FAILED:
                    return "El lector fallo. Desconectalo y vuelvelo a conectar, o avisa al administrador.";

                default:
                    return "No se leyo bien la huella. Vuelve a poner el dedo.";
            }
        }

        static void AtenderCaptura(HttpListenerContext ctx, string ruta)
        {
            {
                var cap = Capturar(LeerSegundos(ctx));

                // Nadie puso el dedo. El SDK lo reporta de tres formas y las tres
                // son lo mismo: sin entregar nada, con la captura marcada como
                // vencida, o -lo que hace en la practica al vencerse- con calidad
                // NO_FINGER. Medido: al agotarse una espera de 5 s sin nadie
                // enfrente, contesta NO_FINGER.
                //
                // Hay que distinguirlo o la pantalla estaria mostrando "vuelve a
                // poner el dedo" cada 15 segundos sin nadie enfrente, que ademas
                // de inutil le quita sentido al aviso cuando SI hace falta.
                if (cap != null &&
                    (cap.Quality == Constants.CaptureQuality.DP_QUALITY_TIMED_OUT ||
                     cap.Quality == Constants.CaptureQuality.DP_QUALITY_NO_FINGER))
                    cap = null;

                if (cap == null)
                {
                    Log("    RESULTADO: nadie puso el dedo.");
                    Responder(ctx, 200, "{\"ok\":false,\"agotado\":true,\"motivo\":\"No se puso el dedo a tiempo.\"}"); return;
                }

                // Si puso el dedo pero salio mal, se dice QUE hacer. Con
                // reintentar=true la pantalla lo muestra como aviso para volver a
                // intentar, no como si no hubiera pasado nada.
                if (cap.ResultCode != Constants.ResultCode.DP_SUCCESS || cap.Data == null)
                {
                    string porque = PorQueFallo(cap.Quality);
                    Log("    RESULTADO: lectura fallida (" + cap.ResultCode + " / " + cap.Quality + ") -> " + porque);
                    Responder(ctx, 200,
                        "{\"ok\":false,\"reintentar\":true,\"calidad\":\"" + Esc(cap.Quality.ToString()) +
                        "\",\"motivo\":\"" + Esc(porque) + "\"}");
                    return;
                }

                if (ruta == "/capturar")
                {
                    Log("    RESULTADO: huella capturada para el alta (" + cap.Data.Bytes.Length + " bytes). Se manda a la pagina.");
                    // Alta: se devuelve la imagen tal cual, en el mismo formato
                    // que ya usa legacy, para que el checador viejo la reconozca.
                    Responder(ctx, 200,
                        "{\"ok\":true,\"equipo\":\"" + Esc(Environment.MachineName) +
                        "\",\"huellaBase64\":\"" + Convert.ToBase64String(cap.Data.Bytes) + "\"}");
                    return;
                }

                var f = FeatureExtraction.CreateFmdFromFid(cap.Data, Constants.Formats.Fmd.ANSI);
                if (f.ResultCode != Constants.ResultCode.DP_SUCCESS)
                {
                    Log("    RESULTADO: la huella se leyo pero no se pudo procesar (" + f.ResultCode + ").");
                    Responder(ctx, 200,
                        "{\"ok\":false,\"reintentar\":true," +
                        "\"motivo\":\"La huella se leyo incompleta. Ponla plana y vuelve a intentar.\"}");
                    return;
                }

                Registro mejor = null; int mejorScore = int.MaxValue;
                int mejorDeTodos = int.MaxValue;   // el mas parecido aunque no pase el umbral
                var reloj = Stopwatch.StartNew();
                lock (_candado)
                    foreach (var reg in _padron)
                    {
                        var cr = Comparison.Compare(reg.Template, 0, f.Data, 0);
                        if (cr.ResultCode != Constants.ResultCode.DP_SUCCESS) continue;
                        if (cr.Score < mejorDeTodos) mejorDeTodos = cr.Score;
                        if (cr.Score < UMBRAL && cr.Score < mejorScore)
                        { mejorScore = cr.Score; mejor = reg; }
                    }
                reloj.Stop();

                if (mejor == null)
                {
                    // Se registra el mejor parecido aunque no alcance: distingue
                    // "esa huella no esta registrada" de "si esta pero el dedo
                    // salio mal puesto y quedo apenas arriba del umbral".
                    Log("    RESULTADO: no se reconocio. Comparadas " + _padron.Count +
                        " huellas en " + reloj.ElapsedMilliseconds + " ms. Mejor parecido=" +
                        mejorDeTodos + " (hace falta menos de " + UMBRAL + ").");
                    Responder(ctx, 200, "{\"ok\":true,\"empleado\":null,\"ms\":" + reloj.ElapsedMilliseconds + "}"); return;
                }

                Log("    RESULTADO: reconocido -> " + mejor.Empleado + " (empleado " + mejor.IdEmpleado +
                    "), score=" + mejorScore + ", en " + reloj.ElapsedMilliseconds + " ms.");

                Responder(ctx, 200,
                    "{\"ok\":true,\"ms\":" + reloj.ElapsedMilliseconds +
                    ",\"equipo\":\"" + Esc(Environment.MachineName) + "\"" +
                    ",\"empleado\":{\"idEmpleado\":" + mejor.IdEmpleado +
                    ",\"nombre\":\"" + Esc(mejor.Empleado) + "\"}" +
                    ",\"huella\":{\"idEmpleadoHuella\":" + mejor.IdHuella +
                    ",\"idMano\":" + mejor.Mano + ",\"idDedo\":" + mejor.Dedo + "}}");
            }
        }

        /// <summary>
        /// Cada peticion se atiende en su propio hilo.
        ///
        /// Antes se atendian de una en una, y con la pagina escuchando al lector
        /// todo el tiempo eso dejaba al conector mudo: mientras esperaba el dedo
        /// (15 s) no podia contestar ni /estado, asi que la pantalla creia que el
        /// conector no estaba instalado y se apagaba sola. Lo mismo pasaba con el
        /// alta de huellas.
        ///
        /// La captura si esta serializada (ver el candado en Atender): el lector
        /// es uno solo. Lo que se gana aqui es que las consultas que NO tocan el
        /// lector se contesten al momento, aunque haya una espera en curso.
        /// </summary>
        static void Servir(HttpListener oyente)
        {
            while (oyente.IsListening)
            {
                HttpListenerContext ctx;
                try { ctx = oyente.GetContext(); }
                catch { return; }   // se cerro el oyente

                ThreadPool.QueueUserWorkItem(delegate
                {
                    try { Atender(ctx); }
                    catch (Exception e)
                    {
                        try { Responder(ctx, 500, "{\"ok\":false,\"motivo\":\"" + Esc(e.Message) + "\"}"); } catch { }
                    }
                });
            }
        }

        // ------------------------------------------------------------- arranque

        static string LeerCadenaConexion()
        {
            // En archivo junto al programa, para no recompilar por equipo.
            string ruta = Path.Combine(CarpetaBase(), "conector.config");
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

            // UNA SOLA INSTANCIA.
            //
            // Dos conectores abiertos a la vez rompen el lector de una forma que
            // no se ve: el segundo se queda con el lector, el primero sigue
            // atendiendo la pagina, y el resultado es que todo se ve bien
            // (dice "lector listo") pero la huella nunca llega. Es facilisimo
            // llegar ahi: basta con darle dos veces al icono.
            //
            // El candado es del sistema, asi que sirve aunque el otro conector
            // lo haya abierto otra sesion de Windows.
            bool esElPrimero;
            _instanciaUnica = new Mutex(true, @"Global\MacChecadorConector", out esElPrimero);
            if (!esElPrimero)
            {
                Console.WriteLine("Ya hay un conector abierto en esta computadora.");
                Console.WriteLine("Usa ese; no hace falta abrir otro.");
                Console.WriteLine("\nPresiona una tecla para cerrar.");
                Console.ReadKey();
                return 1;
            }

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

            // El PUERTO se toma antes que el lector, a proposito.
            //
            // Al reves, un segundo conector alcanzaria a quedarse con el lector y
            // solo despues descubriria que el puerto esta ocupado: para entonces
            // ya dejo ciego al conector bueno. Tomando primero el puerto, el que
            // sobra se sale sin haber tocado el lector.
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

            var lectores = ReaderCollection.GetReaders();
            Log("Lectores encontrados: " + lectores.Count);
            if (lectores.Count == 0) Log("AVISO: no hay lector conectado.");
            else
            {
                _lector = lectores[0];
                Log("Usando: " + _lector.Description.Name);

                // EXCLUSIVE, no COOPERATIVE.
                //
                // En cooperativo, otro programa que tambien tenga el lector
                // abierto (el checador viejo, el software del fabricante, o un
                // segundo conector) se lleva las capturas, y el sintoma es que
                // el lector prende, el dedo se lee, y aqui no llega nada. En
                // exclusivo, si alguien mas lo tiene, falla AQUI y se ve en el
                // log, en vez de fallar en silencio cada vez que alguien checa.
                var rc = _lector.Open(Constants.CapturePriority.DP_PRIORITY_EXCLUSIVE);
                Log("Open(EXCLUSIVE) -> " + rc);

                if (rc != Constants.ResultCode.DP_SUCCESS)
                {
                    Log("No se pudo tomar el lector en exclusiva; se intenta compartido.");
                    rc = _lector.Open(Constants.CapturePriority.DP_PRIORITY_COOPERATIVE);
                    Log("Open(COOPERATIVE) -> " + rc);
                }

                if (rc != Constants.ResultCode.DP_SUCCESS)
                { Log("No se pudo abrir el lector. Cierra el checador viejo y vuelve a intentar."); _lector = null; }
                else Log("Lector listo.");
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
