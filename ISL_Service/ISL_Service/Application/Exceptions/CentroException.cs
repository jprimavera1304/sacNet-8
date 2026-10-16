namespace ISL_Service.Application.Exceptions;

/*
   EL 409 DE CENTROS, CON UN CODIGO QUE SE PUEDE RECONOCER

   `ConflictException` contesta siempre `code = "CONFLICTO"`, y eso vale para el
   servidor pero no para el cliente: el telefono tiene que distinguir "esta
   pantalla todavia no sabe de centros" -que se pinta como aviso con su motivo y
   un boton para volver a la matriz- de cualquier otro 409 de los que ya habia
   antes (por ejemplo el PDF de una factura que no esta en disco). Pintar ese
   como "cambia de centro" mandaria a la gente a tocar justo lo que no tiene que
   ver con el problema.

   Antes de esto el cliente lo adivinaba mirando si el texto del mensaje hablaba
   de "centro". Eso es fragil a proposito -lo dice su propio comentario- y se cae
   el dia que alguien reescriba una frase.

   Cada motivo lleva su codigo y TODOS empiezan por `CENTRO`, que es lo que el
   movil reconoce (`shared_mobile/lib/core/centros/centro_no_soportado.dart`).
   El `detail` sigue llevando la frase para soporte; el `message` es lo que se
   le ensena a la persona.
*/
public sealed class CentroException : ConflictException
{
    public CentroException(string codigo, string message, string? detail = null)
        : base(message, detail)
    {
        _codigo = codigo;
    }

    private readonly string _codigo;

    public override string Code => _codigo;

    /* Los nueve motivos, uno por rechazo de ConexionDelCentro. Son constantes y
       no cadenas sueltas para que renombrar uno se note en el compilador y no en
       un telefono. */
    public const string PeticionInvalida = "CENTRO_PETICION_INVALIDA";
    public const string ModuloNoSoporta = "CENTRO_MODULO_NO_SOPORTA";
    public const string OperacionSoloMatriz = "CENTRO_OPERACION_SOLO_MATRIZ";
    public const string MatrizSinOperar = "CENTRO_MATRIZ_SIN_OPERAR";
    public const string SinCambiar = "CENTRO_SIN_CAMBIAR";
    public const string NoExiste = "CENTRO_NO_EXISTE";
    public const string DeOtraEmpresa = "CENTRO_DE_OTRA_EMPRESA";
    public const string SinVer = "CENTRO_SIN_VER";
    public const string SinOperar = "CENTRO_SIN_OPERAR";
}
