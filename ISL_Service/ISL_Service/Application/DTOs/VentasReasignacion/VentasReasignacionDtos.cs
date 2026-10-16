namespace ISL_Service.Application.DTOs.VentasReasignacion;

/*
  EL ARRANQUE DE LA PANTALLA DE REASIGNAR, EN UNA SOLA LECTURA

  POR QUE EXISTE ESTE ENDPOINT Y NO SE REUSAN LOS DEL WEB
  -------------------------------------------------------
  El web arma esta pantalla con tres rutas que YA existian:

      GET  /api/ventas/consulta/catalogos               el catalogo de agentes
      POST /api/ventas/pedidos/captura/clientes/buscar  el cliente
      POST /api/ventas/consulta/remisiones              las notas vigentes

  Las tres funcionan para el web porque quien abre esa pantalla alla suele
  traer, ademas, los permisos de ventas y de captura de pedidos. Para una
  persona que solo tiene `cobros.reasignar` —que es exactamente el permiso de
  ESTE modulo— las tres FALLAN, y dos de ellas fallan CALLANDO:

  1) `POST /api/ventas/consulta/remisiones` pasa por
     `VentasConsultaController.ResolverAlcanceAsync`, que sin
     `ventas.consultar` / `ventas.ver_modulo` / `app_movil.ventas.consulta.ver_de_otros`
     fuerza `IDUsuario = usuarioDelToken` (ver `VentasConsultaService.AcotarFiltroDeUsuario`).
     O sea: devuelve SOLO las remisiones que emitio quien pregunta, sin decirlo.

     MEDIDO EN MacZ (2026-10-15): el cliente 19299 tiene 1,720 remisiones
     emitidas por 14 usuarios distintos; el 20715, 1,634 por 11; el 19468,
     1,563 por 12. Un cobrador veria una fraccion de la cartera, marcaria
     "todas" y moveria una de cada doce notas. Y como la reasignacion cambia de
     quien es el dinero por cobrar, la liquidacion de DOS personas queda mal sin
     que nada en pantalla lo haya avisado.

     Una lista a medias en una pantalla de consulta es una molestia. En esta es
     una mentira con consecuencias contables.

  2) `POST /api/ventas/pedidos/captura/clientes/buscar` acota los clientes al
     AGENTE del usuario ("cualquiera podria pedir los clientes de otro agente
     mandando otro id", dice su propio comentario) y ademas exige la politica de
     MODIFICAR pedidos. Un cobrador no captura pedidos: recibe 403.

  3) `GET /api/ventas/consulta/catalogos` no exige NINGUN permiso —basta estar
     autenticado— y entrega empresas, almacenes, 59 agentes, las fechas de
     operacion y la funcionalidad de la empresa. Es lo contrario del problema,
     pero igual de malo: el catalogo de a quien se le puede entregar una cartera
     no tiene por que estar abierto a cualquiera con cuenta.

  Y hay un cuarto motivo, que es el que no se puede resolver con permisos:

  4) EL AGENTE ACTUAL DEL CLIENTE NO SE PUEDE SABER POR ESAS RUTAS cuando el
     cliente no tiene notas vigentes. Mac31 lo lee de la PRIMERA FILA de la
     consulta de ventas (`VentasReasignacion.cs:407`, `lblAgenteActual`), asi que
     con cero filas la etiqueta se esconde. Pero el modo que solo cambia el
     agente del CLIENTE es valido justamente para un cliente sin notas... y es
     EL MAS USADO: de las 793 filas de `[Ventas Reasignacion]` en MacZ, 557
     (70%) tienen `IDVenta = 0`, que es la firma de ese modo. Una confirmacion
     que no puede decir A QUIEN se le quita la cartera no es una confirmacion.

  ASI QUE ESTE ENDPOINT ES DE LECTURA, ACOTADO A UN CLIENTE, Y DICE LAS CUATRO
  COSAS QUE LA PANTALLA NECESITA PARA NO ENGANAR: el cliente, SU AGENTE ACTUAL
  leido de `dbo.Clientes`, el catalogo de agentes CON SU ESTATUS, y las notas
  vigentes de ese cliente SIN RECORTAR POR USUARIO.

  No se toca ningun `sp_n_`. Se usan `sp_n_ConsultaAgentes` (leido entero: es
  SQL dinamico de solo SELECT) y `sp_n_ConsultaVentas` a traves del repositorio
  que ya existe, mas dos SELECT planos a `dbo.Clientes` y `dbo.Agentes` — el
  mismo patron que ya usa `VentasPagosRepository.EsquemaPagoAsync` para no
  llamar a `sp_n_ConsultaConstantes`, que hace el cierre del dia.
*/

/// <summary>Un agente del catalogo, con su estatus. Destino posible de una cartera.</summary>
public class AgenteDeReasignacionDto
{
    public int IdAgente { get; set; }

    /// <summary>El NUMERO del agente (`Agentes.Agente`), que es lo que la gente dice.</summary>
    public int Numero { get; set; }

    public string Nombre { get; set; } = "";

    /// <summary>
    /// `Agentes.IDStatus = 1`. VIENE EN LA RESPUESTA A PROPOSITO.
    ///
    /// El combo de Mac31 se llena con `new CatAgenteInputDTO()`, o sea
    /// `@IDStatus = 0`, que en `sp_n_ConsultaAgentes` se traduce a
    /// `fn_RegresaTablaStatus(0)` = TODOS los estatus. Asi que alla se puede
    /// entregar una cartera a un agente dado de baja, y PASO: 9 filas de
    /// `[Ventas Reasignacion]` en MacZ apuntan a un agente con `IDStatus &lt;&gt; 1`.
    /// El catalogo se devuelve completo —un agente inactivo sigue teniendo
    /// cartera vieja que a veces hay que mirar— pero marcado, para que el
    /// cliente pueda apagar la accion con su motivo en vez de dejar que la
    /// cartera se vaya a quien ya no trabaja ahi.
    /// </summary>
    public bool Activo { get; set; }
}

/// <summary>El cliente de la reasignacion y de quien es hoy su cartera.</summary>
public class ClienteDeReasignacionDto
{
    public int IdCliente { get; set; }
    public int Numero { get; set; }
    public string Nombre { get; set; } = "";
    public string NombreNegocio { get; set; } = "";
    public bool Activo { get; set; }

    /// <summary>`Clientes.IDAgente`. 0 si el cliente no tiene agente asignado.</summary>
    public int IdAgenteActual { get; set; }

    /// <summary>El numero del agente actual, o 0.</summary>
    public int NumeroAgenteActual { get; set; }

    /// <summary>
    /// El nombre del agente actual. Vacio si `IDAgente` apunta a un agente que
    /// no existe en `dbo.Agentes` — y puede pasar: la columna NO tiene llave
    /// ajena (comprobado en `sys.foreign_keys`), asi que nada impide que ahi
    /// haya un id huerfano.
    /// </summary>
    public string NombreAgenteActual { get; set; } = "";
}

/// <summary>Lo que la pantalla de reasignar necesita para abrirse, de una vez.</summary>
public class ReasignacionArranqueResponse
{
    /// <summary>Nulo cuando se pidio sin cliente: entonces solo viajan los agentes.</summary>
    public ClienteDeReasignacionDto? Cliente { get; set; }

    public List<AgenteDeReasignacionDto> Agentes { get; set; } = new();

    /// <summary>
    /// Las notas VIGENTES del cliente, crudas de `sp_n_ConsultaVentas` con
    /// `@Formato = 1` (es lo que manda `VentasReasignacion.cs:332`).
    /// </summary>
    public List<Dictionary<string, object?>> Remisiones { get; set; } = new();

    /// <summary>
    /// true = la lista es TODA la cartera vigente del cliente, sin recorte por
    /// usuario. Lo dice el servidor y no se deduce en el telefono: es la
    /// diferencia entre "no hay mas" y "no te dejamos ver mas", y en esta
    /// pantalla confundirlas cuesta dinero.
    /// </summary>
    public bool AlcanceCompleto { get; set; } = true;

    /// <summary>Cuantas notas trae, para no contarlas dos veces.</summary>
    public int Total { get; set; }
}

/// <summary>Un candidato de la busqueda de cliente de esta pantalla.</summary>
public class ClienteCandidatoResponse
{
    public List<ClienteDeReasignacionDto> Clientes { get; set; } = new();

    /// <summary>
    /// true cuando la busqueda devolvio mas de lo que cabe y se corto. La
    /// pantalla lo dice en vez de ensenar los primeros N como si fueran todos.
    /// </summary>
    public bool Truncada { get; set; }
}
