using System.Data;
using ISL_Service.Application.DTOs.Reportes;
using ISL_Service.Application.Interfaces;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Infrastructure.Reports;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

public partial class ReportesVentasRepository
{
    private static async Task<List<ReportesVentasCatalogoItem>> ConsultarEmpresasAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaEmpresas", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };
        cmd.Parameters.AddWithValue("@IDEmpresa", 0);
        cmd.Parameters.AddWithValue("@IDStatus", 1);
        cmd.Parameters.AddWithValue("@RazonSocial", "");
        cmd.Parameters.AddWithValue("@Formato", 0);
        cmd.Parameters.AddWithValue("@Identico", 0);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return table.AsEnumerable()
            .Select(row => new ReportesVentasCatalogoItem
            {
                Id = ReadInt(row, "IDEmpresa"),
                Nombre = ReadString(row, "NombreCorto", "RazonSocial", "Empresa"),
                Clave = ReadString(row, "Clave", "NombreCorto")
            })
            .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Nombre))
            .ToList();
    }

    private static async Task<List<ReportesVentasCatalogoItem>> ConsultarAlmacenesAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaAlmacenes", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };
        cmd.Parameters.AddWithValue("@IDAlmacen", 0);
        cmd.Parameters.AddWithValue("@IDStatus", 1);
        cmd.Parameters.AddWithValue("@Almacen", "");
        cmd.Parameters.AddWithValue("@Identico", 0);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return table.AsEnumerable()
            .Select(row => new ReportesVentasCatalogoItem
            {
                Id = ReadInt(row, "IDAlmacen"),
                Nombre = ReadString(row, "Almacen"),
                Clave = ReadString(row, "Almacen")
            })
            .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Nombre))
            .ToList();
    }

    private static async Task<List<ReportesVentasCatalogoItem>> ConsultarAgentesAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaAgentes", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };
        cmd.Parameters.AddWithValue("@IDAgente", 0);
        cmd.Parameters.AddWithValue("@IDStatus", 1);
        cmd.Parameters.AddWithValue("@NumeroAgente", 0);
        cmd.Parameters.AddWithValue("@NombreAgente", "");
        cmd.Parameters.AddWithValue("@Formato", 0);
        cmd.Parameters.AddWithValue("@Identico", 0);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return table.AsEnumerable()
            .Select(row => new ReportesVentasCatalogoItem
            {
                Id = ReadInt(row, "IDAgente"),
                Nombre = ReadString(row, "NombreAgente"),
                Clave = ReadString(row, "NumeroAgente")
            })
            .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Nombre))
            .OrderBy(x => x.Nombre)
            .ToList();
    }

    private static async Task<List<ReportesVentasCatalogoItem>> ConsultarUsuariosAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaUsuarios", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };
        cmd.Parameters.AddWithValue("@IDUsuario", 0);
        cmd.Parameters.AddWithValue("@IDPerfil", 0);
        cmd.Parameters.AddWithValue("@IDStatus", 1);
        cmd.Parameters.AddWithValue("@Usuario", "");
        cmd.Parameters.AddWithValue("@Identico", 0);
        cmd.Parameters.AddWithValue("@Formato", 0);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return table.AsEnumerable()
            .Select(row => new ReportesVentasCatalogoItem
            {
                Id = ReadInt(row, "IDUsuario"),
                Nombre = ReadString(row, "Usuario", "Nombre"),
                Clave = ReadString(row, "Usuario")
            })
            .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Nombre))
            .OrderBy(x => x.Nombre)
            .ToList();
    }

    private static async Task<List<ReportesVentasCatalogoItem>> ConsultarRepartidoresAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaRepartidores", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };
        cmd.Parameters.AddWithValue("@IDRepartidor", 0);
        cmd.Parameters.AddWithValue("@IDZona", 0);
        cmd.Parameters.AddWithValue("@IDStatus", 1);
        cmd.Parameters.AddWithValue("@Repartidor", "");
        cmd.Parameters.AddWithValue("@Identico", 0);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return table.AsEnumerable()
            .Select(row => new ReportesVentasCatalogoItem
            {
                Id = ReadInt(row, "IDRepartidor"),
                Nombre = ReadString(row, "Repartidor"),
                Clave = ReadString(row, "Repartidor")
            })
            .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Nombre))
            .OrderBy(x => x.Nombre)
            .ToList();
    }

    private static async Task<List<ReportesVentasCatalogoItem>> ConsultarProveedoresAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaProveedores", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };
        cmd.Parameters.AddWithValue("@IDProveedor", 0);
        cmd.Parameters.AddWithValue("@IDCondicionesCredito", 0);
        cmd.Parameters.AddWithValue("@IDDescuento", 0);
        cmd.Parameters.AddWithValue("@IDStatus", 1);
        cmd.Parameters.AddWithValue("@Numero", "");
        cmd.Parameters.AddWithValue("@Nombre", "");
        cmd.Parameters.AddWithValue("@ApellidoPaterno", "");
        cmd.Parameters.AddWithValue("@ApellidoMaterno", "");
        cmd.Parameters.AddWithValue("@RFC", "");
        cmd.Parameters.AddWithValue("@Formato", 0);
        cmd.Parameters.AddWithValue("@Identico", 0);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return table.AsEnumerable()
            .Select(row => new ReportesVentasCatalogoItem
            {
                Id = ReadInt(row, "IDProveedor"),
                Nombre = ReadString(row, "NombreProveedor", "Proveedor", "Nombre"),
                Clave = ReadString(row, "Numero", "RFC")
            })
            .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Nombre))
            .OrderBy(x => x.Nombre)
            .ToList();
    }

    private static async Task<List<ReportesVentasCatalogoItem>> ConsultarAutosAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaAutos", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };
        cmd.Parameters.AddWithValue("@IDAuto", 0);
        cmd.Parameters.AddWithValue("@IDStatus", 1);
        cmd.Parameters.AddWithValue("@Nombre", "");

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return table.AsEnumerable()
            .Select(row => new ReportesVentasCatalogoItem
            {
                Id = ReadInt(row, "IDAuto"),
                Nombre = ReadString(row, "Nombre", "Auto"),
                Clave = ReadString(row, "Placas", "Nombre")
            })
            .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Nombre))
            .OrderBy(x => x.Nombre)
            .ToList();
    }

    /*
      LOS TIPOS DE PAGO DEL REPORTE DE PAGOS (71/75/77), Y POR QUE SE LEEN AQUI.

      El filtro existe: es `gridTiposPago` de Mac31, que se llena con
      `Consultas.ConsultarTiposPago` pasando `EsPago = 1`
      (ReportesMenu.cs:474-489) y arranca con TODOS marcados. Sus ids viajan al
      SP en `Param3`, separados por "|".

      Ya habia un endpoint que los sabe dar -`GET /api/catalogos/tipos-pago`-
      pero exige la politica `perm:catalogos.ver`, y quien tiene reportes NO
      tiene por que tener catalogos: el filtro contestaria 403 y el campo se
      quedaria vacio sin explicar nada. Es la misma forma del fallo que dejo 68
      capacidades contestando 403 a todo el que no fuera SuperAdmin. Asi que se
      leen con el MISMO procedimiento desde aqui, donde el candado es
      `reportes.ver_modulo`, que es el que ya hace falta para pedir el reporte.

      `@EsPago = 1` es el filtro de Mac31 y no es cosmetico: la tabla
      `[Catalogo Tipos Pagos]` trae dieciocho renglones y seis de ellos son
      DESCUENTOS (3, 4, 6, 8, 12) e INCOBRABLE (18), que no son formas de pago y
      no pertenecen a este filtro.
    */
    private static async Task<List<ReportesVentasCatalogoItem>> ConsultarTiposPagoAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaTiposPagos", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };
        cmd.Parameters.AddWithValue("@IDTipoPago", 0);
        cmd.Parameters.AddWithValue("@IDStatus", 1);
        cmd.Parameters.AddWithValue("@TipoPago", "");
        cmd.Parameters.AddWithValue("@Identico", 0);
        cmd.Parameters.AddWithValue("@EsPago", 1);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return table.AsEnumerable()
            .Select(row => new ReportesVentasCatalogoItem
            {
                Id = ReadInt(row, "IDTipoPago"),
                Nombre = ReadString(row, "TipoPago"),
                Clave = ReadString(row, "TipoPagoCorto", "TipoPago")
            })
            .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Nombre))
            .ToList();
    }

    private static async Task<List<ReportesVentasCatalogoItem>> ConsultarTiposGastoAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaTiposGastos", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };
        cmd.Parameters.AddWithValue("@IDTipoGasto", 0);
        cmd.Parameters.AddWithValue("@IDStatus", 1);
        cmd.Parameters.AddWithValue("@TipoGasto", "");
        cmd.Parameters.AddWithValue("@Formato", 0);
        cmd.Parameters.AddWithValue("@Identico", 0);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return table.AsEnumerable()
            .Select(row => new ReportesVentasCatalogoItem
            {
                Id = ReadInt(row, "IDTipoGasto"),
                Nombre = ReadString(row, "TipoGasto"),
                Clave = ReadString(row, "TipoGasto")
            })
            .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Nombre))
            .OrderBy(x => x.Nombre)
            .ToList();
    }


    /*
      @IDEmpresaCS IBA CABLEADO EN 1, Y 1 ES ZARAGOZA.

      Lo que decia era `cmd.Parameters.AddWithValue("@IDEmpresaCS", 1);` -el unico
      filtro de este metodo que no iba en 0- y el procedimiento SI lo usa: cuando
      @IDCliente es 0 agrega `AND C.IDEmpresaCS = @IDEmpresaCS` (linea 126 de la
      definicion de sp_n_ConsultaClientes). No es "0 = todas": es una igualdad
      siempre.

      El numero correcto esta en la base, en `Constantes.IDEmpresaCS`, que es
      justo "que empresa es esta instalacion". Comprobado leyendolo:

          Produccion_svr (Tauro)   -> IDEmpresaCS = 0
          MacZ           (Zaragoza)-> IDEmpresaCS = 1

      O sea que el 1 cableado era el valor de Zaragoza aplicado tambien a Tauro.
      En Tauro recortaba el padron a los clientes marcados como de Zaragoza y en
      Zaragoza dejaba fuera los de CS1..CS5 y LURO. Sin error y sin aviso: una
      lista mas corta, que es el peor modo de equivocarse.

      Ahora sale de Constantes. El parametro sigue existiendo por si algun dia hay
      que pedir el padron de otra empresa; quien no lo pasa, obtiene el de la
      instalacion que esta contestando.
    */
    private static async Task<List<ReportesVentasClienteItem>> ConsultarClientesAsync(
        SqlConnection conn,
        int? numero,
        int? idCliente,
        CancellationToken ct,
        int? idEmpresaCs = null)
    {
        var empresaCs = idEmpresaCs
            ?? (await ConsultarConstantesAsync(conn, ct)).IDEmpresaCS;

        await using var cmd = new SqlCommand("sp_n_ConsultaClientes", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };

        cmd.Parameters.AddWithValue("@IDCliente", idCliente.GetValueOrDefault());
        cmd.Parameters.AddWithValue("@IDCondicionesCredito", 0);
        cmd.Parameters.AddWithValue("@IDCondicionesCreditoAceites", 0);
        cmd.Parameters.AddWithValue("@IDCondicionesCreditoCascos", 0);
        cmd.Parameters.AddWithValue("@IDDescuento", 0);
        cmd.Parameters.AddWithValue("@IDAgente", 0);
        cmd.Parameters.AddWithValue("@IDStatus", 0);
        cmd.Parameters.AddWithValue("@Numero", numero.GetValueOrDefault() > 0 ? numero.GetValueOrDefault().ToString() : "");
        cmd.Parameters.AddWithValue("@Nombre", "");
        cmd.Parameters.AddWithValue("@ApellidoPaterno", "");
        cmd.Parameters.AddWithValue("@ApellidoMaterno", "");
        cmd.Parameters.AddWithValue("@RFC", "");
        cmd.Parameters.AddWithValue("@IDTipoCliente", 0);
        cmd.Parameters.AddWithValue("@ConHuella", 0);
        cmd.Parameters.AddWithValue("@IDEmpresaCS", empresaCs);
        cmd.Parameters.AddWithValue("@EsMostrador", 0);
        cmd.Parameters.AddWithValue("@Referencia", "");
        cmd.Parameters.AddWithValue("@Formato", 0);
        cmd.Parameters.AddWithValue("@Identico", 0);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return table.AsEnumerable()
            .Select(row => new ReportesVentasClienteItem
            {
                IDCliente = ReadInt(row, "IDCliente"),
                Numero = ReadInt(row, "Numero"),
                NombreCliente = ReadString(row, "NombreCliente"),
                Nombre = ReadString(row, "Nombre")
            })
            .Where(x => x.IDCliente > 0 && x.Numero > 0)
            .ToList();
    }

    private static async Task<List<ReportesVentasCatalogoItem>> ConsultarGruposCategoriasAsync(SqlConnection conn, CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaGrupoCategorias", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };
        cmd.Parameters.AddWithValue("@IDsGrupoCategoria", "");
        cmd.Parameters.AddWithValue("@IDStatus", 1);
        cmd.Parameters.AddWithValue("@GrupoCategoria", "");
        cmd.Parameters.AddWithValue("@Identico", 0);
        cmd.Parameters.AddWithValue("@Formato", 0);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return table.AsEnumerable()
            .Select(row => new ReportesVentasCatalogoItem
            {
                Id = ReadInt(row, "IDGrupoCategoria"),
                Nombre = ReadString(row, "GrupoCategoria"),
                Clave = ReadString(row, "GrupoCategoria")
            })
            .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Nombre))
            .ToList();
    }

    private static async Task<List<ReportesVentasProductoItem>> ConsultarSubcategoriasAsync(
        SqlConnection conn,
        int idGrupoCategoria,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaCategorias", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };
        cmd.Parameters.AddWithValue("@IDsCategoria", "");
        cmd.Parameters.AddWithValue("@IDsGrupoCategoria", idGrupoCategoria.ToString());
        cmd.Parameters.AddWithValue("@IDStatus", 1);
        cmd.Parameters.AddWithValue("@Categoria", "");
        cmd.Parameters.AddWithValue("@Identico", 0);
        cmd.Parameters.AddWithValue("@Formato", 0);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return table.AsEnumerable()
            .Select(row => new ReportesVentasProductoItem
            {
                IDGrupoCategoria = ReadInt(row, "IDGrupoCategoria"),
                GrupoCategoria = ReadString(row, "GrupoCategoria"),
                IDCategoria = ReadInt(row, "IDCategoria"),
                Categoria = ReadString(row, "Categoria")
            })
            .Where(x => x.IDGrupoCategoria > 0 && x.IDCategoria > 0 && !string.IsNullOrWhiteSpace(x.Categoria))
            .ToList();
    }

    private static async Task<List<ReportesVentasProductoItem>> ConsultarMarcasPorCategoriasAsync(
        SqlConnection conn,
        int idGrupoCategoria,
        IReadOnlyCollection<int> idCategorias,
        CancellationToken ct)
    {
        await using var cmd = new SqlCommand("sp_n_ConsultaGrupoCategoriasMarcas", conn)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 500
        };
        cmd.Parameters.AddWithValue("@IDsMarca", "");
        cmd.Parameters.AddWithValue("@IDsCategoria", JoinIds(idCategorias));
        cmd.Parameters.AddWithValue("@IDsGrupoCategoria", idGrupoCategoria.ToString());
        cmd.Parameters.AddWithValue("@IDStatus", 1);
        cmd.Parameters.AddWithValue("@Marca", "");
        cmd.Parameters.AddWithValue("@Identico", 0);
        cmd.Parameters.AddWithValue("@Formato", 0);

        var table = await ExecuteFirstTableAsync(cmd, ct);
        return table.AsEnumerable()
            .Select(row => new ReportesVentasProductoItem
            {
                IDGrupoCategoria = ReadInt(row, "IDGrupoCategoria"),
                GrupoCategoria = ReadString(row, "GrupoCategoria"),
                IDCategoria = ReadInt(row, "IDCategoria"),
                Categoria = ReadString(row, "Categoria"),
                IDMarca = ReadInt(row, "IDMarca"),
                Marca = ReadString(row, "Marca")
            })
            .Where(x => x.IDGrupoCategoria > 0 && x.IDCategoria > 0 && x.IDMarca > 0 && !string.IsNullOrWhiteSpace(x.Marca))
            .ToList();
    }

    private static List<ReportesVentasCatalogoItem> BuildDocumentos()
    {
        return new List<ReportesVentasCatalogoItem>
        {
            new() { Id = 10, Nombre = "VENTAS", Clave = "ventas" },
            new() { Id = 1, Nombre = "MAYORISTAS", Clave = "mayoristas" },
            new() { Id = 2, Nombre = "AJUSTES", Clave = "ajustes" },
            new() { Id = 3, Nombre = "LOCALES", Clave = "locales" },
            new() { Id = 4, Nombre = "DEVOLUCIONES", Clave = "devoluciones" }
        };
    }

    private static List<ReportesVentasCatalogoItem> BuildDocumentosConTodos()
    {
        return new List<ReportesVentasCatalogoItem>
        {
            new() { Id = 0, Nombre = "TODOS", Clave = "todos" },
            new() { Id = 10, Nombre = "VENTAS", Clave = "ventas" },
            new() { Id = 1, Nombre = "MAYORISTAS", Clave = "mayoristas" },
            new() { Id = 3, Nombre = "LOCALES", Clave = "locales" },
            new() { Id = 2, Nombre = "AJUSTES", Clave = "ajustes" },
            new() { Id = 4, Nombre = "DEVOLUCIONES", Clave = "devoluciones" }
        };
    }

    private static List<ReportesVentasCatalogoItem> BuildStatusFolios()
    {
        return new List<ReportesVentasCatalogoItem>
        {
            new() { Id = 0, Nombre = "TODOS", Clave = "todos" },
            new() { Id = 1, Nombre = "VIGENTES", Clave = "vigentes" },
            new() { Id = 2, Nombre = "CANCELADAS", Clave = "cancelados" },
            new() { Id = 4, Nombre = "SALDO A FAVOR", Clave = "saldo_favor" }
        };
    }
}
