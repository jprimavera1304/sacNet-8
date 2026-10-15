using System;
using System.Collections.Generic;

namespace ISL_Service.Infrastructure.Security;

/*
  LA TABLA DE VERBOS: HEREDAR SIN MAPEAR A MANO.

  El puente que ya existia (`LegacyModuleBinding`) obliga a escribir a mano la
  pareja "btnNuevo" -> "ventas.crear" para CADA boton de CADA forma. Eso esta
  bien cuando son tres botones; no escala a lo que falta.

  MEDIDO en MacZ el 2026-10-10, sobre los 396 procesos activos de n_Procesos
  que NO son de ReportesMenu:

    btnNuevo      52      btnImprimir   15      btnGuardar     3
    btnVer        51      btnCancelar   19      btnAplicar     4
    btnModificar  47      btnBuscar     33      btnEliminar    2

  Esos nueve nombres son 226 de 396 procesos (57%). Los otros 170 se reparten
  en 121 nombres que aparecen una sola vez ("btnNuevoResidenteSupervision",
  "btnDescargarFactura", ...) y esos SI necesitan mapa propio, porque su clave
  no se puede deducir del nombre del control.

  Asi que el reparto es: la tabla de abajo cubre la mayoria por derivacion
  —como ya hacia Reportes, que deduce la clave del texto del proceso en vez de
  mapearla— y el `Map` de cada familia se queda para las excepciones. El `Map`
  SIEMPRE GANA: si una familia mapea btnNuevo a mano, la tabla no se consulta.

  POR QUE LA CLAVE LLEVA PREFIJO PROPIO Y NO EL DEL MODULO
  --------------------------------------------------------
  Una familia puede tener varias formas y casi todas tienen su btnNuevo. Si las
  cuatro formas de proveedores derivaran con el prefijo del modulo, las cuatro
  producirian "proveedores.crear" y se pisarian entre ellas: el catalogo se
  indexa por clave y el primero en entrar gana. Por eso cada forma puede
  declarar su propio prefijo ("proveedores.documentos"), y solo si no lo
  declara se usa el del modulo.
*/
public sealed partial class PermissionService
{
    /// <summary>
    /// Nombre del control en n_Procesos -> verbo de la clave del web. Lo que
    /// no este aqui no se deriva: se queda fuera a proposito, para que un
    /// boton con nombre raro no genere un permiso inventado.
    /// </summary>
    private static readonly Dictionary<string, string> VerbosLegacy = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnNuevo"] = "crear",
        ["btnVer"] = "ver",
        ["btnModificar"] = "editar",
        ["btnBuscar"] = "consultar",
        ["btnCancelar"] = "cancelar",
        ["btnImprimir"] = "imprimir",
        ["btnEliminar"] = "eliminar",
        ["btnGuardar"] = "guardar",
        ["btnAplicar"] = "aplicar"
    };

    /// <summary>
    /// Texto que se le muestra a quien reparte permisos, por verbo. Solo se
    /// usa cuando n_Procesos no trae Descripcion, que es lo normal.
    /// </summary>
    private static readonly Dictionary<string, string> TextosDeVerbo = new(StringComparer.OrdinalIgnoreCase)
    {
        ["crear"] = "Registrar",
        ["ver"] = "Ver detalle",
        ["editar"] = "Modificar",
        ["consultar"] = "Consultar",
        ["cancelar"] = "Cancelar",
        ["imprimir"] = "Imprimir",
        ["eliminar"] = "Eliminar",
        ["guardar"] = "Guardar",
        ["aplicar"] = "Aplicar"
    };

    /// <summary>
    /// Deriva la clave del web para un proceso de Mac31 que la familia no
    /// mapeo a mano. Devuelve null cuando el nombre del control no esta en la
    /// tabla, y entonces el proceso se ignora igual que antes.
    /// </summary>
    /// <param name="nombreDelControl">El Proceso de n_Procesos, ya sin espacios.</param>
    /// <param name="prefijo">Prefijo de la clave, sin punto final.</param>
    /// <param name="nombreVisible">Nombre del modulo para el texto.</param>
    /// <param name="descripcionLegacy">Lo que traiga n_Procesos.Descripcion, si trae algo.</param>
    private static (string Key, string Name)? DerivarClaveLegacy(
        string nombreDelControl,
        string prefijo,
        string nombreVisible,
        string? descripcionLegacy)
    {
        if (!VerbosLegacy.TryGetValue(nombreDelControl, out var verbo))
            return null;

        var texto = string.IsNullOrWhiteSpace(descripcionLegacy)
            ? (TextosDeVerbo.TryGetValue(verbo, out var t) ? t : verbo)
            : descripcionLegacy.Trim();

        return ($"{prefijo}.{verbo}", $"{nombreVisible} - {texto}");
    }
}
