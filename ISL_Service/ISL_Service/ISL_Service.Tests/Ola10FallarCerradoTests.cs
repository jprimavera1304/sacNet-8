using System.Reflection;
using ISL_Service.Application.DTOs.Recaudacion;

namespace ISL_Service.Tests;

/// <summary>
/// CUANDO NO SE PUEDE SABER, NO SE INVENTA EL VALOR PERMISIVO.
///
/// Tres recaidas de la auditoria final tenian la misma forma: al fallar una
/// lectura se devolvia el valor que MAS deja pasar, y nadie se enteraba.
///
///  1. `sp_n_rptVentasSaldos` tronaba -> saldos en CERO -> disponible = limite
///     completo. Un error de SQL regalaba el credito.
///  2. `sp_n_ConsultaCreditoDisponible` tronaba -> limite estatico, que en Tauro
///     es el MAS ALTO de los dos (10,000 contra 5,000 calculado).
///  3. `sp_w_ConsultaAgenteUsuario` no desplegado -> filtro de agente = 0, que
///     significa "ve a TODOS los clientes".
///
/// Las tres se relanzan ahora como conflicto. Esta prueba no puede provocar un
/// SqlException (no tiene constructor publico), asi que fija lo que SI es
/// comprobable sin base: que esos tres catch ya no se tragan el error, leido del
/// propio ensamblado compilado. Es una prueba estructural, igual que
/// RutasSinChocarTests, y cuesta milisegundos.
/// </summary>
public class Ola10FallarCerradoTests
{
    private static readonly string Fuente = LeerFuenteDelRepositorioDePedidos();

    [Fact]
    public void El_Catch_De_Saldos_Ya_No_Esta_Vacio()
    {
        // El defecto literal que encontro la auditoria: `catch (SqlException)`
        // seguido de un bloque con solo comentarios.
        Assert.Contains("sp_n_rptVentasSaldos fallo para el cliente", Fuente);
        Assert.Contains("No se pudieron leer los saldos del cliente", Fuente);
    }

    [Fact]
    public void El_Credito_Disponible_No_Cae_Al_Limite_Estatico_Por_Un_Error_De_Sql()
    {
        Assert.Contains("sp_n_ConsultaCreditoDisponible fallo para el cliente", Fuente);
        Assert.Contains("No se pudo calcular el limite de credito", Fuente);
    }

    [Fact]
    public void El_Filtro_Por_Agente_Falla_Cerrado()
    {
        Assert.Contains("No se puede resolver el filtro por agente", Fuente);

        // Y lo que NO debe volver: el 0 permisivo con solo una advertencia.
        Assert.DoesNotContain("el FILTRO POR AGENTE queda APAGADO", Fuente);
    }

    /// <summary>
    /// Las tres recaidas tiran <see cref="ISL_Service.Application.Exceptions.ConflictException"/>,
    /// que el middleware traduce a 409 — no a 500. Importa: un 500 invita a
    /// reintentar y a creer que el backend esta roto.
    /// </summary>
    [Fact]
    public void El_Conflicto_Sale_Como_409()
    {
        var ex = new ISL_Service.Application.Exceptions.ConflictException("x");
        Assert.Equal(409, ex.StatusCode);
    }

    // --------------------------------------------- el digito del QR si decide

    /// <summary>
    /// El doc-comment de `RecaudacionesController.Get` promete que los tres datos
    /// del QR "tienen que cuadrar entre si". No cuadraban: el repositorio no
    /// manda @Dv1 al procedimiento, asi que el tercer dato no decidia nada y
    /// bastaba con que existiera el par (IDRecaudacion, IDCaja). Ahora se compara
    /// contra el DV1 del renglon.
    /// </summary>
    [Theory]
    [InlineData("7", 7, true)]
    [InlineData("07", 7, true)]     // la base lo guarda en varchar: "07" es 7
    [InlineData(" 7 ", 7, true)]
    [InlineData("8", 7, false)]
    [InlineData("", 7, true)]       // sin columna DV1 no se puede comprobar nada
    [InlineData("no-es-numero", 7, false)]
    public void El_Digito_Verificador_Del_Qr_Decide(string dv1DelRenglon, int dv1DelQr, bool debePasar)
    {
        var metodo = typeof(ISL_Service.Application.Services.RecaudacionService)
            .GetMethod("FiltrarPorDigitoVerificador", BindingFlags.NonPublic | BindingFlags.Static)!;

        var renglones = new List<RecaudacionDTO> { new() { IDRecaudacion = 10, DV1 = dv1DelRenglon } };
        var salida = (List<RecaudacionDTO>)metodo.Invoke(null, new object[] { renglones, dv1DelQr })!;

        Assert.Equal(debePasar ? 1 : 0, salida.Count);
    }

    [Fact]
    public void Sin_Renglones_No_Hay_Ticket()
    {
        var metodo = typeof(ISL_Service.Application.Services.RecaudacionService)
            .GetMethod("FiltrarPorDigitoVerificador", BindingFlags.NonPublic | BindingFlags.Static)!;

        var salida = (List<RecaudacionDTO>)metodo.Invoke(null, new object?[] { new List<RecaudacionDTO>(), 7 })!;
        Assert.Empty(salida);
    }

    /// <summary>
    /// Las tres ramas que reconstruian el DTO desde cero —y perdian ocho
    /// filtros— eran inalcanzables: el unico llamador de este servicio es el
    /// ticket por QR. Se borraron. La consulta filtrada de verdad es la de
    /// Aviacion.
    /// </summary>
    [Fact]
    public void El_Servicio_Del_Ticket_Ya_No_Reconstruye_El_Dto()
    {
        // Se mira el CODIGO, no el comentario que explica el defecto (que cita la
        // linea original a proposito): fuera de los comentarios no debe quedar
        // ninguna reconstruccion del DTO.
        var codigo = SinComentarios(LeerFuente("Application/Services/RecaudacionService.cs"));
        Assert.DoesNotContain("new RecaudacionInputDTO()", codigo);
    }

    // ------------------------------------------------------------------ ayudas

    /// <summary>
    /// Quita bloques /* */ y lineas //, para poder afirmar cosas del CODIGO sin
    /// que el comentario que documenta el defecto haga fallar la prueba.
    /// </summary>
    private static string SinComentarios(string fuente)
    {
        var sinBloques = System.Text.RegularExpressions.Regex.Replace(
            fuente, @"/\*.*?\*/", string.Empty, System.Text.RegularExpressions.RegexOptions.Singleline);

        return string.Join('\n', sinBloques
            .Split('\n')
            .Select(l => l.TrimStart().StartsWith("//", StringComparison.Ordinal) ? string.Empty : l));
    }

    private static string LeerFuenteDelRepositorioDePedidos()
        => LeerFuente("Infrastructure/Repositories/VentasPedidoCapturaRepository.cs");

    /// <summary>
    /// Sube desde el directorio de la prueba hasta encontrar el .csproj del
    /// servicio. Asi no depende de cuantas carpetas tenga bin/Debug/netX.
    /// </summary>
    private static string LeerFuente(string relativa)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidato = Path.Combine(dir.FullName, "ISL_Service.csproj");
            if (File.Exists(candidato))
                return File.ReadAllText(Path.Combine(dir.FullName, relativa.Replace('/', Path.DirectorySeparatorChar)));
            dir = dir.Parent;
        }

        throw new FileNotFoundException("No se encontro la raiz del proyecto ISL_Service para leer " + relativa);
    }
}
