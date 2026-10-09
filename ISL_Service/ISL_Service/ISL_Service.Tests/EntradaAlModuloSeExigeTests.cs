using ISL_Service.Application.Security;

namespace ISL_Service.Tests;

/// <summary>
/// QUE SIN LA ENTRADA AL MODULO NO SE PUEDA, AUNQUE TENGA LOS ESPECIFICOS.
///
/// POR QUE ESTA PRUEBA EXISTE
/// --------------------------
/// Se midio en la auditoria del 2026-10-09: de los 93 controladores,
/// NINGUNO comprobaba un ".ver_modulo". La capa de entrada vivia solo en el
/// front —cada tarjeta del inicio se enciende con su clave— asi que quien
/// conociera la URL entraba a un modulo que no tenia concedido, con solo
/// tener suelto cualquier permiso fino de adentro.
///
/// Es el tipo de agujero que no se ve probando: desde una cuenta de
/// administrador todo funciona, y desde una cuenta normal "funciona" tambien,
/// porque el menu esconde la puerta. Solo aparece cuando alguien escribe la
/// URL a mano.
/// </summary>
public class EntradaAlModuloSeExigeTests
{
    private static bool Puede(string clave, params string[] permisos) =>
        EntradasDeModulo.PuedeEntrar(clave, c => permisos.Contains(c, StringComparer.OrdinalIgnoreCase));

    [Theory]
    [InlineData("compras.cancelar", "compras.ver_modulo")]
    [InlineData("garantias.cancelar", "garantias.ver_modulo")]
    [InlineData("cortes.cancelar", "cortes.ver_modulo")]
    [InlineData("cambiosvigencia.cancelar", "cambiosvigencia.ver_modulo")]
    [InlineData("inventario.ajustes.cancelar", "inventario_ajustes.ver_modulo")]
    [InlineData("inventario.transito.procesar", "inventario_transito.ver_modulo")]
    [InlineData("facturacion.cancelar", "facturacion.ver_modulo")]
    [InlineData("cobros.pagos.cancelar", "cobros.ver_modulo")]
    [InlineData("obra.contratos.cerrar", "obra.ver_modulo")]
    [InlineData("aviacion.polizas.cancelar", "aviacion.ver_modulo")]
    public void El_Permiso_Fino_Solo_No_Alcanza(string fina, string entrada)
    {
        // Solo el permiso fino: la puerta esta cerrada.
        Assert.False(Puede(fina, fina));

        // Con la entrada, pasa.
        Assert.True(Puede(fina, fina, entrada));
    }

    /// <summary>
    /// La entrada de "inventario.ajustes.*" NO se llama "inventario.ver_modulo".
    /// Es el renglon que mas facil se habria roto si la entrada se dedujera del
    /// primer segmento de la clave: "inventario.ver_modulo" no existe en el
    /// catalogo, asi que TODO inventario habria contestado 403 a todo el mundo.
    /// </summary>
    [Fact]
    public void La_Entrada_No_Siempre_Se_Llama_Como_El_Prefijo()
    {
        Assert.Equal(new[] { "inventario_ajustes.ver_modulo" },
            EntradasDeModulo.Requeridas("inventario.ajustes.crear"));

        Assert.Equal(new[] { "inventario_transito.ver_modulo" },
            EntradasDeModulo.Requeridas("inventario.transito.procesar"));

        // Y la de ajustes no abre la de transito, ni al contrario.
        Assert.False(Puede("inventario.transito.procesar", "inventario.transito.procesar", "inventario_ajustes.ver_modulo"));
    }

    /// <summary>
    /// Las dos llaves duplicadas valen las dos. Mientras no se borre la que
    /// sobra, quien tenga la "mala" tiene que poder entrar igual.
    /// </summary>
    [Fact]
    public void Las_Entradas_Duplicadas_Valen_Las_Dos()
    {
        Assert.True(Puede("cambiosvigencia.crear", "cambiosvigencia.crear", "cambiosvigencia.ver_modulo"));
        Assert.True(Puede("cambiosvigencia.crear", "cambiosvigencia.crear", "cambios_vigencia.ver_modulo"));

        Assert.True(Puede("facturacion.timbrar", "facturacion.timbrar", "facturacion.ver_modulo"));
        Assert.True(Puede("facturacion.timbrar", "facturacion.timbrar", "facturas.ver_modulo"));
    }

    /// <summary>
    /// EL ".ver" YA NO VALE COMO ENTRADA: la rampa esta cerrada.
    /// </summary>
    /// <remarks>
    /// Esta prueba afirmaba lo contrario, y se invirtio a proposito. La rampa
    /// contradecia la regla ("entrada al modulo + permiso de la operacion") y
    /// dejaba entrar a quien tenia permisos finos sueltos y ninguna entrada:
    /// medido con QA_ESPECIFICOS, que con los siete "usuarios.*" y sin
    /// "usuarios.ver_modulo" recibia 200 y los 31 usuarios.
    ///
    /// Se puede cerrar sin quitarle acceso a nadie porque el snapshot DERIVA la
    /// entrada cuando Mac31 concede el ".ver" de una familia puenteada (ver
    /// PermissionService.FamiliasLegacy.cs). Lo que antes resolvia la rampa en
    /// la autorizacion hoy viene resuelto en la lista de permisos.
    /// </remarks>
    [Fact]
    public void El_Ver_Ya_No_Vale_Como_Entrada()
    {
        Assert.False(Puede("compras.cancelar", "compras.cancelar", "compras.ver"));
        Assert.False(Puede("garantias.editar", "garantias.editar", "garantias.ver"));

        // Con la entrada de verdad si pasa.
        Assert.True(Puede("compras.cancelar", "compras.cancelar", "compras.ver_modulo"));
        Assert.True(Puede("garantias.editar", "garantias.editar", "garantias.ver_modulo"));
    }

    /// <summary>
    /// Cerrar la rampa NO toca las entradas duplicadas: el recorrido sigue
    /// siendo sobre la lista completa, asi que cualquiera de las dos vale.
    /// Si esto se rompiera, a quien tenga la llave "mala" lo dejariamos fuera.
    /// </summary>
    [Fact]
    public void Cerrar_La_Rampa_No_Rompe_Las_Duplicadas()
    {
        Assert.True(Puede("cobros.aplicar", "cobros.aplicar", "cobros.ver_modulo"));
        Assert.True(Puede("cobros.aplicar", "cobros.aplicar", "dinero_usados.ver_modulo"));
        Assert.True(Puede("cambiosvigencia.crear", "cambiosvigencia.crear", "cambios_vigencia.ver_modulo"));
        Assert.True(Puede("facturacion.timbrar", "facturacion.timbrar", "facturas.ver_modulo"));

        // Y sin ninguna de las dos, no.
        Assert.False(Puede("cobros.aplicar", "cobros.aplicar"));
    }

    /// <summary>
    /// EL CANAL MOVIL NO SE TOCA. Sus claves no tienen tarjeta de inicio ni
    /// ".ver_modulo", y esta entrega es solo del web: si se les exigiera una
    /// puerta que no existe, la app se quedaria sin permisos de golpe.
    /// </summary>
    [Theory]
    [InlineData("app_movil.ventas.ver")]
    [InlineData("app_movil.empleados.crear")]
    [InlineData("app_movil.prestamos.cancelar")]
    [InlineData("app_movil.cascos_cambio.cancelar")]
    [InlineData("app_movil.asistencias.editar")]
    public void El_Movil_No_Pide_Entrada(string clave)
    {
        Assert.Empty(EntradasDeModulo.Requeridas(clave));
        Assert.True(Puede(clave, clave));
    }

    /// <summary>
    /// Una entrada no se pide a si misma: seria un ciclo y nadie entraria
    /// nunca a ningun modulo.
    /// </summary>
    [Theory]
    [InlineData("compras.ver_modulo")]
    [InlineData("garantias.ver_modulo")]
    [InlineData("ventas.ver_modulo")]
    [InlineData("reportes.ver_modulo")]
    public void Una_Entrada_No_Se_Pide_A_Si_Misma(string entrada)
    {
        Assert.Empty(EntradasDeModulo.Requeridas(entrada));
        Assert.True(Puede(entrada, entrada));
    }

    /// <summary>
    /// LAS FAMILIAS QUE A PROPOSITO NO SE GATEAN. Si alguien las agrega a la
    /// tabla sin pensarlo, esta prueba se lo dice y lo obliga a leer el por
    /// que esta escrito en EntradasDeModulo.
    /// </summary>
    [Theory]
    // No son una pantalla: son el diccionario que leen las demas.
    [InlineData("catalogos.ver")]
    [InlineData("catalogos.marcas.editar")]
    [InlineData("constantes.ver")]
    // Tiene dos candidatas y tres pantallas bajo el mismo prefijo.
    [InlineData("descuentos.simular")]
    [InlineData("descuentos.ajustes.autorizar")]
    // No tiene NINGUN ".ver_modulo" sembrado: inventarle uno dejaria el modulo
    // muerto para todos menos el SuperAdmin.
    [InlineData("cobranza.ver")]
    [InlineData("cobranza.cobros.cerrar")]
    // Plomeria y administracion del propio modelo de permisos.
    [InlineData("permisosweb.bootstrap")]
    [InlineData("seguridad.bitacora.registrar")]
    [InlineData("sincronizacion.ver")]
    [InlineData("huellas.editar")]
    [InlineData("empresas.ver")]
    public void Lo_Que_No_Se_Pudo_Comprobar_No_Se_Cierra(string clave)
    {
        Assert.Empty(EntradasDeModulo.Requeridas(clave));
        Assert.True(Puede(clave, clave));
    }

    [Fact]
    public void Una_Clave_Vacia_No_Revienta()
    {
        Assert.Empty(EntradasDeModulo.Requeridas(null));
        Assert.Empty(EntradasDeModulo.Requeridas(string.Empty));
        Assert.Empty(EntradasDeModulo.Requeridas("   "));
    }
}
