using System;
using System.Collections.Generic;

namespace ISL_Service.Infrastructure.Security;

/*
  COBROS DE CASCOS USADOS  <-  los dos botones CASCOS de CONSULTA DE HOJAS DE
  COBRO (forma "ConsultarCobros").

  LA AMBIGUEDAD QUE ESTO RESUELVE, Y COMO SE RESOLVIO
  ---------------------------------------------------
  `usados.cobros.editar` tenia DOS candidatos a origen y las dos fuentes del
  repo se contradecian:

    * nucleo/cobranza.ts:109-116 del web decia "botones CASCOS y VER CASCOS de
      ConsultarCobros".
    * el nombre de la semilla -"Cascos usados - Ingresos: registrar",
      PermissionService.cs- apuntaba a btnEntrada de
      ConsultarDineroUsadosCobros, que es OTRA pantalla.

  Se decidio mirando Mac31, que es el oraculo cuando el repo se contradice
  (Legacy\Mac31\Mac31\Forms\Ventas\ConsultarCobros.cs:3227-3275): btnCascos y
  btnVerCascos abren la MISMA pantalla -VentasCobrosUsados- y lo unico que
  cambia entre ellos es `soloLectura`: false para btnCascos, true para
  btnVerCascos. O sea que btnCascos ES el permiso de capturar el cobro de
  cascos, que es justo lo que hace el endpoint que el web protege con
  `usados.cobros.editar` (VentasUsadosOperacionesController.CrearCobroUsadoAsync).

  Gana cobranza.ts. El nombre de la semilla estaba mal y el de aqui lo corrige.
  btnEntrada se queda donde ya estaba, en `usados.cobros_dinero.editar`
  (PermissionService.Familias.DineroUsados.cs), que es su pantalla.

  POR QUE UNA FAMILIA NUEVA Y NO UN RENGLON EN LA DE COBRANZA
  -----------------------------------------------------------
  ConsultarCobros ya la lee la familia "cobranza.", pero una clave mapeada
  tiene que caer DENTRO del prefijo de su familia: el borrado es por prefijo, y
  una clave de fuera se sembraria sin que nadie la gobierne -nadie la borra,
  nadie la repone- y se quedaria congelada con lo que dijera el web, con Mac31
  dejando de mandar sobre ella en silencio. Lo vigila
  FamiliasLegacyNoSePierdenTests.Ninguna_Clave_Mapeada_Se_Sale_De_Su_Familia.

  Asi que la misma forma la leen DOS familias con prefijos distintos, que es un
  patron que ya existe aqui: "cobranza." y "cobranzas." tambien se reparten
  ConsultarCobranzas. Y no choca con `usados.cobros_dinero.`: "cobros_dinero"
  no empieza por "cobros." -el guion bajo lo separa-, asi que el borrado por
  prefijo de una no alcanza a la otra.

  SIN ENTRADA PROPIA, Y A PROPOSITO
  ---------------------------------
  El prefijo "usados." no esta en la tabla de EntradasDeModulo, asi que estas
  claves no viven detras de ninguna puerta. Derivarle una desde un permiso de
  captura le abriria el modulo a quien no puede ni leerlo, y sembrar una clave
  de entrada nueva la dejaria sin tenedores: apagaria el modulo para todos
  menos el SuperAdmin. Es el error que ya se cometio una vez con 68
  capacidades.

  IMPACTO MEDIDO EL 2026-10-12, ANTES DE PONERLO
  ----------------------------------------------
  NADIE PIERDE NADA en ninguna de las dos empresas. La clave la tiene guardada
  solo DEMO, que tambien tiene btnCascos encendido, asi que el puente se la
  repone. Y GANAN lo que Mac31 ya les concede: NORMA, REBECA y RICARDO
  `usados.cobros.editar` y `.ver` en Zaragoza, y FERNANDA solo `.ver` -tiene
  btnVerCascos y no btnCascos, que es exactamente la diferencia que Mac31
  marca-. En Produccion_svr los dos botones los tiene solo DEMO.

  Ni un IDProceso escrito: en estas dos formas no coinciden entre empresas.
*/
public sealed partial class PermissionService
{
    /// <summary>
    /// Los dos botones de cascos, y nada mas.
    /// </summary>
    /// <remarks>
    /// Los otros catorce botones de ConsultarCobros son de la familia
    /// "cobranza." y alli se quedan: si se derivaran tambien aqui, cada uno
    /// produciria una clave "usados.cobros.*" gemela que nadie comprueba y que
    /// ensuciaria el catalogo con casillas que no deciden nada.
    ///
    /// Por eso el Map tiene los dos y la familia NO deja que la tabla de verbos
    /// derive el resto: btnBuscar, btnModificar y compania no aparecen aqui, y
    /// el prefijo propio de la forma hace que lo que derive la tabla caiga en
    /// claves de esta familia. Ver la nota de Prefijo abajo.
    /// </remarks>
    private static readonly Dictionary<string, (string Key, string Name)> CobrosUsadosMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnCascos"] = ("usados.cobros.editar", "Cobros - Capturar los cascos del cobro"),
        ["btnVerCascos"] = ("usados.cobros.ver", "Cobros - Ver los cascos del cobro (solo lectura)"),

        /*
          ESTOS DOS ESTAN AQUI PARA APAGAR SU DERIVACION, Y SE MIDIO POR QUE
          HACE FALTA.

          La tabla de verbos manda btnModificar a ".editar" y btnVer a ".ver",
          que son EXACTAMENTE las dos claves de los botones de cascos. Con el
          Map a medias, la primera version de esta familia le dio
          `usados.cobros.editar` a FERNANDA, que tiene btnModificar y NO tiene
          btnCascos: o sea que quien podia modificar la hoja de cobro se
          llevaba de regalo el permiso de capturar los cascos, que es otra
          pantalla y otra decision.

          Mandandolos a claves propias, ".editar" y ".ver" vuelven a ser solo de
          los botones de cascos. Las dos claves de aqui no las comprueba ningun
          endpoint del web -su pantalla ya se gobierna con cobranza.cobros.*-:
          existen para que el permiso de Mac31 quede leido, con nombre y
          gobernado por el prefijo de esta familia.
        */
        ["btnModificar"] = ("usados.cobros.hoja.editar", "Cobros - Modificar la hoja (solo registro de Mac31)"),
        ["btnVer"] = ("usados.cobros.hoja.ver", "Cobros - Ver la hoja (solo registro de Mac31)")
    };

    private static readonly FamiliaLegacy CobrosUsadosFamilia = new(
        "usados.cobros.", "usados", "Usados",
        null, null,
        Array.Empty<PermissionSeed>(),
        new List<LegacyModuleBinding>
        {
            /*
              Prefijo "usados.cobros" para que lo que derive la tabla de verbos
              de los OTROS catorce botones caiga dentro de esta familia y no
              fuera. Son claves que hoy no comprueba nadie -y por eso la
              pantalla de permisos no las ofrece, ver ClavesQueProtegenAlgo-,
              pero tienen que pertenecer a alguien: una clave derivada que
              cayera fuera del prefijo se quedaria sin gobierno.
            */
            new("usados", "Usados", "CONSULTA DE HOJAS DE COBRO",
                CobrosUsadosMap, Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarCobros",
                Prefijo: "usados.cobros")
        });
}
