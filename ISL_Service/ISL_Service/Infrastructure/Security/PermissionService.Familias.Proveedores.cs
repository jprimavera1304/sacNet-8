using System;
using System.Collections.Generic;

namespace ISL_Service.Infrastructure.Security;

/*
  PROVEEDORES: LA FAMILIA DE REFERENCIA PARA HEREDAR SIN MAPEAR A MANO.

  Se eligio esta primero porque es el caso que rompe las dos suposiciones del
  puente viejo, asi que si funciona aqui funciona en las faciles.

  1. LA DESCRIPCION NO IDENTIFICA LA FORMA.
     Medido en LAS DOS empresas el 2026-10-10: "CONSULTA DE PROVEEDORES" son
     DOS formas distintas, ConsultarProveedores (IDForma 1018, 3 procesos) y
     MCConsultarProveedores (IDForma 4045, 7 procesos, la de obra). El puente
     empataba por descripcion, asi que habria mezclado los permisos de las dos
     pantallas. Por eso estas ataduras van por `FormaExacta`, el nombre de la
     forma, que si es unico. Es el mismo fallo que ya se habia corregido en
     Reportes ("REPORTES" tambien esta duplicada).

  2. VARIAS FORMAS DE LA MISMA FAMILIA TIENEN EL MISMO btnNuevo.
     Las cinco formas de proveedores tienen btnNuevo/btnVer/btnModificar. Si
     todas derivaran con el prefijo del modulo, las cinco producirian
     "proveedores.crear" y se pisarian: el catalogo se indexa por clave y gana
     el primero. Por eso cada atadura lleva su `Prefijo`.

  LO QUE SE HEREDA SOLO: nada de lo de abajo mapea btnNuevo, btnVer,
  btnModificar ni btnBuscar. Los deriva la tabla compartida (VerbosLegacy), y
  dan justo las claves que el web ya usaba —"proveedores.ver", ".crear",
  ".editar"—, que es la prueba de que la derivacion no inventa nombres.

  LO QUE NO SE HEREDA Y POR QUE:

  * "proveedores.estado.editar" (activar/inactivar) se queda del web: Mac31 no
    tiene ese boton dado de alta en n_Procesos, asi que alla no hay nada que
    leer.

  * btnDomicilios, btnApoderados y btnDocumentos (de MCConsultarProveedores)
    SI se mapean a mano: son nombres unicos y su clave no se puede deducir del
    nombre del control.

  OJO CON LOS IDProceso: aqui NO se escribe ninguno a proposito. Medido el
  2026-10-10, estas formas NO tienen los mismos IDProceso en las dos empresas
  (btnBuscar de apoderados es 5275 en MacZ y 5277 en Produccion_svr, con el
  mismo IDForma 4055). El catalogo se arma leyendo la base de cada empresa en
  cada peticion, asi que el numero nunca se fija en el codigo.
*/
public sealed partial class PermissionService
{
    /// <summary>
    /// Los tres botones de MCConsultarProveedores que abren las pantallas de
    /// adentro. Nombres unicos, no derivables: van a mano.
    /// </summary>
    /*
      LA CLAVE ES ".abrir" Y NO ".ver", Y ESO SE MIDIO.

      El primer intento mapeo btnDomicilios a "obra.proveedores.domicilios.ver".
      Comprobado contra /api/me el 2026-10-10, esa clave sale por DOS caminos:
      este boton (que abre la pantalla, y vive en MCConsultarProveedores) y el
      btnVer de adentro de ConsultarProveedoresDomicilios, que la tabla de
      verbos deriva al mismo nombre. En Mac31 son DOS permisos distintos, y al
      juntarlos en una clave quien tuviera uno se quedaba con los dos: un
      permiso mas ancho del que reparte Mac31.

      Asi que el boton que ABRE lleva ".abrir" y el de adentro se queda con el
      ".ver" derivado. Son dos claves porque alla son dos permisos.
    */
    private static readonly Dictionary<string, (string Key, string Name)> ObraProveedoresMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnDomicilios"] = ("obra.proveedores.domicilios.abrir", "Obra - Proveedores: Entrar a domicilios"),
        ["btnApoderados"] = ("obra.proveedores.apoderados.abrir", "Obra - Proveedores: Entrar a representantes legales"),
        ["btnDocumentos"] = ("obra.proveedores.documentos.abrir", "Obra - Proveedores: Entrar a documentos")
    };

    private static readonly Dictionary<string, (string Key, string Name)> ObraProveedoresApoderadosMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnNuevoArea"] = ("obra.proveedores.apoderados.area.crear", "Obra - Proveedores: Nueva area de apoderado")
    };

    private static readonly Dictionary<string, (string Key, string Name)> ObraProveedoresDocumentosMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["btnNuevoDocumento"] = ("obra.proveedores.documentos.crear", "Obra - Proveedores: Nuevo documento")
    };

    /// <summary>
    /// Proveedores "normales" (ConsultarProveedores, IDForma 1018). Es el
    /// modulo que el web ya tiene en docs2.
    /// </summary>
    private static readonly FamiliaLegacy ProveedoresFamilia = new(
        "proveedores.", "proveedores", "Proveedores",
        // La entrada al modulo no existe en Mac31: se DERIVA de quien pueda
        // ver proveedores alla, para no obligar a capturarla a mano a quien ya
        // tenia el permiso. Igual que se hizo en compras.
        "proveedores.ver_modulo", "proveedores.ver",
        new List<PermissionSeed>
        {
            new("proveedores.ver_modulo", "Proveedores - Ver modulo", "proveedores"),
            new("proveedores.estado.editar", "Proveedores - Activar/Inactivar", "proveedores")
        },
        new List<LegacyModuleBinding>
        {
            // Sin Map: btnNuevo/btnVer/btnModificar los deriva VerbosLegacy.
            new("proveedores", "Proveedores", "CONSULTA DE PROVEEDORES",
                new Dictionary<string, (string Key, string Name)>(StringComparer.OrdinalIgnoreCase),
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarProveedores",
                Prefijo: "proveedores")
        });

    /// <summary>
    /// Los proveedores de OBRA, que son otra pantalla y otro modulo del web.
    /// Se declaran aqui, junto a los de arriba, porque lo que los separa es
    /// justo lo que esta explicado arriba.
    /// </summary>
    private static readonly FamiliaLegacy ObraProveedoresFamilia = new(
        "obra.proveedores.", "obra", "Obra",
        // "obra.ver_modulo" ya lo siembra el catalogo del web, asi que aqui no
        // se vuelve a sembrar; solo se dice de que permiso de Mac31 se deriva.
        "obra.ver_modulo", "obra.proveedores.ver",
        Array.Empty<PermissionSeed>(),
        new List<LegacyModuleBinding>
        {
            new("obra", "Obra", "CONSULTA DE PROVEEDORES", ObraProveedoresMap,
                Array.Empty<PermissionSeed>(),
                FormaExacta: "MCConsultarProveedores",
                Prefijo: "obra.proveedores"),

            new("obra", "Obra", "CONSULTA DE PROVEEDOR DOMICILIOS",
                new Dictionary<string, (string Key, string Name)>(StringComparer.OrdinalIgnoreCase),
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarProveedoresDomicilios",
                Prefijo: "obra.proveedores.domicilios"),

            new("obra", "Obra", "CONSULTA DE PROVEEDOR REPRESENTANTES LEGALES",
                new Dictionary<string, (string Key, string Name)>(StringComparer.OrdinalIgnoreCase),
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarProveedoresApoderados",
                Prefijo: "obra.proveedores.apoderados"),

            new("obra", "Obra", "DETALLE DE PROVEEDOR REPRESENTANTES LEGALES",
                ObraProveedoresApoderadosMap, Array.Empty<PermissionSeed>(),
                FormaExacta: "ProveedoresApoderados",
                Prefijo: "obra.proveedores.apoderados"),

            new("obra", "Obra", "CONSULTA DE PROVEEDOR DOCUMENTOS",
                new Dictionary<string, (string Key, string Name)>(StringComparer.OrdinalIgnoreCase),
                Array.Empty<PermissionSeed>(),
                FormaExacta: "ConsultarProveedoresDocumentos",
                Prefijo: "obra.proveedores.documentos"),

            new("obra", "Obra", "DETALLE DE PROVEEDOR DOCUMENTOS",
                ObraProveedoresDocumentosMap, Array.Empty<PermissionSeed>(),
                FormaExacta: "ProveedoresDocumentos",
                Prefijo: "obra.proveedores.documentos")
        });
}
