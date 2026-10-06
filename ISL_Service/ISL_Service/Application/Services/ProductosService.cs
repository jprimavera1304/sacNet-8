using System.Globalization;
using ISL_Service.Application.DTOs.Productos;
using ISL_Service.Application.Interfaces;
using ISL_Service.Utils;

namespace ISL_Service.Application.Services;

/// <summary>
/// Normaliza los filtros del catalogo de productos.
///
/// ES EL UNICO SP DEL LOTE QUE LEGACY SI SANEABA: ProductoService.cs:43-44 hace
/// `Clave.Replace("'","").Trim()` y lo mismo con `Descripcion`. Se conserva, y
/// ademas se arregla lo que ahi reventaba: legacy llamaba `.Replace` sobre
/// campos que su propio DTO no inicializaba, asi que un body sin `clave` daba
/// NullReferenceException y 400. Aqui el ausente es cadena vacia.
/// </summary>
public class ProductosService : IProductosService
{
    private readonly IProductosRepository _repository;

    public ProductosService(IProductosRepository repository)
    {
        _repository = repository;
    }

    private static string Texto(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return string.Empty;
        return valor.Replace("'", string.Empty).Trim();
    }

    /// <summary>
    /// Convierte una lista de ids a la cadena separada por `~` que el SP parte
    /// con fn_DevuelveCadenaEnTabla.
    ///
    /// El 0 se descarta. Para `@IDsCategoria` y `@IDsGrupoCategoria` el SP tiene
    /// la guarda `<> '0'` y lo entiende como "todos", pero para `@IDsMarca` NO la
    /// tiene: un "0" ahi se vuelve `IDMarca IN (0)` y devuelve CERO renglones.
    /// Un filtro que silenciosamente vacia el catalogo es peor que uno que no
    /// filtra, asi que el 0 no viaja en ninguna de las tres.
    /// </summary>
    private static string Ids(IEnumerable<int>? valores)
    {
        var limpios = (valores ?? Array.Empty<int>()).Where(x => x != 0).Distinct();
        return string.Join(Variables.DELIMITADOR_PARAM_TILDE_SP, limpios);
    }

    public Task<List<ProductoDto>> ConsultarAsync(ProductosFiltro filtro, CancellationToken ct)
    {
        /*
          LOS PORCENTAJES DE DESCUENTO LOS ARMA EL BACKEND, NO EL CLIENTE.

          Legacy pasaba `@PorcentajesDescuento` TAL CUAL venia en el body: una
          cadena ya delimitada que el cliente tenia que saber separar con `~`.
          El SP la parte con fn_DevuelveCadenaEnTabla(..., '~') y los aplica en
          CASCADA, en el orden en que llegan, asi que el orden cambia el precio
          final. Dejar que el cliente arme la cadena es dejar que un error de
          formato se vuelva un precio equivocado sin ningun error visible.
          Aqui se recibe una lista de numeros y la cadena se arma aqui, con el
          punto decimal invariante (con la cultura del servidor en espanol,
          12.5 se habria escrito "12,5" y el SP habria leido dos descuentos).
        */
        var porcentajes = string.Join(
            Variables.DELIMITADOR_PARAM_TILDE_SP,
            (filtro.PorcentajesDescuento ?? Array.Empty<decimal>())
                .Select(x => x.ToString(CultureInfo.InvariantCulture)));

        return _repository.ConsultarAsync(new ProductosFiltroSp
        {
            IDProducto = filtro.IDProducto,
            IDsMarca = Ids(filtro.IDsMarca),
            IDsCategoria = Ids(filtro.IDsCategoria),
            IDsGrupoCategoria = Ids(filtro.IDsGrupoCategoria),
            IDStatus = filtro.IDStatus,
            Clave = Texto(filtro.Clave),
            Descripcion = Texto(filtro.Descripcion),
            Exacto = filtro.Exacto,
            ListaRoshfrans = filtro.ListaRoshfrans,
            EsCarga = filtro.EsCarga,
            PorcentajesDescuento = porcentajes
        }, ct);
    }
}
