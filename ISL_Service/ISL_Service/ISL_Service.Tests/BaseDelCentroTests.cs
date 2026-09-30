using ISL_Service.Infrastructure.Data;
using ISL_Service.Infrastructure.Repositories;
using Microsoft.Data.SqlClient;
using Xunit;

namespace ISL_Service.Tests;

/// <summary>
/// Apuntar la conexion a la base de otro centro. Es la pieza que decide a que
/// base se le pregunta, asi que aqui es donde importa que no acepte cualquier
/// cosa.
/// </summary>
public class BaseDelCentroTests
{
    private const string Cadena =
        "Server=srv;Database=MacZ;User Id=sa;Password=x;TrustServerCertificate=True;";

    [Fact]
    public void Cambia_La_Base_Y_No_Toca_Nada_Mas()
    {
        var nueva = new SqlConnectionStringBuilder(BaseDelCentro.Apuntar(Cadena, "MacZCS4"));
        var vieja = new SqlConnectionStringBuilder(Cadena);

        Assert.Equal("MacZCS4", nueva.InitialCatalog);
        /* Mismo servidor, mismo usuario, misma seguridad: solo el catalogo. */
        Assert.Equal(vieja.DataSource, nueva.DataSource);
        Assert.Equal(vieja.UserID, nueva.UserID);
        Assert.Equal(vieja.TrustServerCertificate, nueva.TrustServerCertificate);
    }

    [Fact]
    public void Sin_Centro_Se_Queda_En_La_Principal()
    {
        Assert.Equal(Cadena, BaseDelCentro.Apuntar(Cadena, null));
        Assert.Equal(Cadena, BaseDelCentro.Apuntar(Cadena, "   "));
    }

    [Theory]
    [InlineData("MacZ;Password=otra")]      // colaria opciones nuevas
    [InlineData("MacZ]")]                   // corchete suelto
    [InlineData("otra base")]               // espacio
    [InlineData("master.dbo")]              // punto
    [InlineData("")]
    public void Un_Nombre_Raro_No_Pasa(string malo)
    {
        Assert.False(BaseDelCentro.EsNombreDeBaseValido(malo));
        if (malo.Length > 0)
            Assert.Throws<ArgumentException>(() => BaseDelCentro.Apuntar(Cadena, malo));
    }

    [Theory]
    [InlineData("[MacZCS1].[dbo].", "MacZCS1")]
    [InlineData("[MacZCS12].[dbo].", "MacZCS12")]
    [InlineData("  [MacAviacionSC3].[dbo].  ", "MacAviacionSC3")]
    [InlineData("MacZCS7.dbo.", "MacZCS7")]   // por si alguien la captura sin corchetes
    [InlineData("", "")]
    public void BaseCentro_De_Legacy_Es_Un_Prefijo_De_SQL_No_Un_Nombre(string guardado, string esperado)
    {
        /* La columna guarda "[MacZCS1].[dbo]." para concatenarlo dentro de una
           consulta. Aqui hace falta el nombre pelado. */
        Assert.Equal(esperado, CentrosServicioRepository.NombreDeLaBase(guardado));
    }
}
