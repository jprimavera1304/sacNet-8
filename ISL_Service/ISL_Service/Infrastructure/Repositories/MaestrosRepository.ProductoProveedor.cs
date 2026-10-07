using System.Globalization;
using ISL_Service.Application.DTOs.Empleados;
using ISL_Service.Application.DTOs.Maestros;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// La ficha del producto y la del proveedor, con sus apoderados, domicilios y
/// documentos.
/// </summary>
public partial class MaestrosRepository
{
    // =======================================================================
    // PRODUCTO
    // =======================================================================

    /// <summary>
    /// Las columnas que escribe el UPDATE de sp_n_ActualizarProducto y que hay
    /// que acarrear en una baja. Incluye el STOCK y los TRES PRECIOS: si no se
    /// acarrean, dar de baja un producto lo deja con existencia cero y precio
    /// cero, y el historico de compras deja de cuadrar contra el catalogo.
    /// </summary>
    private static readonly BajaLogicaLegacy.Descriptor StatusProducto = new(
        "Productos", "IDProducto", "@IDProducto", "dbo.sp_n_ActualizarProducto",
        new[]
        {
            ("@IDCategoria", "IDCategoria"),
            ("@IDMarca", "IDMarca"),
            ("@IDTipoUsado", "IDTipoUsado"),
            ("@Codigo", "Codigo"),
            ("@CodigoBarras", "Codigo de Barras"),
            ("@Producto", "Producto"),
            ("@Descripcion", "Descripcion"),
            ("@DescripcionSat", "DescripcionSat"),
            ("@PrecioLista", "Precio de Lista"),
            ("@PrecioLista2", "PrecioLista2"),
            ("@PrecioListaCS", "PrecioListaCS"),
            ("@Stock", "Stock"),
            ("@StockMinimo", "Stock Minimo"),
            ("@StockMaximo", "Stock Maximo"),
            ("@Observaciones", "Observaciones"),
            ("@PesoKg", "PesoKg"),
            ("@UnidadMedida", "UnidadMedida"),
            ("@ClaveSat", "ClaveSat"),
            ("@IDFacturama", "IDFacturama"),
            ("@IDUnidadMedida", "IDUnidadMedida"),
            ("@NumPiezas", "NumPiezas"),
            ("@Litros", "Litros"),
            ("@MostrarLista", "MostrarLista"),
            ("@EsCarga", "EsCarga")
        },
        PideUsuario: true,
        PideEquipo: true);

    /// <summary>
    /// Alta de producto.
    ///
    /// DOS COSAS QUE NO SE HEREDAN DE LEGACY:
    ///  1. El alta de legacy llamaba antes a sp_n_ConsultaConstantes para leer
    ///     PorcentajeIVA y NO LO USABA: el unico consumidor era un bloque de
    ///     Facturapi que esta comentado entero. Si esa consulta fallaba, el alta
    ///     fallaba. Aqui no se llama.
    ///  2. sp_n_InsertarProducto no recibe @IDUsuario ni @Equipo, asi que el alta
    ///     de un producto no deja firma en la base. No es algo que se pueda
    ///     arreglar sin tocar el procedimiento, y los sp_n_ no se tocan; queda
    ///     anotado: en Productos la columna de alta no dice quien lo dio de alta
    ///     desde el web igual que no lo dice desde Mac31.
    /// </summary>
    public Task<(ResultadoLegacy, int)> InsertarProductoAsync(CrearProductoRequest r, CancellationToken ct)
        => InsertarConUltimoIdAsync("dbo.sp_n_InsertarProducto", "Productos", "IDProducto", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCategoria", r.IdCategoria);
            cmd.Parameters.AddWithValue("@IDMarca", r.IdMarca);
            cmd.Parameters.AddWithValue("@IDTipoUsado", r.IdTipoUsado);
            cmd.Parameters.AddWithValue("@Codigo", Texto(r.Codigo));
            cmd.Parameters.AddWithValue("@CodigoBarras", CodigoBarras(r));
            cmd.Parameters.AddWithValue("@Producto", Texto(r.Producto));
            cmd.Parameters.AddWithValue("@Descripcion", Texto(r.Descripcion));
            cmd.Parameters.AddWithValue("@DescripcionSat", Texto(r.DescripcionSat));
            cmd.Parameters.AddWithValue("@PrecioLista", r.PrecioLista);
            cmd.Parameters.AddWithValue("@PrecioLista2", r.PrecioLista2);
            cmd.Parameters.AddWithValue("@PrecioListaCS", r.PrecioListaCS);
            cmd.Parameters.AddWithValue("@Stock", r.Stock);
            cmd.Parameters.AddWithValue("@StockMinimo", r.StockMinimo);
            cmd.Parameters.AddWithValue("@StockMaximo", r.StockMaximo);
            cmd.Parameters.AddWithValue("@Observaciones", Texto(r.Observaciones));
            cmd.Parameters.AddWithValue("@PesoKg", r.PesoKg);
            cmd.Parameters.AddWithValue("@UnidadMedida", Texto(r.UnidadMedidaSat));
            cmd.Parameters.AddWithValue("@ClaveSat", Texto(r.ClaveSat));
            cmd.Parameters.AddWithValue("@IDFacturama", Texto(r.IdFacturama));
            cmd.Parameters.AddWithValue("@IDUnidadMedida", r.IdUnidadMedida);
            cmd.Parameters.AddWithValue("@NumPiezas", r.NumPiezas);
            cmd.Parameters.AddWithValue("@Litros", r.Litros);
            cmd.Parameters.AddWithValue("@MostrarLista", r.MostrarLista ? 1 : 0);
            cmd.Parameters.AddWithValue("@EsCarga", r.EsCarga ? 1 : 0);
        }, ct);

    public Task<ResultadoLegacy> ActualizarProductoAsync(
        int id, ActualizarProductoRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarProducto", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDProducto", id);
            cmd.Parameters.AddWithValue("@IDCategoria", r.IdCategoria);
            cmd.Parameters.AddWithValue("@IDMarca", r.IdMarca);
            cmd.Parameters.AddWithValue("@IDTipoUsado", r.IdTipoUsado);
            cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);
            cmd.Parameters.AddWithValue("@Codigo", Texto(r.Codigo));
            cmd.Parameters.AddWithValue("@CodigoBarras", CodigoBarras(r));
            cmd.Parameters.AddWithValue("@Producto", Texto(r.Producto));
            cmd.Parameters.AddWithValue("@Descripcion", Texto(r.Descripcion));
            cmd.Parameters.AddWithValue("@DescripcionSat", Texto(r.DescripcionSat));
            cmd.Parameters.AddWithValue("@PrecioLista", r.PrecioLista);
            cmd.Parameters.AddWithValue("@PrecioLista2", r.PrecioLista2);
            cmd.Parameters.AddWithValue("@PrecioListaCS", r.PrecioListaCS);
            cmd.Parameters.AddWithValue("@Stock", r.Stock);
            cmd.Parameters.AddWithValue("@StockMinimo", r.StockMinimo);
            cmd.Parameters.AddWithValue("@StockMaximo", r.StockMaximo);
            cmd.Parameters.AddWithValue("@Observaciones", Texto(r.Observaciones));
            cmd.Parameters.AddWithValue("@PesoKg", r.PesoKg);
            cmd.Parameters.AddWithValue("@UnidadMedida", Texto(r.UnidadMedidaSat));
            cmd.Parameters.AddWithValue("@ClaveSat", Texto(r.ClaveSat));
            cmd.Parameters.AddWithValue("@IDFacturama", Texto(r.IdFacturama));
            cmd.Parameters.AddWithValue("@IDUnidadMedida", r.IdUnidadMedida);
            cmd.Parameters.AddWithValue("@NumPiezas", r.NumPiezas);
            cmd.Parameters.AddWithValue("@Litros", r.Litros);
            cmd.Parameters.AddWithValue("@MostrarLista", r.MostrarLista ? 1 : 0);
            cmd.Parameters.AddWithValue("@EsCarga", r.EsCarga ? 1 : 0);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    public Task<ResultadoLegacy?> CambiarStatusProductoAsync(
        int id, int idStatus, int idUsuario, string equipo, CancellationToken ct)
        => CambiarStatusAsync(StatusProducto, id, idStatus, idUsuario, equipo, ct);

    /// <summary>
    /// Adjunta un archivo al producto.
    ///
    /// EL PRECIO SE LEE, NO SE RECIBE. sp_n_ActualizarProductoFile recibe
    /// @PrecioLista y lo escribe; en legacy, el cliente que no lo mandaba dejaba
    /// el precio del producto en CERO por subir una foto. Aqui se lee el precio
    /// actual del renglon y se le devuelve al procedimiento, para que adjuntar un
    /// archivo no pueda cambiar un precio. Si el producto no existe se devuelve
    /// null y el controlador contesta 404: sin renglon no hay precio que
    /// conservar, y mandar 0 seria exactamente el defecto que se esta evitando.
    /// </summary>
    public async Task<(ResultadoLegacy Resultado, int IdArchivo)?> GuardarArchivoProductoAsync(
        int idProducto, ArchivoProductoRequest r, int idUsuario, string equipo, CancellationToken ct)
    {
        await using var conn = GetConnection();
        await conn.OpenAsync(ct);

        decimal precioActual;
        await using (var lectura = new SqlCommand(
            "SELECT [Precio de Lista] FROM dbo.Productos WHERE IDProducto = @Id", conn))
        {
            lectura.Parameters.AddWithValue("@Id", idProducto);
            var valor = await lectura.ExecuteScalarAsync(ct);
            if (valor is null) return null;
            precioActual = valor is DBNull ? 0m : Convert.ToDecimal(valor, CultureInfo.InvariantCulture);
        }

        await using var cmd = Sp("dbo.sp_n_ActualizarProductoFile", conn);
        cmd.Parameters.AddWithValue("@IDProducto", idProducto);
        cmd.Parameters.AddWithValue("@PrecioLista", precioActual);
        cmd.Parameters.AddWithValue("@IDArchivo", r.IdArchivo);
        cmd.Parameters.AddWithValue("@Archivo", Texto(r.Archivo));
        cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
        cmd.Parameters.AddWithValue("@Equipo", equipo);

        var dt = await FillAsync(cmd, ct);
        var resultado = BajaLogicaLegacy.LeerResultado(dt);
        // El procedimiento devuelve el id del archivo en su propia columna.
        var idArchivo = dt.Rows.Count > 0 ? BajaLogicaLegacy.LeerInt(dt.Rows[0], "IDArchivo") : 0;
        return (resultado, idArchivo);
    }

    /// <summary>
    /// Sin codigo de barras se usa el codigo. Es lo que hacia legacy y lo que
    /// espera la caja: el lector dispara la misma busqueda con los dos.
    /// </summary>
    private static string CodigoBarras(CrearProductoRequest r)
    {
        var barras = Texto(r.CodigoBarras);
        return barras.Length > 0 ? barras : Texto(r.Codigo);
    }

    // =======================================================================
    // PROVEEDOR
    // =======================================================================

    private static readonly BajaLogicaLegacy.Descriptor StatusProveedor = new(
        "Proveedores", "IDProveedor", "@IDProveedor", "dbo.sp_n_ActualizarProveedor",
        new[]
        {
            ("@IDColonia", "IDColonia"),
            ("@IDCondicionesCredito", "IDCondicionesCredito"),
            ("@Nombre", "Nombre"),
            ("@ApellidoPaterno", "Apellido Paterno"),
            ("@ApellidoMaterno", "Apellido Materno"),
            ("@RFC", "RFC"),
            ("@CalleNumero", "Calle y Numero"),
            ("@Telefono1", "Telefono 1"),
            ("@Telefono2", "Telefono 2"),
            ("@Email", "Email"),
            ("@Observaciones", "Observaciones"),
            ("@PaginaWeb", "Pagina Web")
        });

    public Task<(ResultadoLegacy, int)> InsertarProveedorAsync(CrearProveedorRequest r, CancellationToken ct)
        => InsertarConUltimoIdAsync("dbo.sp_n_InsertarProveedor", "Proveedores", "IDProveedor", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDColonia", r.IdColonia);
            cmd.Parameters.AddWithValue("@IDCondicionesCredito", r.IdCondicionesCredito);
            cmd.Parameters.AddWithValue("@IDDescuento", r.IdDescuento);
            cmd.Parameters.AddWithValue("@Nombre", Texto(r.Nombre));
            cmd.Parameters.AddWithValue("@ApellidoPaterno", Texto(r.ApellidoPaterno));
            cmd.Parameters.AddWithValue("@ApellidoMaterno", Texto(r.ApellidoMaterno));
            cmd.Parameters.AddWithValue("@RFC", Texto(r.RFC));
            cmd.Parameters.AddWithValue("@CalleNumero", Texto(r.CalleNumero));
            cmd.Parameters.AddWithValue("@Telefono1", Texto(r.Telefono1));
            cmd.Parameters.AddWithValue("@Telefono2", Texto(r.Telefono2));
            cmd.Parameters.AddWithValue("@Email", Texto(r.Email));
            cmd.Parameters.AddWithValue("@Observaciones", Texto(r.Observaciones));
            cmd.Parameters.AddWithValue("@PaginaWeb", Texto(r.PaginaWeb));
            cmd.Parameters.AddWithValue("@IDFacturama", Texto(r.IdFacturama));
        }, ct);

    /// <summary>
    /// Modificacion de proveedor.
    ///
    /// sp_n_ActualizarProveedor NO recibe @IDDescuento ni @IDFacturama, aunque el
    /// alta si: por eso el request de modificacion no los pide. Ofrecerlos seria
    /// prometer un cambio que la base no va a hacer. Tampoco recibe @IDUsuario ni
    /// @Equipo, asi que la modificacion de un proveedor no deja firma — ni desde
    /// el web ni desde Mac31. Queda anotado, no se puede arreglar sin tocar el
    /// procedimiento.
    /// </summary>
    public Task<ResultadoLegacy> ActualizarProveedorAsync(int id, ActualizarProveedorRequest r, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarProveedor", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDProveedor", id);
            cmd.Parameters.AddWithValue("@IDColonia", r.IdColonia);
            cmd.Parameters.AddWithValue("@IDCondicionesCredito", r.IdCondicionesCredito);
            cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);
            cmd.Parameters.AddWithValue("@Nombre", Texto(r.Nombre));
            cmd.Parameters.AddWithValue("@ApellidoPaterno", Texto(r.ApellidoPaterno));
            cmd.Parameters.AddWithValue("@ApellidoMaterno", Texto(r.ApellidoMaterno));
            cmd.Parameters.AddWithValue("@RFC", Texto(r.RFC));
            cmd.Parameters.AddWithValue("@CalleNumero", Texto(r.CalleNumero));
            cmd.Parameters.AddWithValue("@Telefono1", Texto(r.Telefono1));
            cmd.Parameters.AddWithValue("@Telefono2", Texto(r.Telefono2));
            cmd.Parameters.AddWithValue("@Email", Texto(r.Email));
            cmd.Parameters.AddWithValue("@Observaciones", Texto(r.Observaciones));
            cmd.Parameters.AddWithValue("@PaginaWeb", Texto(r.PaginaWeb));
        }, ct);

    public Task<ResultadoLegacy?> CambiarStatusProveedorAsync(int id, int idStatus, CancellationToken ct)
        => CambiarStatusAsync(StatusProveedor, id, idStatus, 0, string.Empty, ct);

    // --- Apoderados

    private static readonly BajaLogicaLegacy.Descriptor StatusApoderado = new(
        "ProveedoresApoderados", "IDProveedorApoderado", "@IDProveedorApoderado",
        "dbo.sp_n_ActualizarProveedorApoderado",
        new[] { ("@IDProveedor", "IDProveedor"), ("@IDPuesto", "IDPuesto"), ("@Apoderado", "Apoderado") },
        PideUsuario: true, PideEquipo: true);

    public Task<(ResultadoLegacy, int)> InsertarApoderadoAsync(
        CrearProveedorApoderadoRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarConIdAsync("dbo.sp_n_InsertarProveedorApoderado", "IDResult", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDProveedor", r.IdProveedor);
            cmd.Parameters.AddWithValue("@IDPuesto", r.IdPuesto);
            cmd.Parameters.AddWithValue("@Apoderado", Texto(r.Apoderado));
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    public Task<ResultadoLegacy> ActualizarApoderadoAsync(
        int id, ActualizarProveedorApoderadoRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarProveedorApoderado", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDProveedorApoderado", id);
            cmd.Parameters.AddWithValue("@IDProveedor", r.IdProveedor);
            cmd.Parameters.AddWithValue("@IDPuesto", r.IdPuesto);
            cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);
            cmd.Parameters.AddWithValue("@Apoderado", Texto(r.Apoderado));
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    public Task<ResultadoLegacy?> CambiarStatusApoderadoAsync(
        int id, int idStatus, int idUsuario, string equipo, CancellationToken ct)
        => CambiarStatusAsync(StatusApoderado, id, idStatus, idUsuario, equipo, ct);

    // --- Domicilios

    private static readonly BajaLogicaLegacy.Descriptor StatusDomicilio = new(
        "ProveedoresDomicilios", "IDProveedorDomicilio", "@IDProveedorDomicilio",
        "dbo.sp_n_ActualizarProveedorDomicilio",
        new[]
        {
            ("@IDTipoDomicilio", "IDTipoDomicilio"),
            ("@IDColonia", "IDColonia"),
            ("@Domicilio", "Domicilio"),
            ("@CalleNumero", "CalleNumero")
        },
        PideUsuario: true, PideEquipo: true);

    public Task<(ResultadoLegacy, int)> InsertarDomicilioAsync(
        CrearProveedorDomicilioRequest r, int idUsuario, string equipo, CancellationToken ct)
        => InsertarConUltimoIdAsync("dbo.sp_n_InsertarProveedorDomicilio",
            "ProveedoresDomicilios", "IDProveedorDomicilio", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDProveedor", r.IdProveedor);
            cmd.Parameters.AddWithValue("@IDTipoDomicilio", r.IdTipoDomicilio);
            cmd.Parameters.AddWithValue("@IDColonia", r.IdColonia);
            cmd.Parameters.AddWithValue("@Domicilio", Texto(r.Domicilio));
            cmd.Parameters.AddWithValue("@CalleNumero", Texto(r.CalleNumero));
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    public Task<ResultadoLegacy> ActualizarDomicilioAsync(
        int id, ActualizarProveedorDomicilioRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarProveedorDomicilio", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDProveedorDomicilio", id);
            cmd.Parameters.AddWithValue("@IDTipoDomicilio", r.IdTipoDomicilio);
            cmd.Parameters.AddWithValue("@IDColonia", r.IdColonia);
            cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);
            cmd.Parameters.AddWithValue("@Domicilio", Texto(r.Domicilio));
            cmd.Parameters.AddWithValue("@CalleNumero", Texto(r.CalleNumero));
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    public Task<ResultadoLegacy?> CambiarStatusDomicilioAsync(
        int id, int idStatus, int idUsuario, string equipo, CancellationToken ct)
        => CambiarStatusAsync(StatusDomicilio, id, idStatus, idUsuario, equipo, ct);

    // --- Documentos

    private static readonly BajaLogicaLegacy.Descriptor StatusDocumentoProveedor = new(
        "ProveedoresDocumentos", "IDProveedorDocumento", "@IDProveedorDocumento",
        "dbo.sp_n_ActualizarProveedorDocumento",
        new[]
        {
            ("@IDProveedor", "IDProveedor"),
            ("@IDTipoDocumento", "IDTipoDocumento"),
            ("@Documento", "Documento"),
            ("@Archivo", "Archivo"),
            ("@ArchivoFtp", "ArchivoFtp")
        },
        PideUsuario: true, PideEquipo: true);

    /// <summary>
    /// Alta de documento del proveedor.
    ///
    /// SE MANDAN SIETE PARAMETROS, NO NUEVE. El repositorio de legacy le manda a
    /// sp_n_InsertarProveedorDocumento un @IDStatus y un @Escaneado que el
    /// procedimiento no tiene: leidos de sys.parameters en Produccion_svr, sus
    /// unicos parametros son IDProveedor, IDTipoDocumento, Documento, Archivo,
    /// ArchivoFtp, IDUsuario y Equipo. Es decir que esa alta de MacServicios2
    /// revienta en esta base antes de tocar la tabla. Aqui se manda lo que el
    /// procedimiento pide, que es lo unico que puede funcionar.
    /// </summary>
    public Task<(ResultadoLegacy, int)> InsertarDocumentoProveedorAsync(
        CrearProveedorDocumentoRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarConIdAsync("dbo.sp_n_InsertarProveedorDocumento", "IDResult", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDProveedor", r.IdProveedor);
            cmd.Parameters.AddWithValue("@IDTipoDocumento", r.IdTipoDocumento);
            cmd.Parameters.AddWithValue("@Documento", Texto(r.Documento));
            cmd.Parameters.AddWithValue("@Archivo", Texto(r.Archivo));
            cmd.Parameters.AddWithValue("@ArchivoFtp", Texto(r.ArchivoFtp));
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    /// <summary>
    /// Modificacion del documento. Mismo caso: legacy manda un @Escaneado que el
    /// procedimiento no recibe. Aqui no se manda.
    /// </summary>
    public Task<ResultadoLegacy> ActualizarDocumentoProveedorAsync(
        int id, ActualizarProveedorDocumentoRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarProveedorDocumento", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDProveedorDocumento", id);
            cmd.Parameters.AddWithValue("@IDProveedor", r.IdProveedor);
            cmd.Parameters.AddWithValue("@IDTipoDocumento", r.IdTipoDocumento);
            cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);
            cmd.Parameters.AddWithValue("@Documento", Texto(r.Documento));
            cmd.Parameters.AddWithValue("@Archivo", Texto(r.Archivo));
            cmd.Parameters.AddWithValue("@ArchivoFtp", Texto(r.ArchivoFtp));
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    public Task<ResultadoLegacy?> CambiarStatusDocumentoProveedorAsync(
        int id, int idStatus, int idUsuario, string equipo, CancellationToken ct)
        => CambiarStatusAsync(StatusDocumentoProveedor, id, idStatus, idUsuario, equipo, ct);
}
