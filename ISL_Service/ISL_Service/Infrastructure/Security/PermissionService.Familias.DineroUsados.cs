using System;
using System.Collections.Generic;

namespace ISL_Service.Infrastructure.Security;

/*
  COBROS DE DINERO Y CASCOS USADOS.

  De las 24 formas de Mac31 que seguian sin heredar, esta es la UNICA que da
  permiso util el mismo dia: sus claves ya existen en WPermiso de las dos
  empresas y ya las comprueban endpoints vivos. Las demas, o no tienen pantalla
  en el web (las quince MC* de obra), o solo producirian claves que nadie
  comprueba, o quitarian acceso a gente real. Esta no: medido el 2026-10-12,
  nadie pierde nada y ganan tres personas en Zaragoza.

  LAS DOS FORMAS Y POR QUE VAN JUNTAS
  -----------------------------------
  "COBROS INGRESOS" (ConsultarDineroUsadosCobros) y "COBROS TRABAJADOS"
  (ConsultarDineroUsadosCobrosClientes) son dos pantallas de Mac31 que en el
  web son la MISMA: paginas\dinero-usados, y sus acciones las sirve
  VentasUsadosOperacionesController con claves usados.cobros_dinero.*.

  EL CHOQUE QUE OBLIGA A DARLE PREFIJO PROPIO A LA SEGUNDA
  --------------------------------------------------------
  Las dos formas tienen btnCancelar, y las dos tienen btnVer, btnBuscar y
  btnModificar. Con el mismo prefijo, cada par produciria UNA sola clave y el
  catalogo guarda un solo IDProceso por clave: la otra forma quedaria muerta en
  silencio. Por eso la segunda lleva el prefijo
  "usados.cobros_dinero.clientes" y de ella solo se toma a mano el boton que
  si tiene clave de verdad en el web, btnAplicar.

  EL OTRO CHOQUE, DENTRO DE LA PRIMERA FORMA
  ------------------------------------------
  btnEntrada ("INGRESAR DINERO Y CASCOS") es el que el web llama
  usados.cobros_dinero.editar. Pero esa misma forma tiene btnModificar, que la
  tabla de verbos derivaria tambien a .editar. Como el Map gana sobre la tabla,
  btnModificar se manda a una clave propia (.modificar) para que no se pisen.
  Medido en las dos empresas: quien tiene btnEntrada tiene tambien
  btnModificar -DEMO, NORMA y RICARDO en MacZ; solo DEMO en Produccion_svr-,
  asi que separarlos no le quita el permiso a nadie.

  LO QUE ESTA FAMILIA NO PUEDE DAR, Y NO SE INVENTA
  -------------------------------------------------
  "usados.ver" es la clave de LECTURA de estas pantallas, y la comparten ocho
  endpoints de VentasUsadosOperacionesController que vienen de OTRAS formas
  (tipos de usados, movimientos de almacen y los excedentes de CobrosDetalle).
  Gobernar el prefijo "usados." desde aqui le borraria a quien los tenga
  usados.excedentes.*, usados.movimientos.* y usados.tipos.editar, porque
  LaMandaLegacy borra por prefijo. Por eso el prefijo de esta familia es
  "usados.cobros_dinero." y no "usados.".

  Consecuencia honesta: la tarjeta del inicio (dinero_usados.ver_modulo) NO se
  puede derivar aqui, porque nacería de usados.ver, que esta familia no posee.
  Se queda sin entrada propia (null) en vez de inventarle una.

  OJO CON LOS IDProceso: no hay ni uno escrito. En estas dos formas NO
  coinciden entre empresas -5383..5386 y 5407 en MacZ contra 5385..5388 y 5409
  en Produccion_svr para la primera; 5387-5393 contra 5389-5395 para la
  segunda-, asi que fijarlos a mano concederia el boton equivocado.
*/
public sealed partial class PermissionService
{
    /// <summary>
    /// COBROS INGRESOS. btnCancelar NO se mapea a proposito: la tabla de
    /// verbos ya lo deriva a ".cancelar", que es justo la clave que el web
    /// comprueba.
    /// </summary>
    private static readonly Dictionary<string, (string Key, string Name)> DineroUsadosCobrosMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnEntrada"] = ("usados.cobros_dinero.editar", "Cobros ingresos - Ingresar dinero y cascos"),

        // A mano SOLO para apagar su derivacion, que chocaria con btnEntrada.
        // La clave es nueva y hoy no la comprueba nadie: existe para que el
        // permiso de Mac31 quede leido y con nombre, no para abrir nada.
        ["btnModificar"] = ("usados.cobros_dinero.modificar", "Cobros ingresos - Modificar (solo registro de Mac31)")
    };

    /// <summary>
    /// COBROS TRABAJADOS. Solo se toma btnAplicar, que es el unico de sus
    /// siete botones cuya clave existe en el web. El resto cae en el prefijo
    /// propio de la forma y produce claves inertes, que es mejor que pisarle
    /// las suyas a la otra forma.
    /// </summary>
    private static readonly Dictionary<string, (string Key, string Name)> DineroUsadosClientesMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnAplicar"] = ("usados.cobros_dinero.aplicar", "Cobros trabajados - Aplicar")
    };

    /*
      EL ModuleKey ES "usados" Y NO "dinero_usados", AUNQUE LA TARJETA DEL
      INICIO SE LLAME ASI.

      Lo caza FamiliasLegacyNoSePierdenTests: EsDeModuloApagado deduce el
      modulo del PRIMER TRAMO de la clave, asi que una familia cuyas claves
      empiezan por "usados." tiene que declarar el modulo "usados". Con
      "dinero_usados" el candado del modulo no cerraria: apagar el modulo no
      filtraria estas claves y se podrian conceder en Mac31 sin poder
      revocarlas desde el web.

      La tarjeta dinero_usados sigue siendo cosa aparte, y por eso esta
      familia no declara entrada.
    */
    private static readonly FamiliaLegacy DineroUsadosCobrosFamilia = new(
        "usados.cobros_dinero.", "usados", "Usados",
        // Sin entrada propia: ver el comentario de arriba. Derivarla desde un
        // permiso de captura le abriria el modulo a quien no puede ni leerlo.
        null, null,
        Array.Empty<PermissionSeed>(),
        new List<LegacyModuleBinding>
        {
            new("usados", "Usados", "CONSULTA DE COBROS INGRESOS",
                DineroUsadosCobrosMap, Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarDineroUsadosCobros",
                Prefijo: "usados.cobros_dinero"),

            new("usados", "Usados", "CONSULTA DE COBROS TRABAJADOS",
                DineroUsadosClientesMap, Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarDineroUsadosCobrosClientes",
                Prefijo: "usados.cobros_dinero.clientes")
        });
}
