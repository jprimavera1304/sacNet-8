using System.Data;
using ISL_Service.Application.DTOs.VentasReasignacion;
using ISL_Service.Application.Interfaces;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/*
  LA LECTURA DE LA PANTALLA DE REASIGNAR. SOLO LEE.

  Las tres consultas de aqui no escriben nada, y eso se comprobo leyendo lo que
  llaman, no suponiendolo:

    - `sp_n_ConsultaAgentes` (leido entero de MacZ el 2026-10-15): arma un
      SELECT dinamico sobre `Agentes INNER JOIN [Catalogo Status]` y lo ejecuta.
      Ni un INSERT, ni un UPDATE, ni un cursor. Con `@IDStatus = 0` devuelve
      TODOS los estatus, que es lo que hace el combo de Mac31.
    - Dos SELECT planos a `dbo.Clientes` y `dbo.Agentes`, con parametros.

  POR QUE EL AGENTE ACTUAL SE LEE DE `dbo.Clientes` Y NO DE LA PRIMERA NOTA
  ------------------------------------------------------------------------
  Porque son dos cosas distintas y Mac31 las confunde. `lblAgenteActual`
  (`VentasReasignacion.cs:402-407`) saca el agente de la PRIMERA FILA de la
  consulta de ventas, o sea el agente de UNA nota. Pero el modo que cambia el
  agente del CLIENTE escribe `UPDATE Clientes SET IDAgente = @IDAgente`, asi
  que lo que se esta por reemplazar es `Clientes.IDAgente`, no el de una nota.
  Cuando el cliente tiene notas de dos agentes —pasa: el cliente 19489 de MacZ
  tiene notas de 4 agentes distintos— la etiqueta de Mac31 ensena uno de los
  cuatro y lo llama "el actual".

  Y con cero notas vigentes Mac31 no ensena nada, que es el caso del modo mas
  usado. De `dbo.Clientes` siempre se puede leer.

  POR QUE NO SE USA LA CAPA DE CENTROS
  ------------------------------------
  Porque este modulo NO esta declarado en `ModulosConCentros` y no debe
  estarlo. El motivo esta medido y escrito en el controlador: el catalogo de
  agentes de la matriz NO coincide con el de las bases de los centros. El
  controlador rechaza con 409 cualquier peticion con `X-Centro != 0`, asi que
  este repositorio nunca se ejecuta apuntando a otra base.
*/
public class VentasReasignacionRepository : SpLegacyRepositoryBase, IVentasReasignacionRepository
{
    private const string SpAgentes = "sp_n_ConsultaAgentes";

    /// <summary>
    /// Cuantos candidatos devuelve una busqueda de cliente por nombre.
    ///
    /// 50 y no "todos": el campo de busqueda de un telefono con tres letras
    /// tecleadas puede casar con cientos de los 882 clientes activos de MacZ, y
    /// bajarlos todos para que la persona elija uno es pagar el viaje entero
    /// para tirarlo. Cuando se corta, la respuesta lo DICE
    /// (<see cref="ClienteCandidatoResponse.Truncada"/>) en vez de ensenar los
    /// primeros 50 como si fueran todos.
    /// </summary>
    public const int MaximoCandidatos = 50;

    private readonly IVentasConsultaRepository _ventas;

    public VentasReasignacionRepository(IConfiguration configuration, IVentasConsultaRepository ventas)
        : base(configuration)
    {
        _ventas = ventas;
    }

    public async Task<List<AgenteDeReasignacionDto>> ConsultarAgentesAsync(CancellationToken ct)
    {
        var filas = await EjecutarFilasAsync(SpAgentes, cmd =>
        {
            // @IDStatus = 0 -> fn_RegresaTablaStatus(0) -> TODOS. Es lo que
            // carga el combo de Mac31, y el estatus viaja en la respuesta para
            // que el cliente pueda apagar la accion sobre un agente de baja.
            cmd.Parameters.Add(Entero("@IDAgente", 0));
            cmd.Parameters.Add(Entero("@IDStatus", 0));
            cmd.Parameters.Add(Entero("@NumeroAgente", 0));
            cmd.Parameters.Add(Varchar("@NombreAgente", 100, ""));
            cmd.Parameters.Add(Entero("@Formato", 0));
            cmd.Parameters.Add(Entero("@Identico", 0));
        }, ct);

        return filas
            .Select(f => new AgenteDeReasignacionDto
            {
                IdAgente = Entero(f, "IDAgente"),
                Numero = Entero(f, "NumeroAgente") is var n && n > 0 ? n : Entero(f, "Agente"),
                Nombre = Texto(f, "NombreAgente") is var nom && nom.Length > 0 ? nom : Texto(f, "Nombre"),
                // `IDStatus = 1` es ACTIVO en `[Catalogo Status]`. Los demas
                // valores (2 = inactivo en `dbo.Agentes`) no son activo.
                Activo = Entero(f, "IDStatus") == 1,
            })
            .Where(a => a.IdAgente > 0)
            .OrderBy(a => a.Nombre, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<ClienteDeReasignacionDto?> ConsultarClienteAsync(int idCliente, CancellationToken ct)
    {
        if (idCliente <= 0) return null;

        var candidatos = await LeerClientesAsync(
            "AND C.IDCliente = @IDCliente", 1,
            cmd => cmd.Parameters.Add(Entero("@IDCliente", idCliente)), ct);

        return candidatos.Count > 0 ? candidatos[0] : null;
    }

    public async Task<ClienteCandidatoResponse> BuscarClientesAsync(
        int numero, string? nombre, CancellationToken ct)
    {
        var texto = (nombre ?? "").Trim();

        if (numero > 0)
        {
            // POR NUMERO ES EXACTO, NO "EMPIEZA CON". El numero 5 y el 50 son
            // dos clientes, y en una pantalla que mueve dinero por cobrar
            // "parecido" no vale.
            var porNumero = await LeerClientesAsync(
                "AND C.Numero = @Numero", MaximoCandidatos + 1,
                cmd => cmd.Parameters.Add(Entero("@Numero", numero)), ct);
            return Armar(porNumero);
        }

        if (texto.Length == 0) return new ClienteCandidatoResponse();

        var porNombre = await LeerClientesAsync(
            "AND (C.Nombre LIKE @Patron OR C.NombreNegocio LIKE @Patron)", MaximoCandidatos + 1,
            cmd => cmd.Parameters.Add(Varchar("@Patron", 202, $"%{Escapar(texto)}%")), ct);
        return Armar(porNombre);
    }

    public Task<(List<Dictionary<string, object?>> Filas, int Total)> ConsultarRemisionesVigentesAsync(
        int idCliente, string desde, string hasta, CancellationToken ct)
    {
        /*
          SE LLAMA AL REPOSITORIO, NO AL SERVICIO DE CONSULTA DE VENTAS, Y ES A
          PROPOSITO.

          El recorte por usuario ("solo lo que yo emiti") vive en
          `VentasConsultaService.AcotarFiltroDeUsuario`, no en el repositorio.
          Pasando por el servicio, esta pantalla heredaria el recorte y
          devolveria una cartera a medias — que es justo el motivo por el que
          este endpoint existe (ver el bloque de los DTOs). El permiso que
          autoriza esta lectura es `cobros.reasignar`, que es el que deja MOVER
          la cartera completa: no se puede mover con responsabilidad lo que no
          se puede ver entero.

          `@Formato = 1` = solo VIGENTES, igual que `VentasReasignacion.cs:332`.
          `@IDCliente` lleva el ID, no el numero: medido en MacZ, mandar el
          numero devuelve cero filas (ver la nota de `idCliente` en el cliente
          del web, `nucleo/ventas.ts`).
        */
        return LeerRemisionesAsync(idCliente, desde, hasta, ct);
    }

    private async Task<(List<Dictionary<string, object?>> Filas, int Total)> LeerRemisionesAsync(
        int idCliente, string desde, string hasta, CancellationToken ct)
    {
        var respuesta = await _ventas.ConsultarRemisionesAsync(
            new Application.DTOs.VentasConsulta.VentasConsultaRequest
            {
                IDCliente = idCliente,
                Formato = 1,
                FechaEmisionInicial = desde,
                FechaEmisionFinal = hasta,
                FechaInicial = desde,
                FechaFinal = hasta,
            }, ct);

        return (respuesta.Rows, respuesta.Rows.Count);
    }

    private static ClienteCandidatoResponse Armar(List<ClienteDeReasignacionDto> encontrados)
    {
        var truncada = encontrados.Count > MaximoCandidatos;
        return new ClienteCandidatoResponse
        {
            Clientes = truncada ? encontrados.Take(MaximoCandidatos).ToList() : encontrados,
            Truncada = truncada,
        };
    }

    /// <summary>
    /// El unico SELECT de clientes de este repositorio. Un solo sitio para que
    /// las tres entradas (por id, por numero, por nombre) devuelvan EXACTAMENTE
    /// las mismas columnas: si el agente actual se leyera distinto segun como
    /// se llego al cliente, la confirmacion diria una cosa u otra segun el
    /// camino.
    /// </summary>
    private async Task<List<ClienteDeReasignacionDto>> LeerClientesAsync(
        string filtro, int tope, Action<SqlCommand> parametros, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        /*
          LEFT JOIN Y NO INNER, Y HACE FALTA. `Clientes.IDAgente` no tiene llave
          ajena contra `Agentes` (comprobado en `sys.foreign_keys` de MacZ), asi
          que puede haber un id que no existe. Con INNER JOIN ese cliente
          DESAPARECERIA de la busqueda, y seria justo el cliente al que hay que
          arreglarle el agente.
        */
        var sql = $@"
            SELECT TOP (@Tope)
                   C.IDCliente,
                   C.Numero,
                   C.Nombre,
                   ISNULL(C.NombreNegocio, '') AS NombreNegocio,
                   C.IDStatus,
                   ISNULL(C.IDAgente, 0)       AS IDAgente,
                   ISNULL(A.Agente, 0)         AS NumeroAgente,
                   ISNULL(A.Nombre, '')        AS NombreAgente
              FROM dbo.Clientes C
                   LEFT JOIN dbo.Agentes A ON A.IDAgente = C.IDAgente
             WHERE 1 = 1 {filtro}
             ORDER BY C.Nombre";

        await using var cmd = new SqlCommand(sql, conn)
        {
            CommandType = CommandType.Text,
            CommandTimeout = CommandTimeoutSeconds,
        };
        cmd.Parameters.Add(Entero("@Tope", tope));
        parametros(cmd);

        var encontrados = new List<ClienteDeReasignacionDto>();
        await using var lector = await cmd.ExecuteReaderAsync(ct);
        while (await lector.ReadAsync(ct))
        {
            encontrados.Add(new ClienteDeReasignacionDto
            {
                IdCliente = LeerEntero(lector, "IDCliente"),
                Numero = LeerEntero(lector, "Numero"),
                Nombre = LeerTexto(lector, "Nombre"),
                NombreNegocio = LeerTexto(lector, "NombreNegocio"),
                Activo = LeerEntero(lector, "IDStatus") == 1,
                IdAgenteActual = LeerEntero(lector, "IDAgente"),
                NumeroAgenteActual = LeerEntero(lector, "NumeroAgente"),
                NombreAgenteActual = LeerTexto(lector, "NombreAgente"),
            });
        }

        return encontrados;
    }

    /// <summary>
    /// Los comodines de LIKE, neutralizados. Sin esto, teclear "%" en el
    /// buscador trae los 882 clientes de un golpe y un "[" rompe el patron.
    /// </summary>
    private static string Escapar(string texto) => texto
        .Replace("[", "[[]")
        .Replace("%", "[%]")
        .Replace("_", "[_]");

    private static int LeerEntero(SqlDataReader lector, string columna)
    {
        var i = lector.GetOrdinal(columna);
        if (lector.IsDBNull(i)) return 0;
        var valor = lector.GetValue(i);
        return valor is int entero ? entero : int.TryParse($"{valor}", out var n) ? n : 0;
    }

    private static string LeerTexto(SqlDataReader lector, string columna)
    {
        var i = lector.GetOrdinal(columna);
        return lector.IsDBNull(i) ? "" : $"{lector.GetValue(i)}".Trim();
    }
}
