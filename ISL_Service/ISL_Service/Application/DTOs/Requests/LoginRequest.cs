namespace ISL_Service.Application.DTOs.Requests;

public class LoginRequest
{
    public string Usuario { get; set; } = string.Empty;
    public string Contrasena { get; set; } = string.Empty;
    /*
      PIDE REFRESH TOKEN. YA NO ES "SOLO LA APP MOVIL".

      Aqui decia "Solo la app movil lo manda en true... El web no lo manda", y
      es FALSO desde que el web cambio: `Web/QR_Web/docs2/src/nucleo/api.ts:155`
      lo manda en TODOS sus logins, con su propio comentario explicando por que
      ("sin el la sesion muere a las 4 horas; la computadora de la puerta del
      checador se queda prendida todo el dia"). Nadie actualizo este comentario
      cuando paso.

      Se corrige porque era una trampa cargada: resolver el canal del token con
      esta senal —que es lo primero que uno piensa, y por eso se escribe aqui—
      habria convertido cada login del web en canal movil y el web entero
      habria empezado a pedir `app_movil.acceso`. Medido el 2026-10-15: dejaba
      fuera del navegador a 11 de 31 usuarios en Zaragoza y a 6 de 10 en Tauro.

      El canal se declara en `Canal`, aparte, y no se deduce de esto.
    */
    public bool IncluirRefresh { get; set; }

    /*
      DESDE DONDE SE ESTA ENTRANDO: "movil" o "web" (vacio = web).

      Lo declara el CLIENTE, pero solo aqui, en el login, y el servidor lo
      normaliza y lo estampa firmado en el token (CanalDeAcceso /
      JwtTokenGenerator). De ahi en adelante el canal ya no se pregunta nunca
      mas al cliente: se lee del claim, que no se puede reescribir sin la llave
      del servidor.

      Y declararlo NO DA NADA, solo quita:
        - "movil" obliga a tener `app_movil.acceso` en el servidor;
        - "web" (o no mandarlo) hace que las claves `app_movil.*` no cuenten.
      Por eso se puede dejar que lo diga el cliente sin abrir nada: las dos
      opciones cuestan, ninguna regala.
    */
    public string? Canal { get; set; }
}

public class RefreshRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
