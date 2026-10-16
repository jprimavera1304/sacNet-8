# Claves de permiso de los centros de servicio

Este bloque lo integra **solo el agente de permisos** en
`Infrastructure/Security/PermissionService.cs` (contrato §8). Aquí se deja
escrito para que no haya que deducirlo del código.

Las claves se **nombran** en `Application/Security/ClavesDeCentros.cs`; úsense
esas constantes al sembrarlas, nunca el texto a mano. Escritas dos veces, el
día que cambie una dejarían de casar y nadie vería un error: simplemente el
selector se quedaría vacío.

## Las claves

| Clave | Nombre en pantalla | Qué hace |
|---|---|---|
| `centros.cambiar` | Centros - Cambiar de centro | Puede usar el selector. Sin esto todo sale de la matriz, por más centros que tenga concedidos. Es el interruptor de arriba. |
| `centros.<numero>.ver` | Centros - Consultar `<NOMBRE>` | Puede **leer** ese centro. Una por centro activo del catálogo. |
| `centros.<numero>.operar` | Centros - Operar en `<NOMBRE>` | Puede **escribir** en ese centro. |
| `centros.matriz.ver` | Centros - Consultar la matriz | Consultar la base principal. |
| `centros.matriz.operar` | Centros - Operar en la matriz | Escribir en la base principal. |

`<numero>` es `CentrosServicio.Numero` (2001..2012 en Zaragoza), **no**
`IDCentro`: Numero es lo que la gente reconoce (2004 = ORIENTE) y es el mismo
en las dos bases, mientras IDCentro es un consecutivo que puede coincidir entre
empresas apuntando a centros distintos — es exactamente lo que pasa hoy entre
`MacZ` y `Produccion_svr`.

## Lo que NO se siembra, y por qué

- **`centros.<n>.operar` no se concede a nadie al sembrar.** Escribir fuera de
  la matriz se reparte a mano desde Web. Si se sembrara concedido, doce mil
  permisos de consulta se convertirían en permisos de escritura que nadie
  decidió.
- **`centros.matriz.*` no se concede a nadie.** Hoy nadie las tiene y la matriz
  funciona para todos; el código lo respeta a propósito
  (`CapacidadesDeCentros.PuedeOperarEnLaMatriz`): mientras un usuario no tenga
  **ninguna** clave de matriz, la matriz se comporta como hoy. El día que
  alguien le configure una, esa clave empieza a gobernar. Si estas claves se
  sembraran *concedidas*, el modelo no cambiaría; si se sembraran como
  *requeridas sin conceder*, el día del despliegue la aplicación se quedaría en
  blanco para todo el mundo.

## Compatibilidad — es obligatoria

La familia vieja ya está repartida en las dos bases y **sigue valiendo**. Lo
resuelve `CapacidadesDeCentros`, no hace falta migrar ninguna fila:

- `reportes.centros.cambiar` → vale como `centros.cambiar`
- `reportes.centros.<n>.ver` → vale como `centros.<n>.ver`

No hay equivalente viejo de `operar` porque hoy no existe ninguna clave que
signifique "puede escribir en ese centro": no hay nada que heredar.

Las claves viejas **se quedan donde están**: las siembra hoy
`PermissionService.cs:4195-4215` bajo el módulo de reportes y las consume
`GET /api/reportes/ventas/centros`, que no se toca.

Probado en `ISL_Service.Tests/CentrosReglasTests.cs`
(`Quien_Hoy_Puede_Manana_Puede_Sin_Repartir_Nada`) y en
`CentrosLaCapaDeConexionTests.cs`
(`La_Clave_Vieja_De_Reportes_Sigue_Abriendo_El_Centro`).

## Un detalle de la puerta del módulo

La familia vieja `reportes.centros.*` tiene puerta de módulo
(`EntradasDeModulo.cs:105`: el prefijo `reportes.` exige
`reportes.ver_modulo`). La familia nueva `centros.*` **no** tiene puerta, y es
correcto: el selector de centro no es un módulo, es el contexto en el que se
abren los demás. Si se le pusiera una puerta habría que decidir cuál, y
cualquiera que se eligiera dejaría fuera a algún módulo declarado.
