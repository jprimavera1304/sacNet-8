# Ola 1 — Los 28 catálogos de solo lectura

Lote: las filas de `registro.tsv` con `estado = falta`, `escribe = no` y ruta que empieza por
`/api/cat`. Son los catálogos que MacServicios2 servía y el backend nuevo no tenía.

Fecha: 6-oct-2026. Base de las dos pruebas: `LAP-JUAND` / `Produccion_svr` (Tauro).
Legacy en `http://localhost:63479`, backend nuevo en `http://127.0.0.1:5071`.

## Lo que se hizo, en una frase

Un módulo nuevo (`CatalogosGenerales`) con **28 GET** bajo `api/catalogos`, que llaman a los
**mismos `sp_n_Consulta*`** que usaba Legacy, con los mismos parámetros, y devuelven el sobre
`{ ok, message, data }` con permiso `catalogos.*`. Ningún `sp_n_` se tocó; nada de la base se
modificó.

Archivos:

| archivo | qué |
|---|---|
| `Application/DTOs/CatalogosGenerales/CatalogosGeneralesDtos.cs` | los DTO de salida |
| `Application/Interfaces/ICatalogosGeneralesRepository.cs` | contrato de datos + los dos filtros grandes |
| `Application/Interfaces/ICatalogosGeneralesService.cs` | contrato de aplicación |
| `Application/Services/CatalogosGeneralesService.cs` | normaliza filtros (apóstrofo, defaults del SP) |
| `Infrastructure/Repositories/CatalogosGeneralesRepository.cs` | los 27 SP, lectura por nombre de columna |
| `Controllers/CatalogosGeneralesController.cs` | los 28 endpoints y sus políticas |
| `Program.cs` | dos `AddScoped` |
| `Infrastructure/Security/PermissionService.cs` | seis permisos nuevos en `PermissionCatalogSeeds` |
| `ISL_Service.Tests/CatalogosGeneralesServiceTests.cs` | 19 casos de prueba |

## La tabla

| capacidad | ruta legacy | ruta nueva | permiso | veredicto | notas |
|---|---|---|---|---|---|
| Estados | `POST /api/catestados/consultar` | `GET /api/catalogos/estados` | `catalogos.ver` | **OK** | 32 renglones iguales |
| Ciudades | `POST /api/catciudades/consultar` | `GET /api/catalogos/ciudades` | `catalogos.ver` | **OK** | el nuevo además **arregla** Legacy: sin estado, Legacy revienta |
| Municipios | `POST /api/catmunicipios/consultar` | `GET /api/catalogos/municipios` | `catalogos.ver` | **OK** | lo mismo que ciudades |
| Colonias | `POST /api/catcolonias/consultar` | `GET /api/catalogos/colonias` | `catalogos.ver` | **OK** | exige al menos un filtro (68,101 renglones) |
| Unidades territoriales | `POST /api/catunidadterritorial/consultar` | `GET /api/catalogos/unidades-territoriales` | `catalogos.ver` | no verificable | el SP no está en esta base; Legacy falla igual |
| FE regímenes fiscales | `POST /api/catFERegimenesFiscales/consultar` | `GET /api/catalogos/fe/regimenes-fiscales` | `catalogos.fe.ver` | **OK** | 23 renglones |
| FE relación de facturas | `POST /api/catFERelacionFacturas/consultar` | `GET /api/catalogos/fe/relaciones-factura` | `catalogos.fe.ver` | **OK** | 9 renglones |
| FE usos de CFDI | `POST /api/catFEUsosCFDI/consultar` | `GET /api/catalogos/fe/usos-cfdi` | `catalogos.fe.ver` | **OK** | 24 renglones |
| Agentes disponibles | `POST /api/catagentes/disponible` | `GET /api/catalogos/agentes-disponibles` | `catalogos.ver` | **OK** | el SP solo da `NumeroAgente` |
| Centros de servicio | `POST /api/catcentroservicio/consultar` | `GET /api/catalogos/centros-servicio` | `catalogos.ver` | **OK** | 10 renglones |
| Documentos | `POST /api/catdocumento/consultar` | `GET /api/catalogos/documentos` | `catalogos.ver` | no verificable | falta la tabla `[Catalogo Documentos]` en esta base |
| Grupos de cliente | `POST /api/catGruposCliente/consultar` | `GET /api/catalogos/grupos-cliente` | `catalogos.ver` | **OK** | 2 renglones |
| Puestos | `POST /api/catpuestos/consultar` | `GET /api/catalogos/puestos` | `catalogos.ver` | **OK** | 13 renglones |
| Tipos de nota de crédito | `POST /api/cattiponotascredito/consultar` | `GET /api/catalogos/tipos-nota-credito` | `catalogos.ver` | **OK** | sin auditoría |
| Tipos de pago | `POST /api/cattipopago/consultar` | `GET /api/catalogos/tipos-pago` | `catalogos.ver` | **OK** | 13 renglones; defaults 1/1 |
| Tipos de sueldo | `POST /api/cattiposueldo/consultar` | `GET /api/catalogos/tipos-sueldo` | `catalogos.ver` | **OK** | 2 renglones |
| Tipos de documento | `POST /api/catTiposDocumentos/consultar` | `GET /api/catalogos/tipos-documento` | `catalogos.ver` | **OK** | 7 renglones |
| Tipos de recordatorio | `POST /api/cattiporecordatorio/consultar` | `GET /api/catalogos/tipos-recordatorio` | `catalogos.ver` | **OK** | 7 renglones |
| Unidades de medida | `POST /api/catunidadesmedida/consultar` | `GET /api/catalogos/unidades-medida` | `catalogos.ver` | **OK** | 2 renglones |
| Galleros | `POST /api/catgalleros/consultar` | `GET /api/catalogos/galleros` | `catalogos.galleros.ver` | **OK** | catálogo vacío en Tauro; coinciden en el vacío |
| Categorías ↔ marcas | `POST /api/categoriamarcas/consultar` | `GET /api/catalogos/categorias/{idCategoria}/marcas` | `catalogos.marcas.ver` | **OK** | 75 renglones |
| Grupo-categorías ↔ marcas | `POST /api/catgruposcategorias/marcas/relacionar/consultar` | `GET /api/catalogos/grupos-categoria/marcas` | `catalogos.marcas.ver` | **OK** | 75 renglones; el SP se va a la base de Zaragoza si `Funcionalidad='ZARA'` |
| Formas de Mac31 | `POST /api/catformas/consultar` | `GET /api/catalogos/formas` | `catalogos.formas.ver` | no verificable | **Legacy nunca devolvió esto**: su DTO declara el nombre como `int` |
| Formas y procesos | `POST /api/catformasprocesos/consultar` | `GET /api/catalogos/formas-procesos` | `catalogos.formas.ver` | no verificable | **Legacy nunca devolvió esto**: manda un parámetro que el SP no tiene |
| Cajas | `POST /api/catcajas/consultar` | `GET /api/catalogos/cajas` | `catalogos.recaudaciones.ver` | no verificable | SP de recaudaciones, no instalado aquí |
| Conceptos | `POST /api/catconceptos/consultar` | `GET /api/catalogos/conceptos` | `catalogos.recaudaciones.ver` | no verificable | SP de recaudaciones, no instalado aquí |
| Siguiente número de cuenta | `POST /api/catconceptos/consultar/nextnumcuenta` | `GET /api/catalogos/conceptos/{idConcepto}/siguiente-numero-cuenta` | `catalogos.recaudaciones.ver` | no verificable | SP de recaudaciones, no instalado aquí |
| Económicos auxiliares | `POST /api/cateconomicos/auxiliares/consultar` | `GET /api/catalogos/economicos-auxiliares` | `catalogos.recaudaciones.ver` | no verificable | SP de recaudaciones, no instalado aquí |

**20 en verde, 8 no verificables.** En los 8, lo que no responde es **Legacy**: cinco por SP
ausente en `Produccion_svr`, uno por tabla ausente y dos por defectos propios de Legacy. Los 28
están implementados y los 28 tienen su caso escrito.

## Cómo se demuestra

```bash
cd migracion/comparador
NUEVO_BASE=http://127.0.0.1:5071 MAC_SCRATCH=<scratch> python comparar.py casos/
```

Los 20 verificables viven en `comparador/casos/catalogo-*.json` y salen **OK**.
Los 8 no verificables viven en **`comparador/casos/no-verificables/`**, con su `_porque`
explicando qué falla del lado de Legacy. Van en otra carpeta a propósito: `comparar.py casos/`
no entra en subcarpetas, así que la señal de la carpeta principal queda limpia sin esconder
nada. Cuando se corran contra una base que sí tenga esos SP (la de recaudaciones), se pasan a
`casos/`.

Pruebas automatizadas: `dotnet test ISL_Service/ISL_Service/ISL_Service.Tests/ISL_Service.Tests.csproj`
→ **109 pruebas, 0 fallos** (19 nuevas).

## Permisos nuevos

Seis, todos en `PermissionCatalogSeeds` (`PermissionService.cs`), módulo `catalogos`:

| clave | para qué | por qué no se reusó otro |
|---|---|---|
| `catalogos.ver` | desplegables de captura (territorial, puestos, tipos, unidades…) | — |
| `catalogos.fe.ver` | catálogos del SAT | solo los ve quien factura |
| `catalogos.galleros.ver` | galleros | el renglón trae **saldo**: es dinero |
| `catalogos.recaudaciones.ver` | cajas, conceptos, auxiliares | módulo que no todas las empresas tienen |
| `catalogos.formas.ver` | inventario de pantallas de Mac31 | quien lo lee administra seguridad |
| `catalogos.marcas.ver` | relación categoría ↔ marca | catálogo de productos |

No se reusó `ventas.consultar` (lo gobierna Mac31: darlo desde el web se borra en el siguiente
cálculo, sección 2.8 del resumen) ni `permisosweb.bootstrap` (significa "administrar
permisos", que no es esto). La familia `catalogos.*` es del **web**: no está entre las cuatro
familias que manda legacy, así que lo que se da desde la pantalla de permisos sí se respeta.

Los permisos quedan **sembrados en código**, no en la base: entran a `WPermiso` cuando alguien
llame a la sincronización del catálogo desde la pantalla de permisos. No se corrió ningún
script contra la base.

## Diferencias intencionales (las de fondo)

1. **POST → GET.** En Legacy eran POST porque su handler exigía el token dentro del JSON.
   Aquí el token va en la cabecera, así que una consulta vuelve a ser un GET.
2. **Fuera `result` y `mensaje` de cada renglón.** Eran el hueco de error de los SP de
   escritura; en una consulta salen 0/null siempre y el sobre ya dice si la llamada salió.
3. **Fuera la auditoría** (`IDUsuarioAlta`, `FechaAlta`, `EquipoAlta`, …) de tipos de nota de
   crédito, cajas, conceptos y auxiliares. Un desplegable no necesita saber quién capturó.
4. **Fuera los textos preconcatenados**: `NumeroNombreAgente`, `FEDescripcion`,
   `ObligatorioFtm`, los `*Ftm` en general. Los armaba la base para los combos de WinForms.
5. **Fuera `EquipoServidor` / `BaseServidor` / `EquipoLocal` / `BaseLocal`** de cajas:
   publicar el nombre del servidor y de la base en una respuesta de catálogo es regalar media
   cadena de conexión.
6. **Fuera `@Formato`** (el interruptor "0 todos los campos / 1 Excel" de Mac31) en centros de
   servicio y tipos de recordatorio: el Excel lo arma el front con los mismos datos.
7. **Texto recortado.** Varias columnas son `char(n)` y Legacy devuelve el relleno dentro
   (`"AGUASCALIENTES      "`). El relleno es del tipo de la columna, no un dato.
8. **Colonias exige al menos un filtro** → 400. Legacy servía los 68,101 renglones (~14 MB).
9. **`formas-procesos` acepta `idProceso` y lo ignora**, con la nota en el controlador.
   `sp_n_ConsultaFormas` no tiene ese parámetro ni devuelve procesos (viven en `n_Procesos` /
   `UsuarioFormaProceso`, que es lo que lee `PermissionService`). Mandarlo solo serviría para
   fallar igual que Legacy.
10. **Grupos de cliente no expone filtro por texto**: el SP recibe `@GrupoCliente` y no lo usa
    en ninguna rama. Ofrecerlo sería mentir.
11. **Relaciones de factura no expone filtro por status**: el `WHERE` del SP está clavado a
    `IDStatus = 1` aunque reciba el parámetro.

## Tres cosas que el backend nuevo arregla

No son diferencias de contrato, son defectos de Legacy que no se heredaron:

* **El apóstrofo.** Los `sp_n_` de catálogo arman el `WHERE` concatenando y lo corren con
  `EXEC(@Sql)`. Pasar el filtro como parámetro de `SqlCommand` **no** protege, porque el SP lo
  concatena adentro. Legacy quitaba el apóstrofo en cuatro servicios
  (`CatFERegimenFiscal`, `CatFEUsoCFDI`, `CatGrupoCliente`, `CatConcepto`) y se le olvidaba en
  ciudades, municipios, colonias, documentos, centros de servicio y tipos de recordatorio.
  Aquí se quita en todos, en `CatalogosGeneralesService.Texto`, con prueba.
* **Ciudades y municipios sin estado.** Legacy manda `@IDEstado = ''`, el SP lo concatena
  dentro de `fn_RegresaTablaEstados( )` y SQL Server contesta
  *"An insufficient number of arguments were supplied"*. El nuevo traduce el ausente a `"0"`,
  que es lo que la función entiende por "todos".
* **El catálogo de formas.** `CatFormaProcesoDTO` de Legacy declara `FormaNombre` y
  `FormaDescripcion` como `int` y el SP los devuelve `varchar`: su `DataTableToList` truena con
  *"El objeto de tipo 'System.String' no puede convertirse en el tipo 'System.Int32'"*.
  El nuevo los declara `string` y el endpoint funciona.

## Pendiente que queda anotado

* **`registro.tsv` no se tocó.** Las 28 filas siguen en `estado = falta`. Varios agentes
  estaban escribiendo ese archivo al mismo tiempo y reescribirlo entero habría podido pisar
  sus filas. Hay que pasarlas a `hecho`, con `ubicacion_nueva` = la ruta nueva de la tabla de
  arriba, `metodo_validacion` = `comparador`, y `resultado` = `OK` o `no verificable`.
* Los 8 casos de `casos/no-verificables/` hay que correrlos contra la base de recaudaciones
  (`MacAviacion*`) y contra una base que tenga `[Catalogo Documentos]`.
* Si alguna pantalla del web necesita **escribir** en uno de estos catálogos, no se agrega
  aquí: se crea su módulo con su `sp_w_` y su permiso `.crear`. Este controlador es solo
  lectura y así está documentado adentro.
