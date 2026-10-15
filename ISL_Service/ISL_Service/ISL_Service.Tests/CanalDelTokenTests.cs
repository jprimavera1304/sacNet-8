using System.Security.Claims;
using ISL_Service.Application.DTOs.Responses;
using ISL_Service.Application.DTOs.VentasPedidoCaptura;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Models;
using ISL_Service.Application.Security;
using ISL_Service.Controllers;
using ISL_Service.Domain.Entities;
using ISL_Service.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace ISL_Service.Tests;

/*
  EL CANAL DEL TOKEN, Y LAS DOS CLAVES QUE APLICABA FLUTTER.

  Lo que estas pruebas fijan es la regla que hace que el diseno no se pueda
  abusar: DECLARAR UN CANAL NUNCA DA PRIVILEGIOS, SOLO LOS QUITA.

    * canal movil -> cuesta `app_movil.acceso` (AccesoMovilMiddleware y
      PermissionAuthorizationHandler);
    * canal web   -> las claves `app_movil.*` no cuentan (CanalDeAcceso.Filtrar);
    * sin canal   -> como antes del cambio, para los tokens de 4 h que haya
      vivos durante el despliegue.

  Las pruebas de intrusion de verdad -token firmado, API a mano, 403 contra las
  dos bases- se corrieron el 2026-10-15 con el guion
  scratchpad/intrusion.py: 16 casos, 0 fallos en MacZ y en Produccion_svr.
  Aqui quedan las que no necesitan base, que son las que van a correr siempre.
*/
public class CanalDelTokenTests
{
    // ------------------------------------------------------------------
    // Como se resuelve el canal al iniciar sesion
    // ------------------------------------------------------------------

    [Theory]
    [InlineData("movil", null, Canal.Movil)]
    [InlineData("MOVIL", null, Canal.Movil)]
    [InlineData(" movil ", null, Canal.Movil)]
    [InlineData("web", null, Canal.Web)]
    [InlineData("", null, Canal.Web)]
    [InlineData(null, null, Canal.Web)]
    [InlineData("cualquier-cosa", null, Canal.Web)]
    // Lo que declara el cliente MANDA sobre la heuristica: un navegador que
    // dijera "movil" entra al canal movil (y le cuesta la llave maestra).
    [InlineData("movil", "Mozilla/5.0 (Windows NT 10.0) Chrome/130", Canal.Movil)]
    [InlineData("web", "Dart/3.5 (dart:io)", Canal.Web)]
    // La app ya instalada NO manda `canal`: la delata su User-Agent, que es un
    // dato que ve el servidor. Forjarlo no regala nada -mete en el canal que
    // CUESTA la llave maestra-, y por eso puede ser una heuristica.
    [InlineData(null, "Dart/3.5 (dart:io)", Canal.Movil)]
    [InlineData(null, "Flutter/1.0", Canal.Movil)]
    // Un navegador sin `canal` es web, como siempre.
    [InlineData(null, "Mozilla/5.0 (Windows NT 10.0) Chrome/130", Canal.Web)]
    public void El_Canal_Se_Normaliza_En_El_Servidor(string? declarado, string? agente, Canal esperado)
        => Assert.Equal(esperado, CanalDeAcceso.Normalizar(declarado, agenteDeUsuario: agente));

    // ------------------------------------------------------------------
    // X-Cliente-App: el identificador propio de la app
    // ------------------------------------------------------------------

    /// <summary>
    /// La senal buena. La app se identifica y el User-Agent deja de hacer
    /// falta: con cabecera reconocida, el canal sale de ahi aunque el
    /// User-Agent sea el de un navegador.
    /// </summary>
    [Theory]
    [InlineData("mac-movil", Canal.Movil)]
    [InlineData("MAC-MOVIL", Canal.Movil)]
    [InlineData("mac-movil/1.4.2", Canal.Movil)]
    [InlineData("mac-movil/1.4.2 (android 14)", Canal.Movil)]
    [InlineData("mac-mac-mobile", Canal.Movil)]
    [InlineData("mac-web", Canal.Web)]
    [InlineData("qr-web/2.0", Canal.Web)]
    public void El_Identificador_De_Cliente_Dice_El_Canal(string clienteApp, Canal esperado)
        => Assert.Equal(esperado, CanalDeAcceso.DeClienteApp(clienteApp));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("curl/8.0")]
    [InlineData("inventado")]
    [InlineData("/1.0")]
    public void Un_Identificador_Que_No_Se_Reconoce_No_Decide_Nada(string? clienteApp)
        => Assert.Null(CanalDeAcceso.DeClienteApp(clienteApp));

    /// <summary>
    /// Un valor absurdamente largo no se interpreta: se trata como desconocido
    /// y decide el respaldo.
    /// </summary>
    [Fact]
    public void Un_Identificador_Enorme_No_Decide_Nada()
        => Assert.Null(CanalDeAcceso.DeClienteApp(new string('x', 500)));

    /// <summary>
    /// EL ORDEN DE LAS TRES SENALES: cuerpo > cabecera > User-Agent.
    /// </summary>
    [Theory]
    // La app nueva: se identifica, y gana sobre el User-Agent que sea.
    [InlineData(null, "mac-movil/1.4.2", "Mozilla/5.0 Chrome/130", Canal.Movil)]
    [InlineData(null, "mac-movil/1.4.2", null, Canal.Movil)]
    // El APK viejo: no manda cabecera, lo salva el respaldo del User-Agent.
    [InlineData(null, null, "Dart/3.5 (dart:io)", Canal.Movil)]
    [InlineData(null, "", "Dart/3.5 (dart:io)", Canal.Movil)]
    // El navegador: ni cabecera reconocida ni User-Agent de Dart -> web.
    [InlineData(null, null, "Mozilla/5.0 Chrome/130", Canal.Web)]
    [InlineData(null, "mac-web/2.0", "Mozilla/5.0 Chrome/130", Canal.Web)]
    // Lo declarado en el cuerpo sigue mandando sobre todo lo demas.
    [InlineData("web", "mac-movil/1.4.2", "Dart/3.5 (dart:io)", Canal.Web)]
    [InlineData("movil", "mac-web", "Mozilla/5.0 Chrome/130", Canal.Movil)]
    // Cabecera basura: NO cuenta como "soy web". Decide el respaldo, que es el
    // lado que puede cobrar la llave maestra.
    [InlineData(null, "soy-lo-que-quiero", "Dart/3.5 (dart:io)", Canal.Movil)]
    public void Las_Tres_Senales_Se_Aplican_En_Orden(
        string? declarado, string? clienteApp, string? agente, Canal esperado)
        => Assert.Equal(esperado, CanalDeAcceso.Normalizar(declarado, clienteApp, agente));

    /// <summary>
    /// EL CASO DEL QUE MIENTE PARA ESQUIVAR `app_movil.acceso`: un cliente de
    /// Flutter que se declara web (por cuerpo o por cabecera) se queda en canal
    /// web... y en canal web las claves del telefono no cuentan. Sale perdiendo:
    /// se libra de la llave maestra y a cambio pierde TODO lo que la llave
    /// abria.
    /// </summary>
    [Theory]
    [InlineData("web", null)]
    [InlineData(null, "mac-web")]
    public void Mentir_Diciendo_Soy_Web_Esquiva_La_Llave_Maestra_Pero_Pierde_Las_Claves(
        string? declarado, string? clienteApp)
    {
        var canal = CanalDeAcceso.Normalizar(declarado, clienteApp, "Dart/3.5 (dart:io)");
        Assert.Equal(Canal.Web, canal);

        // Se libra del candado de la llave maestra...
        Assert.False(CanalDeAcceso.ExigeLlaveMaestra(canal));

        // ...y se queda sin ninguna clave del telefono, que es lo que queria usar.
        Assert.Empty(CanalDeAcceso.Filtrar(
            new[] { "app_movil.acceso", "app_movil.pedidos", "app_movil.ventas.facturar" }, canal));
    }

    /// <summary>
    /// LA TRAMPA QUE CASI SE PISA, FIJADA PARA QUE NO VUELVA.
    ///
    /// `incluirRefresh` parece la senal del canal movil -asi lo dicen los
    /// comentarios viejos de LoginRequest.cs y de auth_service.dart- y NO lo
    /// es: el web lo manda en todos sus logins desde que cambio
    /// (docs2/src/nucleo/api.ts:155). Deducir el canal de ahi habria vuelto
    /// canal movil cada entrada al web y le habria pedido `app_movil.acceso`:
    /// medido el 2026-10-15, se quedaban fuera del navegador 11 de 31 usuarios
    /// en Zaragoza y 6 de 10 en Tauro.
    ///
    /// La firma de Normalizar ya no acepta ese dato. Esta prueba existe para
    /// que, si alguien vuelve a pasarselo, tenga que leer esto antes.
    /// </summary>
    [Fact]
    public void Pedir_Refresh_No_Es_Senal_De_Canal_Movil()
    {
        // El cuerpo tipico del login del WEB: pide refresh y no declara canal.
        var comoElWeb = new ISL_Service.Application.DTOs.Requests.LoginRequest
        {
            Usuario = "JUAN", Contrasena = "X", IncluirRefresh = true
        };

        Assert.Equal(Canal.Web, CanalDeAcceso.Normalizar(
            comoElWeb.Canal, agenteDeUsuario: "Mozilla/5.0 (Windows NT 10.0) Chrome/130"));
    }

    /// <summary>
    /// Decir "web" es la unica forma de esquivar la llave maestra, y por eso
    /// tiene que costar: en canal web las claves del telefono no cuentan.
    /// </summary>
    [Fact]
    public void En_Canal_Web_Las_Claves_Del_Telefono_No_Cuentan()
    {
        var permisos = new[] { "app_movil.acceso", "app_movil.pedidos", "ventas.pedidos.crear", "clientes.ver" };

        var enWeb = CanalDeAcceso.Filtrar(permisos, Canal.Web);

        Assert.Equal(new[] { "ventas.pedidos.crear", "clientes.ver" }, enWeb);
    }

    [Fact]
    public void En_Canal_Movil_No_Se_Filtra_Nada()
    {
        // Hay repartidores con claves de los dos tipos; quitarles las del web
        // los dejaria sin pantallas que hoy usan.
        var permisos = new[] { "app_movil.acceso", "app_movil.pedidos", "clientes.ver" };

        Assert.Equal(permisos, CanalDeAcceso.Filtrar(permisos, Canal.Movil));
    }

    [Fact]
    public void Un_Token_De_Antes_Del_Cambio_Se_Comporta_Como_Ayer()
    {
        var permisos = new[] { "app_movil.pedidos", "clientes.ver" };

        // Ni se le filtra...
        Assert.Equal(permisos, CanalDeAcceso.Filtrar(permisos, Canal.NoDeclarado));
        // ...ni se le cobra la llave maestra.
        Assert.False(CanalDeAcceso.ExigeLlaveMaestra(Canal.NoDeclarado));
    }

    [Fact]
    public void Solo_Al_Canal_Movil_Se_Le_Cobra_La_Llave_Maestra()
    {
        Assert.True(CanalDeAcceso.ExigeLlaveMaestra(Canal.Movil));
        Assert.False(CanalDeAcceso.ExigeLlaveMaestra(Canal.Web));
    }

    // ------------------------------------------------------------------
    // El login: quien resuelve el canal, y con que
    // ------------------------------------------------------------------

    /// <summary>
    /// EL CASO QUE PROTEGE AL WEB. Cuerpo identico al que manda el navegador
    /// (incluirRefresh: true, sin `canal`) y User-Agent de navegador: canal
    /// WEB. Si esto se volviera movil, el web entero empezaria a pedir
    /// `app_movil.acceso`.
    /// </summary>
    [Fact]
    public async Task El_Login_Del_Navegador_Es_Canal_WEB()
        => Assert.Equal("web", await CanalQueEstampaElLogin(
            declarado: null, incluirRefresh: true,
            agente: "Mozilla/5.0 (Windows NT 10.0; Win64) Chrome/130.0.0.0"));

    /// <summary>La app ya instalada, que no declara canal: la delata dart:io.</summary>
    [Fact]
    public async Task El_Login_De_La_App_Vieja_Es_Canal_MOVIL()
        => Assert.Equal("movil", await CanalQueEstampaElLogin(
            declarado: null, incluirRefresh: true, agente: "Dart/3.5 (dart:io)"));

    /// <summary>La app nueva lo declara, y lo declarado manda.</summary>
    [Fact]
    public async Task El_Login_De_La_App_Nueva_Es_Canal_MOVIL()
        => Assert.Equal("movil", await CanalQueEstampaElLogin(
            declarado: "movil", incluirRefresh: true, agente: "Dart/3.5 (dart:io)"));

    /// <summary>
    /// Lo que el cliente declare se NORMALIZA: nunca se firma el texto que
    /// mando, solo una de las dos constantes.
    /// </summary>
    [Fact]
    public async Task Un_Canal_Inventado_Se_Normaliza_A_WEB()
        => Assert.Equal("web", await CanalQueEstampaElLogin(
            declarado: "superadmin", incluirRefresh: false, agente: "Mozilla/5.0"));

    /// <summary>
    /// EL APK YA INSTALADO, DE PUNTA A PUNTA. No manda `canal` ni
    /// `X-Cliente-App`: tiene que seguir entrando como movil por el respaldo
    /// del User-Agent. Si esto se rompe, el repartidor que no actualizo se
    /// queda sin poder capturar.
    /// </summary>
    [Fact]
    public async Task El_Login_Del_APK_Viejo_Sin_Cabecera_Sigue_Siendo_MOVIL()
        => Assert.Equal("movil", await CanalQueEstampaElLogin(
            declarado: null, incluirRefresh: true, agente: "Dart/3.5 (dart:io)", clienteApp: null));

    /// <summary>La app nueva se identifica con la cabecera y no depende del User-Agent.</summary>
    [Fact]
    public async Task El_Login_De_La_App_Nueva_Con_Cabecera_Es_MOVIL_Aunque_El_UserAgent_Sea_De_Navegador()
        => Assert.Equal("movil", await CanalQueEstampaElLogin(
            declarado: null, incluirRefresh: true,
            agente: "Mozilla/5.0 (Windows NT 10.0) Chrome/130", clienteApp: "mac-movil/1.4.2"));

    /// <summary>El navegador sigue siendo web, con cabecera propia o sin ninguna.</summary>
    [Theory]
    [InlineData(null)]
    [InlineData("mac-web/2.0")]
    public async Task El_Login_Del_Navegador_Sigue_Siendo_WEB(string? clienteApp)
        => Assert.Equal("web", await CanalQueEstampaElLogin(
            declarado: null, incluirRefresh: true,
            agente: "Mozilla/5.0 (Windows NT 10.0; Win64) Chrome/130.0.0.0", clienteApp: clienteApp));

    /// <summary>
    /// El que miente para esquivar la llave maestra: cliente de Flutter que se
    /// declara web. Se le firma canal WEB, que es el canal sin claves
    /// `app_movil.*` (ver la prueba de Filtrar de arriba).
    /// </summary>
    [Theory]
    [InlineData("web", null)]
    [InlineData(null, "mac-web")]
    public async Task Un_Cliente_Que_Miente_Diciendo_Soy_Web_Recibe_Un_Token_De_Canal_WEB(
        string? declarado, string? clienteApp)
        => Assert.Equal("web", await CanalQueEstampaElLogin(
            declarado: declarado, incluirRefresh: true,
            agente: "Dart/3.5 (dart:io)", clienteApp: clienteApp));

    private static async Task<string?> CanalQueEstampaElLogin(
        string? declarado, bool incluirRefresh, string agente, string? clienteApp = null)
    {
        var espia = new LoginEspia();
        var controller = new SesionController(
            espia, Microsoft.Extensions.Logging.Abstractions.NullLogger<SesionController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        controller.HttpContext.Request.Headers.UserAgent = agente;
        if (clienteApp is not null)
            controller.HttpContext.Request.Headers[CanalDeAcceso.CabeceraClienteApp] = clienteApp;

        await controller.Login(new ISL_Service.Application.DTOs.Requests.LoginRequest
        {
            Usuario = "QA",
            Contrasena = "X",
            IncluirRefresh = incluirRefresh,
            Canal = declarado
        }, default);

        // El texto que se deja en el request y el canal TIPADO que recibe el
        // servicio tienen que ser el mismo: es justo lo que se cerro al hacer
        // del canal un parametro (ver IUserService.LoginAsync).
        Assert.Equal(espia.UltimoCanal, CanalDeAcceso.AClaim(espia.CanalTipado!.Value));

        return espia.UltimoCanal;
    }

    private sealed class LoginEspia : IUserService
    {
        public string? UltimoCanal { get; private set; }

        /// <summary>El canal TIPADO que recibio el servicio.</summary>
        public Canal? CanalTipado { get; private set; }

        public Task<LoginResponse> LoginAsync(
            ISL_Service.Application.DTOs.Requests.LoginRequest request, Canal canal, CancellationToken ct)
        {
            UltimoCanal = request.Canal;
            CanalTipado = canal;
            return Task.FromResult(new LoginResponse());
        }

        public Task<LoginResponse> RefreshAsync(string refreshToken, CancellationToken ct) => throw new NotImplementedException();
        public Task LogoutAsync(string refreshToken, CancellationToken ct) => Task.CompletedTask;
    }

    // ------------------------------------------------------------------
    // El canal viaja FIRMADO, no en una cabecera
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(Canal.Movil, "movil")]
    [InlineData(Canal.Web, "web")]
    public void El_Token_Lleva_El_Canal_Que_Resolvio_El_Servidor(Canal canal, string esperado)
    {
        var token = Emitir(canal);
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal(esperado, jwt.Claims.FirstOrDefault(c => c.Type == "canal")?.Value);
    }

    /// <summary>
    /// Sin canal no se escribe claim: es el token de antes del cambio, y hay
    /// que poder distinguirlo de uno de canal web (al que SI se le filtran las
    /// claves del telefono).
    /// </summary>
    [Fact]
    public void Sin_Canal_No_Se_Escribe_Claim()
    {
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
            .ReadJwtToken(Emitir(Canal.NoDeclarado));

        Assert.DoesNotContain(jwt.Claims, c => c.Type == "canal");
        Assert.Equal(Canal.NoDeclarado, CanalDeAcceso.DeTexto(null));
    }

    private static string Emitir(Canal canal)
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "A9F7Q3Z6Vw2R8eKJcUT4NBydH5sYpM3aXWmL",
            ["Jwt:Issuer"] = "isl-service",
            ["Jwt:Audience"] = "isl-frontend"
        }).Build();

        return new JwtTokenGenerator(config).GenerateToken(new Usuario
        {
            Id = Guid.NewGuid(),
            UsuarioNombre = "QA",
            Rol = "User",
            EmpresaId = 1
        }, "zaragoza", canal);
    }

    // ------------------------------------------------------------------
    // app_movil.pedidos.editar, en el controlador de captura
    // ------------------------------------------------------------------

    private const int PedidoGuardado = 500;   // IDStatusPedido = 1
    private const int PedidoBorrador = 501;   // IDStatusPedido = 0
    private const int IdUsuarioLegacy = 1125;

    /// <summary>
    /// EL CASO DEL INFORME: la app esconde el boton Editar, pero el PUT lo
    /// aceptaba igual. Ahora no.
    /// </summary>
    [Fact]
    public async Task Movil_Sin_La_Clave_De_Editar_No_Puede_Guardar_Un_Pedido_Ya_Guardado()
    {
        var controller = Captura(canal: "movil", permisos: new[] { "app_movil.acceso", "app_movil.pedidos" });

        var r = await controller.Guardar(PedidoGuardado, new PedidoGuardarRequest { IDDomicilio = 1 }, default);

        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(r).StatusCode);
    }

    [Fact]
    public async Task Movil_Con_La_Clave_De_Editar_Si_Puede()
    {
        var controller = Captura(canal: "movil",
            permisos: new[] { "app_movil.acceso", "app_movil.pedidos", "app_movil.pedidos.editar" });

        var r = await controller.Guardar(PedidoGuardado, new PedidoGuardarRequest { IDDomicilio = 1 }, default);

        Assert.IsType<OkObjectResult>(r);
    }

    /// <summary>
    /// LA QUE NO PUEDE ROMPERSE: capturar un pedido NUEVO. El renglon ya existe
    /// (lo creo el primer detalle) y esta en borrador, asi que la primera
    /// grabada es CREAR y no se cobra la clave de editar.
    /// </summary>
    [Fact]
    public async Task Movil_Sin_La_Clave_De_Editar_Si_Puede_Cerrar_Un_Pedido_NUEVO()
    {
        var controller = Captura(canal: "movil", permisos: new[] { "app_movil.acceso", "app_movil.pedidos" });

        var r = await controller.Guardar(PedidoBorrador, new PedidoGuardarRequest { IDDomicilio = 1 }, default);

        Assert.IsType<OkObjectResult>(r);
    }

    /// <summary>
    /// Leer no se cobra: la clave gobierna el boton Editar, no la pantalla.
    /// </summary>
    [Fact]
    public async Task Movil_Sin_La_Clave_De_Editar_Si_Puede_ABRIR_Su_Pedido_Guardado()
    {
        var controller = Captura(canal: "movil", permisos: new[] { "app_movil.acceso", "app_movil.pedidos" });

        Assert.IsType<OkObjectResult>(await controller.Obtener(PedidoGuardado, default));
        Assert.IsType<OkObjectResult>(await controller.Historial(PedidoGuardado, default));
    }

    /// <summary>
    /// El web usa ESTAS MISMAS rutas para modificar. No se le cobra nunca una
    /// clave del telefono: medido, ningun usuario de canal web tiene
    /// `app_movil.pedidos.editar`, asi que cobrarla dejaria fuera a la oficina
    /// entera.
    /// </summary>
    [Fact]
    public async Task El_Web_Nunca_Paga_La_Clave_Del_Telefono()
    {
        var controller = Captura(canal: "web", permisos: new[] { "ventas.pedidos.crear" });

        Assert.IsType<OkObjectResult>(
            await controller.Guardar(PedidoGuardado, new PedidoGuardarRequest { IDDomicilio = 1 }, default));
    }

    // ------------------------------------------------------------------
    // app_movil.ventas.pedidos.modificar: la clave que la app ensena y el
    // servidor no cobraba
    // ------------------------------------------------------------------

    /// <summary>
    /// EL AGUJERO QUE CIERRA: la app gatea su boton "Modificar" de Consulta de
    /// ventas con `app_movil.ventas.pedidos.modificar`, y esa clave no
    /// gobernaba ninguna de las rutas que de verdad modifican un pedido. Darla
    /// sola dibujaba el boton y el PUT contestaba 403.
    /// </summary>
    [Fact]
    public async Task Movil_Con_La_Clave_De_Modificar_Puede_Guardar_Un_Pedido_Ya_Guardado()
    {
        var controller = Captura(canal: "movil",
            permisos: new[] { "app_movil.acceso", "app_movil.pedidos", "app_movil.ventas.pedidos.modificar" });

        Assert.IsType<OkObjectResult>(
            await controller.Guardar(PedidoGuardado, new PedidoGuardarRequest { IDDomicilio = 1 }, default));
    }

    /// <summary>
    /// Y tambien el alta y la baja de renglon, que son la otra forma de
    /// modificar el mismo pedido.
    /// </summary>
    [Fact]
    public async Task Movil_Con_La_Clave_De_Modificar_Tambien_Puede_Tocar_El_Detalle()
    {
        var controller = Captura(canal: "movil",
            permisos: new[] { "app_movil.acceso", "app_movil.pedidos", "app_movil.ventas.pedidos.modificar" });

        Assert.IsType<OkObjectResult>(await controller.AgregarDetalle(new PedidoAgregarDetalleRequest
        {
            IDPedido = PedidoGuardado, IDCliente = 1, IDDomicilio = 1, IDProducto = 1, IDAlmacen = 1, Cantidad = 1
        }, default));

        Assert.IsType<OkObjectResult>(await controller.EliminarDetalle(new PedidoEliminarDetalleRequest
        {
            IDPedido = PedidoGuardado, IDPedidoDetalle = 7
        }, default));
    }

    /// <summary>
    /// LO QUE NO SE PUEDE ROMPER AL SUMARLA: quien edita hoy sigue editando.
    /// La clave nueva se SUMA con un OR, y un OR no cierra nada.
    /// </summary>
    [Theory]
    [InlineData("app_movil.pedidos.editar")]   // la que ya esta repartida
    [InlineData("ventas.pedidos.crear")]       // la de oficina / Mac31
    public async Task Las_Claves_Que_Ya_Editaban_Siguen_Editando(string clave)
    {
        var controller = Captura(canal: "movil",
            permisos: new[] { "app_movil.acceso", "app_movil.pedidos", clave });

        Assert.IsType<OkObjectResult>(
            await controller.Guardar(PedidoGuardado, new PedidoGuardarRequest { IDDomicilio = 1 }, default));
    }

    /// <summary>
    /// LA MITAD QUE NO SE PUEDE OLVIDAR: la policy del endpoint. El cobro de
    /// la clave de editar y la politica del atributo son DOS sitios que
    /// deciden, y con uno solo la clave seguiria sin alcanzar.
    ///
    /// Se comprueba por reflexion porque una llamada directa al metodo del
    /// controlador NO pasa por el atributo: lo aplica el pipeline.
    /// </summary>
    [Theory]
    // Rutas que ABREN Y MODIFICAN un pedido que ya existe: aceptan la clave.
    [InlineData(nameof(VentasPedidoCapturaController.Obtener), true)]
    [InlineData(nameof(VentasPedidoCapturaController.Historial), true)]
    [InlineData(nameof(VentasPedidoCapturaController.AgregarDetalle), true)]
    [InlineData(nameof(VentasPedidoCapturaController.EliminarDetalle), true)]
    [InlineData(nameof(VentasPedidoCapturaController.Guardar), true)]
    [InlineData(nameof(VentasPedidoCapturaController.BuscarCliente), true)]
    [InlineData(nameof(VentasPedidoCapturaController.BuscarProducto), true)]
    [InlineData(nameof(VentasPedidoCapturaController.BuscarProductoPagina), true)]
    // Rutas de CREAR: no la aceptan. Modificar no es crear, y quien solo tenga
    // la clave de modificar no tiene por que poder estrenar pedidos ni tirar
    // borradores: eso seria alcance que nadie le concedio.
    [InlineData(nameof(VentasPedidoCapturaController.Bootstrap), false)]
    [InlineData(nameof(VentasPedidoCapturaController.EliminarBorrador), false)]
    [InlineData(nameof(VentasPedidoCapturaController.MisPedidos), false)]
    public void La_Policy_De_Cada_Ruta_De_Captura(string metodo, bool aceptaModificar)
    {
        var atributo = typeof(VentasPedidoCapturaController)
            .GetMethod(metodo)!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Single();

        var policy = atributo.Policy!;

        // Lo que funciona hoy no cambia en ninguna de las dos.
        Assert.Contains("ventas.pedidos.crear", policy);
        Assert.Contains("app_movil.pedidos", policy);

        Assert.Equal(aceptaModificar, policy.Contains("app_movil.ventas.pedidos.modificar"));
    }

    /// <summary>
    /// Y la politica con ese nombre tiene que EXISTIR con las tres claves: un
    /// nombre mal escrito no es un 403, es un reventon en caliente.
    /// </summary>
    [Fact]
    public async Task La_Politica_De_Modificar_Existe_Con_Las_Tres_Claves()
    {
        var policy = typeof(VentasPedidoCapturaController)
            .GetMethod(nameof(VentasPedidoCapturaController.Guardar))!
            .GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), false)
            .Cast<Microsoft.AspNetCore.Authorization.AuthorizeAttribute>()
            .Single().Policy!;

        var provider = new PermissionPolicyProvider(
            new Microsoft.Extensions.Options.OptionsWrapper<Microsoft.AspNetCore.Authorization.AuthorizationOptions>(
                new Microsoft.AspNetCore.Authorization.AuthorizationOptions()));

        var armada = await provider.GetPolicyAsync(policy);
        Assert.NotNull(armada);

        var requisito = Assert.Single(armada!.Requirements.OfType<PermissionRequirement>());
        Assert.Contains("ventas.pedidos.crear", requisito.Permissions);
        Assert.Contains("app_movil.pedidos", requisito.Permissions);
        Assert.Contains("app_movil.ventas.pedidos.modificar", requisito.Permissions);
    }

    /// <summary>
    /// Y un token de antes del cambio tampoco, por la misma razon que en todo
    /// lo demas: durante el despliegue hay sesiones vivas.
    /// </summary>
    [Fact]
    public async Task Un_Token_Sin_Canal_Tampoco_Paga_La_Clave_Del_Telefono()
    {
        var controller = Captura(canal: null, permisos: new[] { "app_movil.acceso", "app_movil.pedidos" });

        Assert.IsType<OkObjectResult>(
            await controller.Guardar(PedidoGuardado, new PedidoGuardarRequest { IDDomicilio = 1 }, default));
    }

    /// <summary>
    /// Agregar o quitar un renglon de un pedido YA GUARDADO tambien es
    /// modificarlo. Si solo se cerrara el PUT, el mismo pedido se podria
    /// cambiar por detras con el alta de detalle.
    /// </summary>
    [Fact]
    public async Task El_Alta_De_Detalle_Sobre_Un_Pedido_Guardado_Tambien_Se_Cobra()
    {
        var controller = Captura(canal: "movil", permisos: new[] { "app_movil.acceso", "app_movil.pedidos" });

        var alta = await controller.AgregarDetalle(new PedidoAgregarDetalleRequest
        {
            IDPedido = PedidoGuardado, IDCliente = 1, IDDomicilio = 1, IDProducto = 1, IDAlmacen = 1, Cantidad = 1
        }, default);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(alta).StatusCode);

        var baja = await controller.EliminarDetalle(new PedidoEliminarDetalleRequest
        {
            IDPedido = PedidoGuardado, IDPedidoDetalle = 7
        }, default);
        Assert.Equal(StatusCodes.Status403Forbidden, Assert.IsType<ObjectResult>(baja).StatusCode);
    }

    // ------------------------------------------------------------------
    // Andamio
    // ------------------------------------------------------------------

    private static VentasPedidoCapturaController Captura(string? canal, string[] permisos)
    {
        var claims = new List<Claim>
        {
            new("sub", Guid.NewGuid().ToString()),
            new("empresaId", "1"),
            new("rolLegacy", "User"),
            new("idUsuario", IdUsuarioLegacy.ToString())
        };
        if (canal is not null) claims.Add(new Claim(CanalDeAcceso.Claim, canal));

        return new VentasPedidoCapturaController(
            new CapturaDoble(),
            new AccesorDeCanal(),
            new PermisosFijos(permisos))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "pruebas"))
                }
            }
        };
    }

    private sealed class AccesorDeCanal : ICurrentUserAccessor
    {
        public Guid? GetUserId(ClaimsPrincipal user) => Guid.Parse(user.FindFirstValue("sub")!);
        public int GetLegacyUserId(ClaimsPrincipal user) => IdUsuarioLegacy;
        public string GetUsername(ClaimsPrincipal user, string fallback = "Sistema") => "QA";
        public int? GetCompanyId(ClaimsPrincipal user) => 1;
        public string? GetCompanyKey(ClaimsPrincipal user) => "zaragoza";
        public string GetRole(ClaimsPrincipal user) => "User";
    }

    private sealed class CapturaDoble : IVentasPedidoCapturaService
    {
        // El pedido 500 esta GUARDADO (estatus 1) y el 501 en BORRADOR (0), los
        // dos a nombre del usuario del token para que el blindaje de propiedad
        // no se meta en medio y cada 403 sea atribuible a la clave de editar.
        public Task<PedidoPropiedad?> ObtenerPropiedadPedidoAsync(int idPedido, CancellationToken ct)
            => Task.FromResult<PedidoPropiedad?>(idPedido switch
            {
                PedidoGuardado => new PedidoPropiedad(IdUsuarioLegacy, 1),
                PedidoBorrador => new PedidoPropiedad(IdUsuarioLegacy, 0),
                _ => null
            });

        public Task<bool> EsCreadorPorBitacoraAsync(int idPedido, int idUsuario, CancellationToken ct)
            => Task.FromResult(true);

        public Task<PedidoSnapshotDto> GuardarAsync(PedidoGuardarRequest request, int idUsuario, string equipo, CancellationToken ct)
            => Task.FromResult(new PedidoSnapshotDto());
        public Task<PedidoSnapshotDto> AgregarDetalleAsync(PedidoAgregarDetalleRequest request, int idUsuario, string equipo, CancellationToken ct)
            => Task.FromResult(new PedidoSnapshotDto());
        public Task<PedidoSnapshotDto> EliminarDetalleAsync(PedidoEliminarDetalleRequest request, CancellationToken ct)
            => Task.FromResult(new PedidoSnapshotDto());
        public Task<PedidoSnapshotDto> ConsultarPedidoAsync(int idPedido, CancellationToken ct)
            => Task.FromResult(new PedidoSnapshotDto());
        public Task<PedidoRowsResponse> ConsultarHistorialPedidoAsync(int idPedido, CancellationToken ct)
            => Task.FromResult(new PedidoRowsResponse());
        public Task RegistrarEventoAsync(int idPedido, int idUsuario, string usuario, string equipo, int productos, decimal totalPagar, CancellationToken ct)
            => Task.CompletedTask;

        public Task<PedidoBootstrapResponse> BootstrapAsync(int idUsuario, CancellationToken ct) => throw new NotImplementedException();
        public Task<PedidoClienteContextResponse> BuscarClienteAsync(PedidoClienteBuscarRequest request, int idUsuario, CancellationToken ct) => throw new NotImplementedException();
        public Task<PedidoRowsResponse> BuscarProductoAsync(PedidoProductoBuscarRequest request, CancellationToken ct) => throw new NotImplementedException();
        public Task<PedidoProductoPaginaResponse> BuscarProductoPaginaAsync(PedidoProductoPaginaRequest request, CancellationToken ct) => throw new NotImplementedException();
        public Task<PedidoRowsResponse> ConsultarMisPedidosAsync(int idUsuario, string fechaInicial, string fechaFinal, CancellationToken ct) => throw new NotImplementedException();
        public Task<PedidoRowsResponse> EliminarBorradorAsync(int idUsuario, CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class PermisosFijos : IPermissionService
    {
        private readonly string[] _permisos;
        public PermisosFijos(string[] permisos) => _permisos = permisos;

        public Task<PermissionSnapshot> GetPermissionsAsync(Guid userId, int empresaId, string rolLegacy, CancellationToken ct)
            => Task.FromResult(new PermissionSnapshot
            {
                UserId = userId,
                EmpresaId = empresaId,
                RolLegacy = rolLegacy,
                PermissionsEnabled = true,
                Permissions = _permisos.ToList()
            });

        public bool IsAllowedByLegacy(string rolLegacy, string permission) => false;
        public Task<PermisosWebBootstrapResponse?> GetPermisosWebBootstrapAsync(int empresaId, CancellationToken ct) => throw new NotImplementedException();
        public Task<PermisosWebRolesBootstrapResponse?> GetPermisosRolesBootstrapAsync(int empresaId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<PermisosWebPermissionItem>> GetPermissionCatalogAsync(int empresaId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<ModuloDisponibleResponse>> GetAvailableModulesAsync(int empresaId, string companyKey, bool includeAllTenants, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<PermisosWebModuleItem>> GetModuleCatalogAsync(int empresaId, CancellationToken ct) => throw new NotImplementedException();
        public Task<PermisosWebModuleItem> SetModuleStatusAsync(int empresaId, string moduleKey, int idStatus, CancellationToken ct) => throw new NotImplementedException();
        public Task<PermisosWebRoleItem> CreateRoleAsync(int empresaId, string roleCode, string name, CancellationToken ct) => throw new NotImplementedException();
        public Task UpsertRolePermissionsAsync(int empresaId, string roleCode, IReadOnlyCollection<string> permissions, CancellationToken ct) => throw new NotImplementedException();
        public Task UpsertUserOverridesAsync(int empresaId, Guid userId, IReadOnlyCollection<string> allow, IReadOnlyCollection<string> deny, bool sincronizarConLegacy, CancellationToken ct) => throw new NotImplementedException();
        public Task SembrarCatalogoCompletoAsync(int empresaId, CancellationToken ct) => Task.CompletedTask;
        public Task<PermisosWebSyncCatalogResponse> SyncPermissionCatalogAsync(int empresaId, IReadOnlyCollection<string>? modules, CancellationToken ct) => throw new NotImplementedException();
        public Task<bool> CreatePermissionAsync(int empresaId, string key, string name, string? description, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyCollection<string>> GetRolePermissionKeysAsync(int empresaId, string roleCode, CancellationToken ct) => throw new NotImplementedException();
        public Task<(IReadOnlyCollection<string> Allow, IReadOnlyCollection<string> Deny)> GetUserOverrideKeysAsync(int empresaId, Guid userId, CancellationToken ct) => throw new NotImplementedException();
        public Task<AdministracionRestante> GetAdministracionRestanteAsync(int empresaId, SimulacionDePermisos simulacion, CancellationToken ct) => throw new NotImplementedException();
        public Task<bool> EsRolDeSistemaAsync(int empresaId, string roleCode, CancellationToken ct) => throw new NotImplementedException();
    }
}
