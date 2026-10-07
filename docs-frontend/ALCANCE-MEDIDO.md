# Cuanto es "todo el frontend", medido

De las **1,651 acciones** que tiene Mac31, no todas son trabajo. Esto no es
recortar el alcance: es no construir lo que no existe o no es de nadie.

| | Acciones | Que es |
|---|---|---|
| **Zaragoza, vivas** | **1,071** | el trabajo de ahora |
| **Solo Tauro, vivas** | **11** | despues de Zaragoza |
| **Total a construir** | **1,082** | |
| Ni Zaragoza ni Tauro | 402 | de otras empresas |
| Sin decidir (`?`) | 161 | se resuelve mirando Legacy |
| Pantallas muertas | 33 | no se construyen |

## Obra/MC: fuera, y no por decision mia

Eran la duda grande. Medido: **132 acciones, y `aplica_zaragoza` y
`aplica_tauro` valen `no` en las 132.** Son de Alcaldia MC
(el discriminador `EsAlcaldiaMC`, 17 sitios en Legacy), que no es ninguna de
las dos empresas en juego. Las pantallas son `MCContratos*`,
`MCConvenios*`, `MCEstimaciones*`, `Proyectos`.

Aviacion son otras **245**, tambien en `no` las dos columnas, y ademas el
usuario ya las puso al final de la lista. Sus bases (`MacZCS*` y las de
Aviacion) **no estan en esta maquina**, asi que tampoco se podrian comprobar.

Juntas son 377 acciones que parecian trabajo pendiente y no lo son. Si algun
dia entra Alcaldia MC o Aviacion, el inventario ya las tiene listadas con su
endpoint; no hay que volver a inventariar Legacy.

## Las 33 pantallas muertas

`VentasFacturar` y `VentasAjustes` no se instancian **en ningun lugar de
Mac31**: el unico `new VentasAjustes()` esta comentado en
`ConsultarVentas.cs:2582`. Las demas: `ConsultarServicios`,
`ConsultarVentasNotasCredito`, `DetalleDescuentos`, `ReporteCobranza`,
`ReporteVentasPagos`.

Hay un hallazgo de seguridad escondido ahi: **ninguna de esas formas pasa por
`Globales.ValidaOperadorProcesos`**, o sea que no tienen control de permisos.
Estan muertas, asi que hoy no hace dano; si alguien las revive en Legacy, si.
Por eso quedan escritas y no solo descartadas.

## Las 161 sin decidir

133 son de Catalogos y se resuelven abriendo el formulario en Legacy y viendo
si su condicion de empresa (`Funcionalidad`, `EsCentroServicio`,
`EsquemaPago`, `EsAlcaldiaMC`, `EsLuro`, `EsAviacion`) deja pasar a Zaragoza.
24 son de Ventas y 2 del cascaron. **Ante la duda se migra**: un `?` se
construye y se anota, no se descarta.
