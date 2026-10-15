using System.Security.Claims;

namespace ISL_Service.Application.Security;

/// <summary>
/// DESDE QUE CANAL LLEGA LA PETICION, Y QUE POLITICA LE TOCA.
///
/// EL PROBLEMA
/// -----------
/// Web y Movil comparten endpoints. Dos claves del canal movil las aplicaba
/// SOLO Flutter, asi que con un token valido se saltaban llamando a la API a
/// mano:
///
///   * `app_movil.acceso` — la llave maestra de la app. Medido el 2026-10-15:
///     el unico sitio que la mira es `lib/app/mobile_access_gate.dart`. Ni un
///     `[Authorize]` del backend la nombra (grep de "app_movil.acceso" sobre
///     todo el repo: 0 resultados en Controllers/, Infrastructure/ y
///     Application/).
///   * `app_movil.pedidos.editar` — la app esconde el boton Editar de "Mis
///     pedidos" (mis_pedidos.dart, `_puedeEditar`), pero
///     `PUT /api/ventas/pedidos/captura/{id}` pedia
///     `perm:ventas.pedidos.crear|app_movil.pedidos`: cualquiera que pudiera
///     capturar podia modificar un pedido ajeno ya guardado.
///
/// POR QUE UN CLAIM DEL TOKEN Y NO UNA CABECERA
/// --------------------------------------------
/// Una cabecera (`X-Canal: web`) o un campo del cuerpo los escribe el cliente
/// en CADA peticion: no son un dato, son una afirmacion del atacante. El canal
/// aqui va DENTRO del JWT, que el servidor firma con HMAC-SHA256
/// (`Jwt:Key`) y valida con `ValidateIssuerSigningKey = true`
/// (Program.cs, seccion "JWT Authentication"). Para cambiar el claim hay que
/// re-firmar el token, y para re-firmarlo hace falta la llave del servidor.
/// El canal se estampa UNA vez, al iniciar sesion, y viaja protegido por la
/// firma el resto de la sesion; el refresh lo arrastra desde
/// `dbo.WRefreshToken.Canal`, no desde lo que diga el cliente al renovar.
///
/// LA REGLA QUE HACE QUE ESTO NO SE PUEDA ABUSAR
/// ---------------------------------------------
/// DECLARAR UN CANAL NUNCA DA PRIVILEGIOS, SOLO LOS QUITA. Es simetrico a
/// proposito, y por eso mentir no sirve de nada en ninguna de las dos
/// direcciones:
///
///   * Token de canal MOVIL  -> se exige `app_movil.acceso` en el servidor
///     (ver AccesoMovilMiddleware). Decir "soy movil" CUESTA: quien no tiene
///     la llave maestra no pasa ni con credenciales validas.
///   * Token de canal WEB    -> las claves `app_movil.*` NO cuentan
///     (ver <c>CanalDeAcceso.Filtrar</c>). Decir "soy web" para esquivar la llave
///     maestra te deja sin las claves moviles, que es lo contrario de un
///     ascenso: el repartidor que mienta pierde `app_movil.pedidos` y recibe
///     403 en la captura.
///   * Token de canal web legitimo -> funciona EXACTAMENTE igual que hoy: el
///     web nunca usa una clave `app_movil.*` para entrar a sus pantallas
///     (medido: `Web/QR_Web/docs2/src/nucleo/*` gatea con las claves propias
///     —`inventario.ajustes.ver`, `almacen_cascos.ver`,
///     `ventas.pedidos.crear`— y `saleEnWeb()` excluye el modulo `app_movil`
///     entero).
///
/// MAC31 NO SE ENTERA: no es cliente de esta API, no pide token y no pasa por
/// ninguna politica de permisos de aqui; habla con la base por sus `sp_n_`.
/// </summary>
public enum Canal
{
    /// <summary>
    /// El token no dice de que canal es. Son los tokens emitidos ANTES de este
    /// cambio, que viven como maximo 4 h (`JwtTokenGenerator`, expires +4h).
    /// Se comportan EXACTAMENTE como hoy —ni se les filtra `app_movil.*` ni se
    /// les exige la llave maestra— para no echar fuera a quien tenga la app
    /// abierta en el momento del despliegue. No es un agujero permanente:
    /// despues del despliegue ya no se puede emitir uno, porque el canal lo
    /// pone el servidor en TODO login y en TODO refresh.
    /// </summary>
    NoDeclarado = 0,

    /// <summary>Navegador: el web de oficina. Es el valor por omision de un login.</summary>
    Web = 1,

    /// <summary>La app del telefono.</summary>
    Movil = 2
}

public static class CanalDeAcceso
{
    /// <summary>Nombre del claim. Corto a proposito: el JWT viaja en cada peticion.</summary>
    public const string Claim = "canal";

    public const string ValorWeb = "web";
    public const string ValorMovil = "movil";

    /// <summary>La llave maestra de la app movil.</summary>
    public const string LlaveMaestraMovil = "app_movil.acceso";

    /// <summary>Prefijo de las claves que solo existen para el telefono.</summary>
    public const string PrefijoMovil = "app_movil.";

    /// <summary>
    /// Normaliza lo que el cliente declara AL INICIAR SESION. Solo se acepta
    /// "movil"; cualquier otra cosa (vacio, basura, "WEB", "mobile") es web.
    ///
    /// Se normaliza en el servidor y no se guarda el texto del cliente: el
    /// claim que se firma es siempre una de las dos constantes de arriba, asi
    /// que no hay forma de colar un tercer canal que ninguna regla contemple.
    /// </summary>
    public static Canal Normalizar(string? declarado, string? agenteDeUsuario = null)
    {
        var valor = (declarado ?? string.Empty).Trim().ToLowerInvariant();
        if (valor == ValorMovil) return Canal.Movil;
        if (valor == ValorWeb) return Canal.Web;

        /*
          COMPATIBILIDAD CON LA APP YA PUBLICADA, Y EL ERROR QUE SE EVITO AQUI.

          PRIMERO LO QUE **NO** SE USA: `incluirRefresh`. Era la senal obvia
          —LoginRequest.cs dice "Solo la app movil lo manda en true... El web no
          lo manda", y auth_service.dart dice lo mismo del otro lado— y ESTA
          DESACTUALIZADA. Medido el 2026-10-15 en el codigo del web:
          `Web/QR_Web/docs2/src/nucleo/api.ts:155` manda `incluirRefresh: true`
          en TODOS los logins, con su comentario explicando por que ("sin el la
          sesion muere a las 4 horas; la computadora de la puerta del checador
          se queda prendida todo el dia"). Nadie actualizo los dos comentarios
          del backend y del movil cuando el web cambio.

          Si se hubiera usado, CADA login del web se habria vuelto canal movil y
          el web entero habria empezado a pedir `app_movil.acceso`. Lo que
          costaba, medido en las dos bases: Zaragoza 11 de 31 usuarios web
          (sin contar al SuperAdmin) se quedaban fuera del navegador, y Tauro 6
          de 10. Entre ellos la oficina completa.

          LO QUE SE USA: el User-Agent. La app de Flutter habla con `dart:io`,
          que pone `Dart/<version> (dart:io)`; un navegador pone `Mozilla/...`.
          Es un dato que el servidor ve, no algo que el login traiga en el
          cuerpo, y SOBRE TODO: es forjable y da igual que lo sea. Forjarlo te
          mete en el canal movil, que es el que CUESTA la llave maestra. No hay
          nada que ganar mintiendo hacia ese lado, y es justo lo que esta
          heuristica necesita para poder ser una heuristica.

          Sirve solo para la transicion: la app nueva declara `canal` y no
          depende de esto. Si el User-Agent no se reconoce, WEB, que es el
          canal sin las claves del telefono — el lado que quita.
        */
        var agente = (agenteDeUsuario ?? string.Empty).ToLowerInvariant();
        if (agente.Contains("dart") || agente.Contains("flutter"))
            return Canal.Movil;

        return Canal.Web;
    }

    /// <summary>El texto que se firma en el token. Null para NoDeclarado.</summary>
    public static string? AClaim(Canal canal) => canal switch
    {
        Canal.Movil => ValorMovil,
        Canal.Web => ValorWeb,
        _ => null
    };

    /// <summary>El canal del token de ESTA peticion.</summary>
    public static Canal De(ClaimsPrincipal? usuario)
        => DeTexto(usuario?.FindFirst(Claim)?.Value);

    /// <summary>
    /// El canal que representa un texto ya guardado (el claim del token, o la
    /// columna `Canal` de WRefreshToken). Vacio/NULL = NoDeclarado.
    /// </summary>
    public static Canal DeTexto(string? valor)
    {
        var v = (valor ?? string.Empty).Trim().ToLowerInvariant();
        if (v.Length == 0) return Canal.NoDeclarado;
        return v switch
        {
            ValorMovil => Canal.Movil,
            ValorWeb => Canal.Web,
            // Un valor que no se reconoce no se interpreta como "pasa": se
            // trata como web, que es el canal SIN las claves moviles.
            // Equivocarse hacia el lado que quita.
            _ => Canal.Web
        };
    }

    /// <summary>
    /// Las claves que cuentan en este canal.
    ///
    /// En canal web se tiran las `app_movil.*`. SE MIDIO ANTES DE CERRARLO, en
    /// las dos bases locales (2026-10-15), quien tiene una clave `app_movil.X`
    /// sin su gemela web `X`:
    ///
    ///   Zaragoza (MacZ):       DEMO (38 claves), NORMA (6), RICARDO (4),
    ///                          FERNANDA (2), QA_DENY (2)
    ///   Tauro (Produccion_svr): DEMO (31), JANET (5), ADMIN (2)
    ///
    /// Ninguno pierde nada en el web, y no es una suposicion: el web no ofrece
    /// esas pantallas con la clave movil. Su menu y su entrada de modulo piden
    /// la clave propia —`inventario.ajustes.ver` (nucleo/entradaDeModulo.ts:118),
    /// `almacen_cascos.ver` (nucleo/almacenCascos.ts:116)— y `saleEnWeb()`
    /// (nucleo/permisosWeb.ts) deja fuera el modulo `app_movil` completo. Quien
    /// solo tiene la clave movil ya hoy no ve esas tarjetas en el navegador;
    /// lo que se cierra es poder pegarle a la API a mano desde un token de web.
    ///
    /// El canal movil NO se filtra: ahi las claves `app_movil.*` son las que
    /// mandan, y las del web siguen contando igual que hoy (hay repartidores
    /// con claves de los dos tipos, y quitarles las del web los dejaria sin
    /// pantallas que hoy usan).
    /// </summary>
    public static IReadOnlyCollection<string> Filtrar(IReadOnlyCollection<string> permisos, Canal canal)
    {
        if (canal != Canal.Web || permisos.Count == 0) return permisos;

        return permisos
            .Where(p => !EsDelCanalMovil(p))
            .ToList();
    }

    public static bool EsDelCanalMovil(string? clave) =>
        !string.IsNullOrWhiteSpace(clave)
        && clave.StartsWith(PrefijoMovil, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Si a este canal hay que exigirle la llave maestra de la app.
    /// Solo al movil: el web nunca la tuvo y pedirsela lo dejaria fuera entero.
    /// </summary>
    public static bool ExigeLlaveMaestra(Canal canal) => canal == Canal.Movil;

    public static bool TieneLlaveMaestra(IEnumerable<string> permisos) =>
        permisos.Any(p => string.Equals(p, LlaveMaestraMovil, StringComparison.OrdinalIgnoreCase));
}
