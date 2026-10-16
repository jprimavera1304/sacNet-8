using ISL_Service.Application.Centros;
using ISL_Service.Application.DTOs.Centros;
using ISL_Service.Application.Exceptions;
using ISL_Service.Application.Interfaces;
using ISL_Service.Application.Security;
using ISL_Service.Infrastructure.Data;
using Microsoft.Data.SqlClient;

/* El alias evita una coincidencia desagradable: este namespace tambien se llama
   `Centros`, asi que `Centros.CentroPedido` apuntaria aqui dentro y no a la
   pieza de Application. Un alias explicito dice cual es. */
using Cabecera = ISL_Service.Application.Centros.CentroPedido;

namespace ISL_Service.Infrastructure.Centros;

/// <inheritdoc cref="IConexionDelCentro"/>
/// <remarks>
/// ALCANCE DE PETICION, Y POR QUE IMPORTA. El centro es un dato de LA PETICION,
/// no del proceso: dos usuarios en dos sucursales distintas estan dentro del
/// mismo w3wp al mismo tiempo. Guardarlo en algo que viva mas que la peticion
/// es la forma de que a alguien le salgan los datos del centro del de al lado,
/// y ese error no se reproduce cuando se prueba de uno en uno.
///
/// EL SNAPSHOT DE PERMISOS SE LEE UNA VEZ POR PETICION y se guarda en el
/// servicio, que vive exactamente lo que la peticion. Pedir un permiso por
/// centro con doce centros serian doce lecturas del mismo juego de claves — y
/// los viajes de mas a SQL ya pusieron lento este backend una vez.
/// </remarks>
public class ConexionDelCentro : IConexionDelCentro
{
    private readonly IHttpContextAccessor _http;
    private readonly IConfiguration _configuration;
    private readonly ICentrosServicioRepository _centros;
    private readonly IIdentidadDeEmpresaRepository _identidad;
    private readonly ICurrentUserAccessor _usuario;
    private readonly IPermissionService _permisos;
    private readonly ILogger<ConexionDelCentro> _log;

    private SnapshotDeLaSesion.Resultado? _snapshot;
    private int? _centroDeLaCabecera;

    public ConexionDelCentro(
        IHttpContextAccessor http,
        IConfiguration configuration,
        ICentrosServicioRepository centros,
        IIdentidadDeEmpresaRepository identidad,
        ICurrentUserAccessor usuario,
        IPermissionService permisos,
        ILogger<ConexionDelCentro> log)
    {
        _http = http;
        _configuration = configuration;
        _centros = centros;
        _identidad = identidad;
        _usuario = usuario;
        _permisos = permisos;
        _log = log;
    }

    // ---------------------------------------------------------------- cabecera

    public int CentroPedido
    {
        get
        {
            if (_centroDeLaCabecera.HasValue) return _centroDeLaCabecera.Value;

            var crudo = _http.HttpContext?.Request.Headers[Cabecera.Cabecera].ToString();
            if (!Cabecera.Interpretar(crudo, out var id))
                throw new ConflictException(
                    "El centro pedido no es valido.",
                    Cabecera.Cabecera + " tiene que ser un numero entero mayor o igual a 0 (0 = matriz).");

            _centroDeLaCabecera = id;
            return id;
        }
    }

    public bool EsLaMatriz => CentroPedido == Cabecera.Matriz;

    public int ConciliarConElCuerpo(int idCentroDelCuerpo)
    {
        var (gana, desacuerdo) = Cabecera.Conciliar(idCentroDelCuerpo, CentroPedido);

        if (desacuerdo)
            _log.LogWarning(
                "Desacuerdo de centro en {Ruta}: el cuerpo pide {Cuerpo} y la cabecera pide {Header}. Manda el del cuerpo ({Gana}).",
                _http.HttpContext?.Request.Path.Value ?? "", idCentroDelCuerpo, CentroPedido, gana);

        return gana;
    }

    // ------------------------------------------------------------- conexiones

    public Task<SqlConnection> AbrirParaLeerAsync(string modulo, CancellationToken ct = default)
        => AbrirAsync(modulo, CentroPedido, operacion: null, escribe: false, ct);

    public Task<SqlConnection> AbrirParaEscribirAsync(string modulo, string operacion, CancellationToken ct = default)
        => AbrirAsync(modulo, CentroPedido, operacion, escribe: true, ct);

    public Task<string> BaseValidadaParaLeerAsync(string modulo, int idCentro, CancellationToken ct = default)
        => ResolverAsync(modulo, idCentro, operacion: null, escribe: false, ct);

    private async Task<SqlConnection> AbrirAsync(
        string modulo, int idCentro, string? operacion, bool escribe, CancellationToken ct)
    {
        var baseDelCentro = await ResolverAsync(modulo, idCentro, operacion, escribe, ct);

        var cadena = BaseDelCentro.Apuntar(CadenaDeLaEmpresa.De(_configuration), baseDelCentro);
        var conn = new Mac3SqlServerConnector(cadena).GetConnection;
        await conn.OpenAsync(ct);
        return conn;
    }

    /// <summary>
    /// LAS CINCO COMPROBACIONES. Devuelve el nombre de la base ("" = matriz) o
    /// lanza el 409 con su motivo.
    /// </summary>
    /// <remarks>
    /// EL ORDEN NO ES CASUAL. Primero lo que no necesita la base ni revela nada
    /// (el modulo, el token, el interruptor del selector), y solo despues el
    /// catalogo. Si se preguntara al catalogo antes de mirar el modulo, el 409
    /// de "este modulo no sabe de centros" costaria un viaje a SQL y, peor,
    /// distinguiria un centro que existe de uno que no para alguien que no
    /// tiene por que saberlo.
    /// </remarks>
    private async Task<string> ResolverAsync(
        string modulo, int idCentro, string? operacion, bool escribe, CancellationToken ct)
    {
        // ---- 5. EL MODULO. Primero, y por omision NO.
        if (idCentro != Cabecera.Matriz)
        {
            var declarado = ModulosConCentros.Buscar(modulo);
            if (declarado is null || !declarado.SoportaCentros)
                throw new ConflictException(
                    "Esta pantalla todavia no funciona por centro de servicio. Vuelve a la matriz para usarla.",
                    "El modulo no esta declarado en ModulosConCentros. Se contesta 409 a proposito: con el centro puesto, devolver datos de la matriz seria ensenar un sitio diciendo que es otro.");

            if (escribe && !declarado.Escribe(operacion))
                throw new ConflictException(
                    "Esta accion solo se puede hacer en la matriz.",
                    "El modulo no declara esa operacion como escribible fuera de la matriz (ModulosConCentros).");
        }

        // ---- 1. LA EMPRESA DE LA SESION.
        var puede = await CapacidadesAsync(ct);
        if (puede is null)
            throw new UnauthorizedException("Token invalido.");

        // ---- LA MATRIZ: igual que hoy, con una sola regla encima.
        if (idCentro == Cabecera.Matriz)
        {
            if (escribe && !puede.PuedeOperarEnLaMatriz)
                throw new ConflictException(
                    "Tu usuario no tiene permiso para escribir en la matriz.",
                    "Falta " + ClavesDeCentros.MatrizOperar + ".");

            return "";   // la cadena se queda tal cual: el camino de siempre
        }

        // ---- 4 (primera mitad). Puede usar el selector.
        if (!puede.PuedeCambiar)
            throw new ConflictException(
                "Tu usuario no puede cambiar de centro de servicio.",
                "Falta " + ClavesDeCentros.Cambiar + " (o la clave equivalente " + ClavesDeCentros.CambiarCompatible + ").");

        // ---- 2. EL CENTRO EXISTE Y ESTA ACTIVO EN EL CATALOGO DE ESTA EMPRESA.
        //
        // Se busca en dbo.CentrosServicio de la base PRINCIPAL, que es el unico
        // catalogo que esta empresa reconoce. Un IDCentro de otra empresa no
        // "no existe": simplemente no esta aqui, y eso basta.
        var centro = await _centros.BuscarAsync(idCentro, ct);
        if (centro is null || string.IsNullOrWhiteSpace(centro.Base))
            throw new ConflictException(
                "Ese centro de servicio no existe o esta dado de baja.",
                "Ese IDCentro no esta activo en dbo.CentrosServicio de esta empresa.");

        // ---- 3. LA BASE DE ESE CENTRO PERTENECE A ESTA EMPRESA.
        var veredicto = PertenenciaDeCentro.Juzgar(
            await _identidad.DeLaMatrizAsync(ct),
            await _identidad.DeLaBaseAsync(centro.Base, ct));

        if (!veredicto.Pertenece)
        {
            /* Se deja en el log CON el nombre de la base porque es un dato de
               captura que alguien tiene que ver y corregir; al usuario NO se le
               dice que base era. */
            _log.LogWarning(
                "Centro rechazado por pertenencia: IDCentro {Id} ({Nombre}) apunta a la base {Base}, que no es de esta empresa. {Motivo}",
                centro.IdCentro, centro.Nombre, centro.Base, veredicto.Motivo);

            throw new ConflictException(veredicto.Motivo,
                "Ese centro apunta a una base que no pertenece a la empresa de la sesion. Se protege en codigo: los datos de dbo.CentrosServicio NO se corrigen desde aqui.");
        }

        // ---- 4. VER PARA LEER, OPERAR PARA ESCRIBIR.
        if (!puede.PuedeVer(centro.Numero))
            throw new ConflictException(
                "Tu usuario no tiene permiso para consultar el centro " + centro.Nombre + ".",
                "Falta " + ClavesDeCentros.Ver(centro.Numero) + " (o la clave equivalente " + ClavesDeCentros.VerCompatible(centro.Numero) + ").");

        if (escribe && !puede.PuedeOperar(centro.Numero))
            throw new ConflictException(
                "Tu usuario puede consultar el centro " + centro.Nombre + " pero no escribir en el.",
                "Falta " + ClavesDeCentros.Operar(centro.Numero) + ". Escribir en un centro se concede a mano: NO se deduce de poder verlo.");

        return centro.Base;
    }

    // --------------------------------------------------------------- catalogo

    public async Task<CentrosResponse> CatalogoAsync(CancellationToken ct = default)
    {
        var puede = await CapacidadesAsync(ct);
        if (puede is null)
            throw new UnauthorizedException("Token invalido.");

        var respuesta = new CentrosResponse
        {
            PuedeCambiar = puede.PuedeCambiar,
            Matriz = new MatrizItem
            {
                Nombre = await NombreDeLaMatrizAsync(ct),
                PuedeOperar = puede.PuedeOperarEnLaMatriz
            }
        };

        /* Sin el interruptor de arriba no se pinta el selector, asi que la lista
           no se arma: ni se consulta el catalogo ni se abren doce bases para
           comprobar pertenencias que nadie va a usar. */
        if (!respuesta.PuedeCambiar) return respuesta;

        var matriz = await _identidad.DeLaMatrizAsync(ct);

        foreach (var centro in await _centros.ListarAsync(ct))
        {
            /* SALE SOLO LO QUE PUEDE VER. El filtro es del servidor: mandar los
               doce con una bandera en false y dejar que el front los esconda es
               ensenarle el mapa de las sucursales de la empresa a cualquiera
               con la consola del navegador abierta. */
            if (!puede.PuedeVer(centro.Numero)) continue;

            /* Y SOLO LO QUE SE PUEDE PROBAR QUE ES NUESTRO. Si no pertenece, no
               se ofrece: ofrecer un centro que al abrirlo da 409 es peor que no
               tenerlo, porque parece una averia. Esto es lo que hace que en
               Tauro NO aparezcan los cuatro centros de Zaragoza. */
            var veredicto = PertenenciaDeCentro.Juzgar(matriz, await _identidad.DeLaBaseAsync(centro.Base, ct));
            if (!veredicto.Pertenece)
            {
                _log.LogWarning(
                    "Centro {Id} ({Nombre}) fuera del catalogo: su base {Base} no es de esta empresa. {Motivo}",
                    centro.IdCentro, centro.Nombre, centro.Base, veredicto.Motivo);
                continue;
            }

            respuesta.Centros.Add(new CentroItem
            {
                IdCentro = centro.IdCentro,
                Numero = centro.Numero,
                Nombre = centro.Nombre,
                PuedeVer = true,
                PuedeOperar = puede.PuedeOperar(centro.Numero)
            });
        }

        return respuesta;
    }

    /// <summary>
    /// El nombre COMERCIAL de la empresa para la matriz. Nunca "Principal".
    /// </summary>
    /// <remarks>
    /// EL ORDEN: lo configurado, lo que dice la base, y como ultimo recurso el
    /// nombre de la base — que no es un nombre comercial, pero es mejor que un
    /// hueco en la pantalla. Ver IIdentidadDeEmpresaRepository para lo que se
    /// descarto y por que.
    /// </remarks>
    private async Task<string> NombreDeLaMatrizAsync(CancellationToken ct)
    {
        var configurado = (_configuration["Centros:NombreDeLaMatriz"] ?? "").Trim();
        if (configurado.Length > 0) return configurado;

        var identidad = await _identidad.DeLaMatrizAsync(ct);
        if (identidad.NombreComercial.Length > 0) return identidad.NombreComercial;

        return await _identidad.BaseDeLaMatrizAsync(ct);
    }

    // --------------------------------------------------------------- permisos

    /// <summary>
    /// Lo que puede el usuario de ESTA peticion, leido una sola vez.
    /// null = el token no dice quien es.
    /// </summary>
    private async Task<CapacidadesDeCentros?> CapacidadesAsync(CancellationToken ct)
    {
        var user = _http.HttpContext?.User;
        if (user is null) return null;

        _snapshot ??= await SnapshotDeLaSesion.LeerAsync(user, _usuario, _permisos, ct);
        if (!_snapshot.TokenValido) return null;

        var snapshot = _snapshot;
        return new CapacidadesDeCentros(
            snapshot.TodoPermitido, snapshot.Permisos, clave => snapshot.Tiene(clave));
    }
}
