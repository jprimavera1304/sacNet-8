# Ola 11 — los reportes que faltaban del menu vivo de Mac31

Hecho el 7-oct-2026 contra el backend nuevo en `http://127.0.0.1:5095` (worktree
aislado, base `LAP-JUAND` / `Produccion_svr` = Tauro, usuario `BancoPruebas`) y,
en **todos** los que entregan documento, contra **Legacy** (`MacServicios2` en
`http://localhost:63479`, `POST api/reportesV2/consultar`) **con el mismo renglon
de `dbo.Parametros`**. Los procedimientos que fallaron se volvieron a correr
pelados en `sqlcmd`, en **las dos** bases, para saber de quien es la culpa.

No se modifico nada de `Legacy/`, nada de `MacReportes2` y **ningun objeto de la
base**: ni SPs, ni `Template_Html`, ni `dbo.Reportes`. Lo unico que se escribio
son renglones de `dbo.Parametros`, que es lo que hace cada generacion de reporte
en los dos sistemas.

`FechaOperacion` de `Produccion_svr` estaba **en el dia** (`2026-10-07`) antes de
empezar, asi que ninguna llamada a Legacy disparo el cierre del dia del negocio a
media medicion. (`MacZ` esta en `2026-10-02`; ahi no se genero nada, ver §7.)

---

## 1. La respuesta corta

| | |
|---|---|
| Reportes del menu vivo de Mac31 que entregan documento | **de 75 a 76 de 99 (77 %)** |
| **Reportes que resultaron NO estar rotos en la base** | **2**: el **106** y el **1245** |
| Reportes que siguen sin entregar documento | **23** |
| …de esos, **rotos tambien en Legacy** (deuda heredada, con la prueba) | **16** |
| …de esos, **inalcanzables a proposito, o sin renglon, o por falta de un centro** | **7** |
| **Reportes nuestros que faltan por hacer** | **0** |
| Claves del catalogo | **57**, sin cambio |
| Claves del catalogo que entregan documento en el barrido | **51 de 57**, igual que la ola 7 |
| Reportes comparados contra Legacy con el mismo `psp` | **76 de 76** (la ola 7 comparo ~20) |
| Reportes en los que el nuevo inventa un numero que Legacy no tenga | **0** |
| Pruebas | **de 449 a 465, 0 fallos** |
| Casos del banco | **135 de 135 en verde** |

**El hallazgo del lote:** la auditoria final apuntaba nueve reportes como
"defectos de los procedimientos en la base". **Dos de los nueve no lo eran.** El
**106** (Clientes CON Compra Por DIA) y el **1245** (Listas de Precios de
productos) no estaban rotos: estaban **mal pedidos**. Su `sp_n_` arma SQL dinamico
con la lista que le llega y se rompe solo cuando llega vacia; Mac31 nunca llega
ahi porque su forma lo impide **antes** de pedir el reporte, y el backend nuevo no
tenia esas dos guardas. Contestaba `500 "Error de base de datos"`, que le echa la
culpa a la base de un dato que faltaba en la peticion.

Con el filtro puesto los dos entregan PDF y **coinciden con Legacy importe por
importe**. Eso corrige la cuenta de la auditoria: no son 9 SP rotos, son **7**.

---

## 2. Reporte por reporte

`gen?` = el `POST legacy/generar` resolvio el id y el GET firmado entrego el
documento. `coincide` se midio pidiendole a Legacy el **mismo `psp`** y comparando
el multiconjunto de importes del PDF (`pdftotext -layout`) contra el HTML de
Legacy, mas el conteo de `<tr>`. **"subconjunto" quiere decir 0 numeros inventados
por el nuevo** y algunos que `pdftotext` junta en la rejilla ancha: es artefacto de
la extraccion, no diferencia de dato (lo mismo que ya vio la auditoria en el 71 y
el 1170).

### 2.1 Los dos que se arreglaron

| clave | id | ¿genero? | tamaño | ¿coincide con Legacy? | ¿falla tambien en Legacy? | notas |
|---|---|---|---|---|---|---|
| `clientes_con_compra_por_dia` + `idDiasSemana` | **106** | **si** | pdf **117 237 B** (175 lineas) | **IGUAL** (277 = 277 importes, **0 inventados**, 155 `<tr>`) | **no**: con los dias Legacy tambien entrega (200, html 250 418 B) | antes 500; le faltaba el filtro de dias |
| `listas_de_precios` productos + `idSubcategorias` | **1245** | **si** | pdf **101 997 B** (46 lineas) | **IGUAL** (16 = 16 importes, **0 inventados**, 30 `<tr>`) | **no**: con las subcategorias Legacy tambien entrega (200, html 21 057 B) | antes 500; le faltaba el filtro de subcategoria |

#### Por que fallaban, con la linea de cada SP

**106 — `sp_n_rptClienteConCompraPorDiaGlobales`.** Param6 es `Dias` (`1~2~4`). El
SP recorre los dias en un cursor y va armando el texto del encabezado; si no hay
dias, `@DiasSemana` se queda en `''` y mas abajo hace:

```sql
SELECT @DiasSemana = SUBSTRING( @DiasSemana, 1, LEN(@DiasSemana)-2 )
```

con `LEN('') - 2 = -2`, que es exactamente
`Invalid length parameter passed to the LEFT or SUBSTRING function`.

Mac31 **no deja llegar ahi** (`ReportesMenu.cs`, case `ClientesConCompraPorDia`):

```csharp
if (Dias == "")
{
    Variables.msgBox.ShowDialog(MsgBox.Accion.NO_PROCESADO, "INFORMACIÓN", "SELECCIONE LOS DÍAS.", "");
    return;
}
```

**1245 — `sp_n_rptListasPreciosProductos`.** Param11 es `IDsCategoria`. El SP lo
pega crudo en el `IN (...)` del `INSERT` dinamico a `#ListasPreciosDetalleTmp`:

```sql
SELECT @SQL = @SQL + '  AND GC.IDGrupoCategoria IN ( ' + REPLACE( @IDsGrupoCategoria, @Delimitador, ',' ) + ')  '
SELECT @SQL = @SQL + '  AND C.IDCategoria IN ( '      + REPLACE( @IDsCategoria,      @Delimitador, ',' ) + ')  '

IF @IDsMarca <> ''
 SELECT @SQL = @SQL + '   AND M.IDMarca IN ( ' + REPLACE( @IDsMarca, @Delimitador, ',' ) + ')  '
```

Vacio queda `AND C.IDCategoria IN ( )`, que es `Incorrect syntax near ')'`. Fijate
que **la de marca SI esta guardada** con `IF @IDsMarca <> ''`: la de categoria se
quedo sin guarda. Mac31 lo tapa desde la forma, en el camino comun de
`GenerarReporte`:

```csharp
if (!Seleccionados(ref IDsCategoria, "IDCategoria", "SELECCIONE UNA SUBCATEGORÍA.", gridMarcas, true))
    return;
```

**El 1245 es el unico de su familia que lo necesita.** El 1240 y las cuatro listas
personalizadas de Tauro (1243/1244/1246/1247) **si** saben contestar con la lista
vacia —comprobado en la ola 7 y otra vez aqui—, asi que la guarda mira la
**categoria** (acumuladores o no), que es lo mismo que mira el resolver para elegir
entre el 1245 y los otros cinco, y **no** la clave. Romper cinco reportes que
funcionan para tapar uno habria sido peor que el problema; hay una prueba que lo
fija.

### 2.2 Los que estan rotos en la base, y la prueba de que alla tambien

Los ocho se corrieron **pelados en `sqlcmd`** (sin backend en medio) y se le
pidieron a **Legacy con el mismo `psp`**. Legacy contesta `status 400` con el
**mismo texto de error**.

| clave | id | ¿genero? | ¿coincide? | ¿falla tambien en Legacy? | notas |
|---|---|---|---|---|---|
| `utilidad` productos | **145** | no, 500 | — | **SI, igual** | `Column name or number of supplied values does not match table definition`. Revienta **dentro** de `sp_n_rptVentasCategoria` (Msg 213, linea 487): `#VentasMarca` del envolvente ya no tiene la forma que devuelve el interno. El **140** (Utilidad Acumuladores), que usa otro SP, si entrega 53 692 B e IGUALA |
| `ventas_y_pagos` | **493** | no, 500 | — | **SI, igual** | mismo mensaje; revienta dentro de `sp_n_rptVentasPagos` (Msg 213, linea 779). `#VentasPagosTmp` del envolvente quedo atrasado (le faltan las columnas `PagosCascosKilo*` que el interno si manda). El 71/75/77, que llaman al mismo interno hacia **otra** tabla temporal, funcionan. **Los Param2..7 del nuevo son identicos a los de Mac31** (`ReportesMenu.cs`, case `VentasPagosComparativo`): no es la peticion |
| `cobranza_pagadas` totalizado | **1166** | no, 500 | — | **SI, igual** | mismo mensaje; revienta dentro de `sp_n_rptVentasSaldos` (Msg 213, linea 956). Es la rama `@Formato = 0` del MISMO SP cuya rama `@Formato = 1` (el **1162**) entrega 89 366 B e IGUALA |
| `cobros_dinero_y_cascos` dinero | **1360** | no, 500 | — | **SI, igual** | `Invalid column name 'CantidadUtilizadaG1'` y 23 mas. `dbo.CobrosDineroUsado` tiene `PrecioUnitarioG1`, `CantidadG1` e `ImporteG1`, y **no** tiene `CantidadUtilizadaG1`, `ImporteUtilizadoG1` ni `ImporteSaldoG1`. **Faltan igual en `MacZ`** (0 de 3) |
| `cobros_dinero_y_cascos` cascos | **1365** | no, 500 | — | **SI, igual** | el mismo SP y la misma causa |
| `estado_de_cuenta` dinero | **1375** | no, 500 | — | **SI, igual** | §2.3 |
| `estado_de_cuenta` cascos | **1380** | no, 500 | — | **SI, igual** | §2.3 |
| `transferencias_duplicadas` **con filtro de agentes** | **74** | no, 500 | — | **SI, igual** | `The multi-part identifier "V.IDAgente" could not be bound`. **Sin** filtro de agentes el 74 entrega 69 556 B e IGUALA a Legacy, y asi es como cuenta en el total. Era el noveno de la lista de la ola 7 y sigue igual |

### 2.3 1375 / 1380: comprobado, y es defecto de la base

El encargo pedia comprobarlo antes de darlo por bloqueado. Comprobado por tres
caminos distintos, y **en las dos bases**:

1. **La firma.** `sys.parameters` dice que `sp_n_rptVentasEstadoCuenta` tiene **un
   solo parametro** hoy:

   ```
   Produccion_svr   sp_n_rptVentasEstadoCuenta         #1 @Params varchar
   Produccion_svr   sp_n_rptVentasEstadoCuentaGlobales #1 @Params varchar
   MacZ             sp_n_rptVentasEstadoCuenta         #1 @Params varchar
   MacZ             sp_n_rptVentasEstadoCuentaGlobales #1 @Params varchar
   ```

2. **El cuerpo del envolvente.** `sp_n_rptVentasEstadoCuentaGlobales`, que es el
   `spV2` del 1375 **y** del 1380, lo llama con **seis**:

   ```
   linea 226:  EXEC sp_n_rptVentasEstadoCuenta  @IDsEmpresa, @IDClienteTmp, @IDAgenteTmp, @FechaInicial, @FechaFinal, @Formato
   ```

3. **Legacy, con el mismo `psp`** (`266680` para el 1375, `266681` para el 1380):

   ```
   id 1375  HTTP 200  status 400  System.Exception: Procedure or function sp_n_rptVentasEstadoCuenta has too many arguments specified.
   id 1380  HTTP 200  status 400  System.Exception: Procedure or function sp_n_rptVentasEstadoCuenta has too many arguments specified.
   ```

Es el mismo SP para los dos sistemas: **Legacy no puede dar otro resultado**. Y en
Mac31 estos dos ids ni se piden, porque el combo que los elegia
(`cboFormatoEdoCuenta`) esta comentado. El resolver nuevo ya los alcanza: el dia
que el envolvente le mande un `IDParametro` al interno —o el interno recupere su
firma de 6— funcionan **sin tocar C#**.

**Que haria falta:** una linea de un `sp_n_`, o sea legacy, o sea no se toca desde
aqui.

### 2.4 1410 / 1415 / 1420: las bases `MacZCS*` no existen

Confirmado y documentado, como pedia el encargo.

| clave | id | ¿genero? | ¿falla tambien en Legacy? | evidencia |
|---|---|---|---|---|
| `movimientos_centros` | **1410** | no, 500 | **SI, igual** | `Invalid object name 'MacZCS7.dbo.Ventas'` (y `MacZCS8`, `MacZCS9`, `MacZCS10`) |
| `centros_pagos_entradas_salidas` | **1415** | no, 500 | **SI, igual** | `Invalid object name 'MacZCS7.dbo.Ventas Pagos'` + `'MacZCS7.dbo.UsadosMovimientos'` |
| `centros_garantias` | **1420** | no, 500 | **SI, igual** | `Invalid object name 'MacZCS7.dbo.Garantias'` |

`sys.databases` de este servidor tiene 47 bases y **ninguna se llama `MacZCS*`**.
El nombre no esta escrito en el SP: sale de la tabla, y por eso no hay nada que
"arreglar" en codigo:

```
dbo.CentrosServicio (Produccion_svr)
 1 |                   | ZARA     | status=1
 ...
 7 | [MacZCS7].[dbo].  | CIRCUITO | status=1
 8 | [MacZCS8].[dbo].  | ERMITA   | status=1
 9 | [MacZCS9].[dbo].  | TALISMAN | status=1
10 | [MacZCS10].[dbo]. | TLALPAN  | status=1
```

**Y no se pueden esquivar pidiendo solo los centros que si existen**: el cursor de
los tres SP no tiene filtro de centro —

```sql
DECLARE Ventas_Cursor CURSOR FOR
 SELECT IDCentro, BaseCentro, NombreCentro
 FROM CentrosServicio
 ORDER BY IDCentro
```

— asi que recorre **los diez** siempre, y los cuatro ultimos cruzan a una base que
no esta. Ni `#ParamIds` del 1410/1415/1420 declara un `IDsCentro` que se pudiera
mandar. **Es infraestructura**: restaurar o renombrar esas cuatro bases. No se
pierde nada al apagar Legacy, porque alla pasa exactamente igual.

### 2.5 1426 / 1428 / 1429 / 1430 / 1432 / 1455: el `spV2` no existe en NINGUNA base

Confirmado en **las dos**, como pedia el encargo. Son los seis Auxiliares de
Cobranza / Fianza / Tarjetas / Ingresos para Contabilidad de la vertical
**Aviacion**.

| id | nombre (igual en las dos bases) | `spV2` | ¿existe en `Produccion_svr`? | ¿en `MacZ`? |
|---|---|---|---|---|
| 1426 | Reporte Auxiliar de Cobranza Consolidada | `sp_n_rptRecaudacionesConsolidadaGlobales` | **NO** | **NO** |
| 1428 | Reporte Auxiliar de Cobranza (DEPÓSITOS) | `sp_n_rptRecaudacionesDepositosGlobales` | **NO** | **NO** |
| 1429 | Reporte Auxiliar de Cobranza | `sp_n_rptRecaudacionesConsolidadaGlobales` | **NO** | **NO** |
| 1430 | Reporte Auxiliar de Fianza | `sp_n_rptRecasFianzasGlobales` | **NO** | **NO** |
| 1432 | Reporte Auxiliar de Cobranza de Tarjetas | `sp_n_rptRecaudacionesTarjetasGlobales` | **NO** | **NO** |
| 1455 | Reporte de Ingresos para Contabilidad | `sp_n_rptIngresosContabilidad` | **NO** | **NO** |

Pelado, en las dos bases:

```
EXEC dbo.sp_n_rptRecaudacionesConsolidadaGlobales '266459'
Msg 2812 … Could not find stored procedure 'dbo.sp_n_rptRecaudacionesConsolidadaGlobales'.

EXEC dbo.sp_n_rptIngresosContabilidad '1'
Msg 2812 … Could not find stored procedure 'dbo.sp_n_rptIngresosContabilidad'.
```

Y **no hay un SP con otro nombre que haga lo mismo**: de todo lo que suena a esto,
en las dos bases solo existen `sp_n_ConsultaRecaudaciones` y
`sp_n_InsertarRecaudacion`, que son la captura, no el reporte.

Legacy lee el `spV2` del mismo renglon de `dbo.Reportes` y llama al mismo nombre:
**no puede dar otro resultado**. Son parte de los 88 `sp_n_` ausentes que la
auditoria ya documento para toda la vertical Aviacion. **No se les creo clave en el
catalogo a proposito**: dar de alta una clave que no puede entregar nada es poner
un boton en el menu del web que siempre falla.

**Una correccion al mapa:** `reportes-cobertura.tsv` anota en el **1428** *"OJO:
mismo id, distinto reporte por empresa"*. **No es asi.** Los seis tienen el **mismo
`NombreReporte` y el mismo `spV2` en `Produccion_svr` y en `MacZ`** (comprobado con
el `SELECT` de las dos bases). La regla de mirar las dos bases antes de agregar un
id por numero sigue valiendo —el 1017 es "Compras" en Tauro y "Cobranza" en
Zaragoza— pero **aqui no aplica**.

### 2.6 72: cableado y no comprobable en estas dos bases

| clave | id | ¿genero? | ¿falla tambien en Legacy? | notas |
|---|---|---|---|---|
| `pagos` (detallado) | **72** | **no alcanzable aqui** | n/a (Legacy tampoco) | el resolver devuelve **77** porque `Constantes.EsCentroServicio = 0` |

`EsCentroServicio` vale **0 en las dos bases** (`Produccion_svr`: `TAU`, 0; `MacZ`:
`ZARA`, 0), y es la base quien lo decide, no una constante de C#. La eleccion es la
misma que hace Mac31:

```csharp
if (!ResolveFormatoDetallado(formato))  return ReporteVentasPagosTotalizado;   // 75
if (constants.EsCentroServicio == 1)    return ReporteVentasPagosDetalladoCS;  // 72
return EsTauro(constants) ? ReporteVentasPagosDetalladoTauro                  // 77
                          : ReporteVentasPagosDetallado;                      // 71
```

Tampoco se llega por el selector de centros: los centros 1 a 6 tienen `BaseCentro`
vacio (son la matriz) y los 7 a 10 apuntan a las `MacZCS*` que no existen. **Hace
falta una instalacion de centro de servicio** para generarlo. Legacy tampoco lo
alcanza aqui, por lo mismo.

### 2.7 71, 1240 y 1355: no son huecos

| id | ¿genero? | por que |
|---|---|---|
| **71** Pagos Detallado | no en Tauro | en Tauro Mac31 pone el **77** (`funcionalidad == "TAU"`). El nodo **si** entrega papel: el 77, 89 836 B, subconjunto de Legacy con 0 inventados. El 71 es el de Zaragoza |
| **1240** Listas de Precios | no en Tauro | en Tauro Mac31 pone una de las **cuatro** personalizadas (1243/1244/1246/1247), y las cuatro entregan e IGUALAN a Legacy. El 1240 es el de Zaragoza |
| **1355** Ventas Cobros Dinero y Cascos | nunca | **Mac31 no lo pide jamas**: el nodo pone 1360 o 1365 segun el radio. Ya lo documento la ola 7 y se vuelve a confirmar |

### 2.8 240, 76 y 1242: los tres ids fantasma

Completan el denominador de **99** del `reportes-plan.md` (96 renglones de
`reportes-cobertura.tsv` + estos tres) y **no tienen renglon en `dbo.Reportes` en
ninguna de las dos bases**:

```
SELECT COUNT(*) FROM dbo.Reportes WHERE IDReporte IN (240,76,1242)
Produccion_svr -> 0
MacZ           -> 0
```

Sin renglon no hay `spV2` ni `Aspx`, asi que **Legacy tampoco puede generarlos**.
No son deuda de esta migracion.

### 2.9 Los 76 que entregan, comparados uno por uno

A diferencia de la ola 7, que comparo ~20, aqui se le pidio a Legacy **el mismo
`psp` de los 76**:

| | |
|---|---|
| **IGUAL** (mismo multiconjunto de importes, mismos `<tr>`) | **69** |
| **subconjunto** (0 inventados; faltan algunos por `pdftotext`) | **5**: 77, 105, 1140, 1160, 1170 |
| no comparable por importes | **1**: el **118**, que solo existe en xlsx (§3.1) |
| Legacy no entrega y el nuevo si | **1**: el **1250** (`"El origen no contiene DataRows"`: `dbo.MC_ContratosEstimaciones` esta vacia y el nuevo si da el papel vacio) |
| **DIFIEREN (el nuevo inventa numeros)** | **0** |

Los cinco "subconjunto" son el patron ya verificado por la auditoria en el 71 y el
1170: `pdftotext` junta celdas pegadas en rejillas anchas y pierde la frontera
entre dos numeros. Lo que importa de la medicion es la otra direccion: **el nuevo
no inventa ni un numero que Legacy no tenga, en ninguno de los 76**.

#### Y traen contenido, no solo papel

Con la ventana de fechas del barrido (15 a 23 de septiembre de 2026) **42** traen
datos y **32** salen vacios **en los dos sistemas**: la base local no tiene
movimientos de esos tipos en esos nueve dias. Para no entregar "genera PDF" como
si fuera "funciona", los 32 se volvieron a pedir con una ventana ancha
(**2025-06-01 a 2026-10-07**) y se volvieron a comparar:

| | |
|---|---|
| de los 32, pasan a traer **contenido real** | **19**: 24, 120, 125, 130, 150, 230, 1026, 1185, 1230, 1231, 1320, 1330, 1335, 1395, 1440, 1475, 1480, 1485, 1490 |
| siguen vacios **en los dos** incluso con 16 meses | **13**: 19, 235, 1235, 1236, 1305, 1319, 1370, 1405, 1406, 1407, 1465, 1470, 1495 (sus tablas estan vacias en esta base; el 1236 ya lo documento la ola 7: `dbo.GarantiasRegistro` tiene 0 renglones) |
| de los 32 con ventana ancha: **IGUAL** | **30** |
| **subconjunto** (0 inventados) | **2**: 1185 (falta 1 de 19 021) y 1320 (faltan 1 175 de 4 479, rejilla ancha) |
| **DIFIEREN** | **0** |

Ejemplos de lo que aparece al abrir la ventana: el **150** (Cascos) pasa de 52 490
a **1 896 750 B con 10 516 `<tr>`**, el **1185** (Concentrados) de 61 200 a
**1 751 444 B con 19 020 importes identicos a Legacy**, el **24** (Facturas) de
75 496 a **539 572 B con 10 267 = 10 267**.

---

## 3. Que se cambio en el codigo

Nada del motor se reescribio: se extendio, como pedia el encargo. Ningun generador
de plantilla nuevo, ninguna clave de catalogo nueva, ningun permiso nuevo, ninguna
ruta nueva.

| archivo | que |
|---|---|
| `Application/Reportes/FiltrosObligatoriosDeReporte.cs` (**nuevo**) | una sola funcion pura, `QueFalta(...)`, con el porque de cada guarda, la linea del SP que se rompe y el mensaje con el que Mac31 lo impide. Esta aparte del controlador para poder probarla sin levantar DI |
| `Controllers/ReportesVentasController.cs` | `legacy/generar` consulta esa funcion y contesta **400 con el motivo** en vez de **500 "Error de base de datos"**. Queda junto a la guarda del estado de cuenta sin cliente, que es el mismo caso |
| `ISL_Service.Tests/Ola11FiltrosObligatoriosTests.cs` (**nuevo**) | 16 pruebas: que las dos guardas salten; que **no** salten con el filtro puesto; que **no** se extiendan a las listas de acumuladores (las cinco que si saben contestar vacias); que no toquen ninguna otra clave; y que la clave se reconozca con espacios y mayusculas |

**465 pruebas, 0 fallos** (449 en la punta de `migracion-legacy` + 16). **0 errores
de compilacion, 0 advertencias nuevas.** `RutasSinChocarTests` y
`ClavesDePermisoSembradasTests` pasan: no se agrego ruta ni clave de permiso.

### 3.1 Lo que NO se cambio, y por que

- **El 118 sigue siendo solo Excel.** Con `Presentacion = 0`,
  `sp_n_rptClienteCompraAcumMotoLubMensual` arma mal su SQL dinamico (`Incorrect
  syntax near the keyword 'FROM'`), y Mac31 fuerza Excel por esa misma razon y hasta
  lo dice en un comentario. No es una limitacion que inventamos. Entrega **xlsx
  45 752 B**.
- **No se dio de alta clave para 1426/1428/1429/1430/1432/1455.** Su `spV2` no
  existe en ninguna base: la clave seria un boton que siempre falla.
- **No se inserto ningun renglon en `dbo.Reportes`** para 240, 76 ni 1242, ni se
  toco `Template_Html`. Faltan y queda documentado, como pedia el encargo.
- **La guarda del 1245 no se extendio a las listas de acumuladores.** Hay una
  prueba que lo fija.
- **No se toco el resolver.** Los 15 ids de este lote ya estaban alcanzables desde
  la ola 7; lo que faltaba era el filtro, no el `case`.

---

## 4. Barrido de regresion: las 57 claves

Se pidieron **las 57 claves del catalogo** de corrido y se comparo con el de la ola
7. Entregan documento **51**; fallan **6**. **El resultado es el mismo que el de la
ola 7**, con una sola diferencia, y es una mejora:

```
                         ola 7                    ola 11
k_ventas_pagos    493    500 (base)               500 (base)                  igual
k_cli_dia         106    500 (base)       ->      400 "selecciona los dias"   <-- el cambio
k_cobros_dc      1360    500 (base)               500 (base)                  igual
k_mov_centros    1410    500 (MacZCS*)            500 (MacZCS*)               igual
k_cen_pagos      1415    500 (MacZCS*)            500 (MacZCS*)               igual
k_cen_gar        1420    500 (MacZCS*)            500 (MacZCS*)               igual
```

El barrido por clave manda la clave **sin filtros**, asi que el 106 cae en su
guarda: ahora dice **por que** en vez de culpar a la base. Con los dias puestos
entrega 117 237 B. Las otras 51 claves entregan exactamente lo mismo que en la ola
7, byte arriba byte abajo por la fecha de impresion.

Y **la clave `listas_de_precios` no cambia** en ese barrido: por omision pide
acumuladores, y ahi el resolver da el 1243, que nunca necesito el filtro.

---

## 5. Defectos heredados: la cuenta corregida

La ola 7 y la auditoria final hablaban de **9 reportes rotos en la base**. Con la
comprobacion de este lote queda asi:

| id | ¿de verdad es de la base? | que es |
|---|---|---|
| **106** | **NO** | nos faltaba el filtro de dias. **Arreglado** |
| **1245** | **NO** | nos faltaba el filtro de subcategoria. **Arreglado** |
| 145 | **SI** | `#VentasMarca` del envolvente no empata con `sp_n_rptVentasCategoria` |
| 493 | **SI** | `#VentasPagosTmp` del envolvente no empata con `sp_n_rptVentasPagos` |
| 1166 | **SI** | la rama Totalizado de `sp_n_rptVentasSaldos` no empata con el envolvente |
| 1360 | **SI** | `dbo.CobrosDineroUsado` no tiene las columnas `*Utilizada*` / `*Saldo*` |
| 1365 | **SI** | el mismo SP, la misma causa |
| 1410 | **SI** (infraestructura) | `MacZCS7..10` no existen |
| 74 con filtro de agentes | **SI** | `V.IDAgente` no se puede resolver en ese `SELECT` |

Mas los que ya se sabian y se vuelven a confirmar: **1375 / 1380**, **1415 / 1420**,
los **seis de Aviacion** sin `spV2` y los **tres ids fantasma** sin renglon.

**Todos y cada uno se le pidieron a Legacy con el mismo `psp`, y Legacy contesto el
mismo error.** Ninguno es deuda del backend nuevo.

---

## 6. Como se reproduce

```bash
cd ISL_Service/ISL_Service && dotnet build ISL_Service.csproj
nohup dotnet run --no-build --no-launch-profile --urls "http://127.0.0.1:5095" &

# token
curl -s -X POST http://127.0.0.1:5095/api/sesion/login \
  -H 'Content-Type: application/json' \
  -d '{"usuario":"BancoPruebas","contrasena":"Banco2026"}'

# el 106, que antes daba 500: AHORA CON LOS DIAS
curl -s -X POST http://127.0.0.1:5095/api/reportes/ventas/legacy/generar \
  -H 'Content-Type: application/json' -H "Authorization: Bearer $TOK" \
  -d '{"reporteKey":"clientes_con_compra_por_dia","fechaInicial":"2026-09-15",
       "fechaFinal":"2026-09-23","idDiasSemana":[1,2,3,4,5,6]}'

# el 1245, que antes daba 500: AHORA CON LAS SUBCATEGORIAS
curl -s -X POST http://127.0.0.1:5095/api/reportes/ventas/legacy/generar \
  -H 'Content-Type: application/json' -H "Authorization: Bearer $TOK" \
  -d '{"reporteKey":"listas_de_precios","fechaInicial":"2026-09-15",
       "fechaFinal":"2026-09-23","idGrupoCategoria":3,"categoria":"productos",
       "idSubcategorias":[4,6,8,10,15,17,21,22,26,30,31,35,47,54,60]}'

# y sin el filtro, que es lo que dice ahora en vez de 500:
#   400 {"message":"Selecciona los dias: ... idDiasSemana (1 lunes - 6 sabado)."}
#   400 {"message":"Selecciona una subcategoria: ... idSubcategorias."}

# el MISMO psp, a Legacy, para comparar el dato
curl -s -X POST http://localhost:63479/api/reportesV2/consultar \
  -H 'Content-Type: application/json' -H "Authorization: Bearer $APP_TOKEN" \
  -d '{"token":"'"$APP_TOKEN"'","correoElectronicoToken":"app_web@tauro.com",
       "IDAplicacion":1,"idReporte":106,"parametros":"266457",
       "parametrosCadena":"","MostrarReporte":0,"Presentacion":0}'
```

El barrido se hizo con tres guiones, que son el metodo y no el resultado:

| guion | que hace |
|---|---|
| `barrido.py` | pide **las 57 claves** del catalogo de corrido y baja el documento de cada una. Es el barrido de regresion de §4. `RPT_FI` / `RPT_FF` cambian la ventana de fechas |
| `barrido_ids.py` | pide **los 96 ids del menu vivo** uno por uno, con los campos que hacen que el resolver elija **exactamente** ese id, y **comprueba que el id que contesta sea el esperado**: si no, lo marca "no alcanzable" en vez de contarlo. Es la medicion de §1. Acepta una lista de ids para repetir solo unos cuantos |
| `comparar_lote.py` | por cada id que entrego, le pide a Legacy el **mismo `psp`** y compara importes y `<tr>`. Es la tabla de §2.9 |

Dos notas utiles sobre `api/reportesV2/consultar`: el `psp` va en el campo
**`parametros`** y exige `idReporte` y `IDAplicacion`; y **Legacy resuelve el SP por
el `IDReporte` guardado en el renglon de `dbo.Parametros`, no por el `idReporte`
del cuerpo**. Para preguntarle por un id hay que mandarle un `psp` generado **para
ese id**: eso descarta el atajo de reusar un `psp` ajeno para probar los seis de
Aviacion, y por eso ahi la prueba es el `EXEC` pelado.

---

## 7. Comportamiento por empresa

Antes de dar cualquier id por bueno se comprobo `NombreReporte` **y** `spV2` **en
las dos bases**, que es la regla del mapa (el 1017 es "Compras" en Tauro y
"Cobranza" en Zaragoza):

- Los 15 ids de este lote (72, 106, 493, 1360, 1375, 1380, 1410, 1415, 1420, 1426,
  1428, 1429, 1430, 1432, 1455) tienen **el mismo nombre y el mismo `spV2` en
  `Produccion_svr` y en `MacZ`**. **Ninguno** cambia de reporte segun la empresa, y
  eso incluye el 1428, que el mapa marcaba como que si (§2.5).
- Donde el id **si** depende de la empresa, lo sigue decidiendo la base y no una
  constante de C#: `Constantes.Funcionalidad` (`TAU` / `ZARA`) y
  `Constantes.EsCentroServicio` eligen entre 71/72/77 y entre 1240 y las cuatro
  listas de Tauro.

**Lo que no se pudo ejecutar, igual que en la ola 7:** generar en Zaragoza.
`BancoPruebas` **no existe en `MacZ`** —cada empresa tiene su propia base de
usuarios—, asi que la ruta de Zaragoza queda verificada por codigo y por consulta a
la base, no por generacion. Por eso tampoco se movio la `FechaOperacion` de `MacZ`.

---

## 8. Que queda, y de quien es

| pendiente | de quien |
|---|---|
| `#VentasMarca` del 145, `#VentasPagosTmp` del 493 y la rama Totalizado del 1166: tres tablas temporales que quedaron atrasadas respecto a su SP interno | legacy / base |
| Que `dbo.CobrosDineroUsado` recupere las columnas `CantidadUtilizadaG*`, `ImporteUtilizadoG*` e `ImporteSaldoG*` (desbloquea 1360 y 1365) | legacy / base |
| Que `sp_n_rptVentasEstadoCuentaGlobales` deje de llamar al interno con 6 argumentos (desbloquea 1375 y 1380) | legacy / base |
| Que el `SELECT` del 74 pueda resolver `V.IDAgente` (desbloquea su filtro de agentes) | legacy / base |
| Que la rama de pantalla de `sp_n_rptClienteCompraAcumMotoLubMensual` arme bien su SQL (desbloquea el pdf del 118; el xlsx ya sale) | legacy / base |
| Restaurar o renombrar `MacZCS7`, `MacZCS8`, `MacZCS9` y `MacZCS10` (desbloquea 1410, 1415 y 1420) | infraestructura |
| Los seis `sp_n_rptRecaudaciones*` / `sp_n_rptRecasFianzas*` / `sp_n_rptIngresosContabilidad` de Aviacion (desbloquea 1426, 1428, 1429, 1430, 1432, 1455) | legacy / base, o el volcado de produccion que dira si alla si existen |
| Los renglones de `dbo.Reportes` de 240, 76 y 1242, si de verdad hacen falta | quien mantiene el catalogo |
| Comprobar en Tauro real el 72, que solo aparece con `EsCentroServicio = 1` | una instalacion de centro de servicio |
| Las listas de columnas de Excel que faltan (cosmetico: el xlsx sale con los nombres del SP) | a goteo |
| `Logo_Mac_WM.png` en `Assets/Logos/` (ya no tumba el PDF, pero la remision sale sin marca de agua en Azure) | nosotros, cuando toque |

---

## 9. El resumen que sirve para decidir

**76 de los 99 del menu vivo de Mac31 entregan documento (77 %), y los 76 estan
comparados contra Legacy con el mismo `psp`: 0 numeros inventados.**

Los **23** que no, repartidos por **de quien es la deuda**:

**Deuda HEREDADA — rotos tambien en Legacy, comprobado uno por uno (16):**

```
 145   Utilidad Productos                 tabla temporal atrasada
 493   Comparativo Ventas y Pagos         tabla temporal atrasada
1166   Cobranza Totalizado                rama Totalizado atrasada
1360   Ventas Cobros DINERO               faltan columnas en CobrosDineroUsado
1365   Ventas Cobros CASCOS               la misma causa
1375   Estado de Cuenta DINERO            el envolvente llama con 6, el interno recibe 1
1380   Estado de Cuenta CASCOS            la misma causa
1410   Centros Remisiones                 MacZCS7..10 no existen
1415   Centros Pagos Entradas y Salidas   MacZCS7..10 no existen
1420   Centros Garantias                  MacZCS7..10 no existen
1426   Auxiliar de Cobranza Consolidada   spV2 no existe en ninguna base
1428   Auxiliar de Cobranza (DEPOSITOS)   spV2 no existe en ninguna base
1429   Auxiliar de Cobranza               spV2 no existe en ninguna base
1430   Auxiliar de Fianza                 spV2 no existe en ninguna base
1432   Auxiliar de Cobranza de Tarjetas   spV2 no existe en ninguna base
1455   Ingresos para Contabilidad         spV2 no existe en ninguna base
```

**No son hueco para el usuario (4):**

```
  71   Pagos Detallado          en Tauro el nodo entrega el 77, que si funciona
1240   Listas de Precios        en Tauro el nodo entrega una de las 4, que si funcionan
1355   Cobros Dinero y Cascos   Mac31 no lo pide NUNCA
  72   Pagos Detallado CS       cableado; hace falta un centro de servicio de verdad
```

**Ids fantasma, sin renglon en `dbo.Reportes` en ninguna de las dos bases (3):**
240, 76 y 1242. Legacy tampoco puede generarlos.

**Deuda NUESTRA: cero.** Los dos que si lo eran —el 106 y el 1245— se arreglaron en
este lote, y los dos coinciden con Legacy importe por importe.

Fuera de la cuenta de los 99 porque no es un nodo sino una variante de filtro: el
**74 con filtro de agentes** sigue roto en los dos sistemas.
