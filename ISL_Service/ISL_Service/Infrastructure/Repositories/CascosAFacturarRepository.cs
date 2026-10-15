using ISL_Service.Application.Interfaces;

namespace ISL_Service.Infrastructure.Repositories;

/// El catalogo de tipos de casco con su clave del SAT. El por que de que sea un
/// repositorio aparte esta en <see cref="ICascosAFacturarRepository"/>.
public class CascosAFacturarRepository : SpLegacyRepositoryBase, ICascosAFacturarRepository
{
    /// El mismo procedimiento que usa la pantalla de usados, pero con la accion
    /// y la venta con las que lo llama la de facturacion.
    private const string Sp = "sp_n_VentasUsadosCredito";

    /// 2 = los entregados MAS el catalogo completo. Es la accion con la que
    /// `ConsultarActualCatalogo` lo llama en Mac31.
    private const int AccionCatalogo = 2;

    public CascosAFacturarRepository(IConfiguration configuration) : base(configuration) { }

    public async Task<List<CascoAFacturar>> ConsultarCatalogoAsync(CancellationToken ct)
    {
        /*
          `@IDVenta = 0` es lo que pide el catalogo sin atarlo a ninguna
          remision (VentasFacturacion.cs:325). Las cantidades que devuelva se
          ignoran: alla tambien se ponen en cero antes de pintarlas (:338-342),
          porque lo que se captura es cuantos cascos se van a FACTURAR, no
          cuantos trae la remision.
        */
        var filas = await EjecutarFilasAsync(Sp, cmd =>
        {
            cmd.Parameters.Add(Entero("@Accion", AccionCatalogo));
            cmd.Parameters.Add(Entero("@IDVenta", 0));
        }, ct);

        var lista = new List<CascoAFacturar>();

        foreach (var fila in filas)
        {
            var idTipoUsado = Entero(fila, "IDTipoUsado");
            if (idTipoUsado <= 0) continue;

            lista.Add(new CascoAFacturar
            {
                IdTipoUsado = idTipoUsado,
                Tipo = Texto(fila, "TipoUsadoCredito"),
                CostoConIva = Decimal(fila, "CostoVentaCreditoConIva"),
                ClaveSat = Texto(fila, "ClaveSat"),
                Orden = Entero(fila, "Orden")
            });
        }

        return lista.OrderBy(x => x.Orden).ThenBy(x => x.IdTipoUsado).ToList();
    }

    /// El procedimiento devuelve los decimales como `decimal`, pero el lector
    /// generico de la base solo tiene ayudas para entero y texto.
    private static decimal Decimal(Dictionary<string, object?> fila, string columna)
    {
        if (!fila.TryGetValue(columna, out var valor) || valor is null || valor == DBNull.Value)
            return 0m;

        return decimal.TryParse(Convert.ToString(valor), out var n) ? n : 0m;
    }
}
