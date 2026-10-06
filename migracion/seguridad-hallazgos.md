# Agujeros de seguridad encontrados durante la migración

No son de la migración: estaban en el backend nuevo, en producción, y salieron
al inventariarlo. Se arreglan aquí porque un bug que aparece durante este
trabajo **amplía** el trabajo, no lo reduce.

## Arreglados

### `ProveedoresPagosController` estaba abierto de par en par — HECHO

Cuatro endpoints **sin `[Authorize]` ninguno**, y **tres de ellos escriben**:
insertar, actualizar y cancelar pagos a proveedores. Cualquiera que conociera la
URL podía mover dinero de proveedores sin identificarse.

Lo llamativo: **los permisos ya existían y nadie los había conectado.**
`PermissionService` declara y siembra `pagosproveedores.ver/crear/editar/cancelar`
desde que se hizo el módulo. Así que el arreglo no inventa un modelo nuevo, usa
el que estaba escrito y sin estrenar.

No rompe al web: su helper de red (`docs/shared/js/safety/api.js`) ya adjunta el
`Bearer` en toda petición cuando hay sesión. Comprobado contra el backend
levantado: **sin token 401, con token pasa**.

## Pendientes, con su plan

### `GET api/reportes/ventas/ReportesV3W` es anónimo y sin pase firmado

Lleva `[AllowAnonymous]` y lo único que pide es `psp`, **un entero secuencial**.
Quien pruebe números ve reportes de otros: ventas, saldos, cobranza.

Sus hermanos `/remision` y `/usados` **sí** están resueltos, y bien: usan
`Application/Security/RemisionTicket.cs`, un pase autocontenido y firmado con la
llave del JWT que dice qué se puede ver, para qué usuario y hasta cuándo, con
diez minutos de vigencia. El razonamiento de por qué el GET es anónimo —una
pestaña nueva del navegador no manda `Authorization`— está escrito ahí y es
correcto.

**El arreglo es aplicarle el mismo pase.** No hace falta tocar los fronts: el
web y la app abren la URL que les devuelve `generar`, así que basta con que esa
URL ya venga firmada.

> Se dejó para después de que terminara el mapeo de reportes, para no romperle
> las mediciones al agente que estaba probando generación de reportes contra ese
> mismo endpoint.

### `/dbcheck` anónimo revela servidor y base

Endpoint de diagnóstico en `Program.cs`. Útil en local, no en producción.
Debería exigir token, o existir solo en Development.

## Mirados y NO tocados, a propósito

### `GET api/usuarios/{id}/password` devuelve la contraseña en claro

Salió marcado como hallazgo, pero al leerlo **está bien resuelto**: exige el
permiso `usuarios.password.ver`, y el código explica por qué existe. Mac31
guarda las contraseñas en claro (`Usuarios.contrasenaDesc`) y quien administra
las necesita para dárselas a la gente. Es una consecuencia de los datos de
legacy, no un descuido.

Cambiarlo sin cambiar antes cómo Mac31 guarda contraseñas dejaría muerta una
función que hoy se usa. Queda anotado para cuando se ataque el tema de fondo.

## Lo que no se puede cerrar desde aquí

- **`Hope5y2k` en claro** en `Infrastructure/Data/Mac3SqlServerConnector.cs:46`.
- **`Jwt:Key`** y cadenas de conexión con contraseña versionadas en
  `appsettings.json`.
- El **token de Legacy** (`Legacy/Mac31/.../Variables.cs`), caducado desde 2020
  y que Legacy acepta igual porque **no verifica firma ni caducidad**.

Esto no se arregla con un commit: hay que **rotar las credenciales** y sacarlas
del repo a configuración del servidor. Mientras estén en el historial de git,
siguen expuestas aunque se borren del archivo.
