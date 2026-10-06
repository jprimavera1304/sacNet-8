# Cómo hablarle al Legacy local (MacServicios2)

Esto costó encontrarlo. Queda escrito para no repetirlo.

## Levantarlo

No se toca ningún archivo de Legacy. Se arranca IIS Express apuntando a la
carpeta del proyecto, con su propia configuración:

```bash
"/c/Program Files/IIS Express/iisexpress.exe" \
  /path:"D:\JuanD\GitHub\MAC\Legacy\MacServicios2\Mac3Servicios" \
  /port:63479 /clr:v4.0
```

El `.vs/.../applicationhost.config` del repo apunta a una ruta vieja
(`D:\JuanD\GitHub\MacServicios2`) que ya no existe. Por eso se usa `/path:`:
así no hay que editar nada de Legacy.

Ya está compilado (`bin/Mac3Servicios.dll`), no hace falta build.

## La base a la que pega

La que diga su `Web.config` (hoy `LAP-JUAND` / `Produccion_svr`, sa/123).

**El back nuevo se apunta a ESA misma base, no al revés.** Legacy es la
referencia: no se le mueve nada, ni su configuración.

## Autenticación: son DOS cosas, no una

Aquí estuvo la trampa. Para que una petición pase hacen falta **las dos**:

1. **Cabecera `Authorization`** con cualquier valor no vacío.
   `TokenValidationHandler` (un `DelegatingHandler` global, registrado en
   `App_Start/WebApiConfig.cs:15`) rechaza con `"No autorizado."` toda URL que
   no contenga `authtoken` si no viene la cabecera. **Ni siquiera mira el
   contenido**: solo que exista.
2. **`token` y `correoElectronicoToken` DENTRO del cuerpo JSON.** Eso es lo
   que el servicio sí valida.

Los dos mensajes de rechazo se parecen y es fácil confundirlos:

| Mensaje | Quién lo manda | Qué falta |
|---|---|---|
| `"No autorizado."` (con punto) | el handler global | la cabecera `Authorization` |
| `"No autorizado"` (sin punto) | el servicio | el token del cuerpo |

## El token

El que usa Mac31, sacado de `Legacy/Mac31/Mac31/Utils/Variables.cs`:

- `APP_TOKEN` — un JWT **caducado desde 2020**
- `APP_CORREO_ELECTRONICO` = `app_web@tauro.com`

Sirve igual, y esto hay que saberlo: **Legacy no verifica la firma ni la
caducidad.** `readJwtTokenAsync` hace `new JwtSecurityToken(encodedString)`,
que solo DECODIFICA; lee `ValidTo` pero nunca lo compara. Así que cualquier JWT
con los claims correctos entra.

> **Esto es un agujero de seguridad real de Legacy.** No se arregla aquí
> (Legacy no se toca, y se va a apagar), pero el backend nuevo **no debe
> heredarlo**: ahí la firma y la caducidad sí se validan de verdad.

Claims que exige: `given_name` y `family_name` no vacíos, `email` igual al
`correoElectronicoToken` del cuerpo, `empresa` igual a `JWT_EMPRESA` del
Web.config (`ITSYSWARE GLOBAL`), y al menos uno de `idApp`/`idCliente`/
`idUsuario` mayor que cero.

## Llamada de ejemplo que funciona

```bash
TOK=$(sed -n 's/.*APP_TOKEN = "\([^"]*\)".*/\1/p' \
      /d/JuanD/GitHub/MAC/Legacy/Mac31/Mac31/Utils/Variables.cs)

curl -s -X POST http://localhost:63479/api/catagentes/consultar \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOK" \
  -d "{\"token\":\"$TOK\",\"correoElectronicoToken\":\"app_web@tauro.com\"}"
```

Responde `{"id":0,"status":200,"result":{"agentes":[...]}}` con datos reales.

## La forma de la respuesta

Legacy siempre envuelve: `{"id":..,"status":..,"result":..}`, y dentro de
`result` una propiedad con el nombre de la colección (`agentes`, `clientes`…).
El back nuevo usa `{"ok":..,"message":..,"data":..}`.

**Esa diferencia es a propósito y no se compara.** El comparador contrasta los
DATOS de adentro, no la cáscara.
