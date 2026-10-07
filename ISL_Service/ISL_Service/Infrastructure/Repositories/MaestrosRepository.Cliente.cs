using ISL_Service.Application.DTOs.Empleados;
using ISL_Service.Application.DTOs.Maestros;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Repositories;

/// <summary>
/// La ficha del cliente: alta, modificacion, baja, bonificaciones, centros de
/// servicio permitidos y domicilios de facturacion.
/// </summary>
public partial class MaestrosRepository
{
    /// <summary>
    /// Lo que hay que acarrear para darle de baja a un cliente sin vaciarlo.
    ///
    /// Son las 31 columnas que escribe el UPDATE de sp_n_ActualizarCliente. La
    /// lista salio de leer ese UPDATE renglon por renglon en Produccion_svr; si
    /// alguien le agrega una columna al procedimiento, hay que agregarla aqui o
    /// la baja la dejara en blanco. Los nombres con espacios son de Mac31
    /// ([Apellido Paterno], [Limite Credito]) y van entre corchetes solos.
    ///
    /// @Accion = 0 va fijo: la rama @Accion = 1 del procedimiento solo cambia el
    /// IDFacturama y se devuelve antes de tocar el estatus.
    /// </summary>
    private static readonly BajaLogicaLegacy.Descriptor StatusCliente = new(
        "Clientes", "IDCliente", "@IDCliente", "dbo.sp_n_ActualizarCliente",
        new[]
        {
            ("@IDTipoCliente", "IDTipoCliente"),
            ("@IDColonia", "IDColonia"),
            ("@IDCondicionesCredito", "IDCondicionesCredito"),
            ("@IDCondicionesCreditoAceites", "IDCondicionesCreditoAceites"),
            ("@IDCondicionesCreditoCascos", "IDCondicionesCreditoCascos"),
            ("@IDAgente", "IDAgente"),
            ("@IDGrupoCliente", "IDGrupoCliente"),
            ("@Nombre", "Nombre"),
            ("@ApellidoPaterno", "Apellido Paterno"),
            ("@ApellidoMaterno", "Apellido Materno"),
            ("@NombreNegocio", "NombreNegocio"),
            ("@NombreFacturacion", "NombreFacturacion"),
            ("@RFC", "RFC"),
            ("@CURP", "CURP"),
            ("@Calle", "Calle"),
            ("@NumeroExterior", "NumeroExterior"),
            ("@NumeroInterior", "NumeroInterior"),
            ("@Telefono1", "Telefono 1"),
            ("@Telefono2", "Telefono 2"),
            ("@Email", "Email"),
            ("@Email2", "Email2"),
            ("@Email3", "Email3"),
            ("@Email4", "Email4"),
            ("@Email5", "Email5"),
            ("@Observaciones", "Observaciones"),
            ("@LimiteCredito", "Limite Credito"),
            ("@IDFacturama", "IDFacturama"),
            ("@Redondear", "Redondear"),
            ("@CascosCambio", "CascosCambio"),
            ("@Efectivo", "Efectivo"),
            ("@Cheque", "Cheque"),
            ("@Transferencia", "Transferencia"),
            ("@DepositoEfectivo", "DepositoEfectivo"),
            ("@FacturaParaPago", "FacturaParaPago"),
            ("@Referencia", "Referencia"),
            ("@NumeroCapturado", "Numero")
        },
        Fijos: new[] { ("@Accion", (object)0) },
        PideUsuario: true,
        PideEquipo: true);

    public Task<(ResultadoLegacy, int)> InsertarClienteAsync(
        CrearClienteRequest r, int idUsuario, string equipo, CancellationToken ct)
        => InsertarConUltimoIdAsync("dbo.sp_n_InsertarCliente", "Clientes", "IDCliente", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDColonia", r.IdColonia);
            cmd.Parameters.AddWithValue("@IDCondicionesCredito", r.IdCondicionesCredito);
            cmd.Parameters.AddWithValue("@IDCondicionesCreditoAceites", r.IdCondicionesCreditoAceites);
            cmd.Parameters.AddWithValue("@IDCondicionesCreditoCascos", r.IdCondicionesCreditoCascos);
            cmd.Parameters.AddWithValue("@IDDescuento", r.IdDescuento);
            cmd.Parameters.AddWithValue("@IDAgente", r.IdAgente);
            cmd.Parameters.AddWithValue("@IDTipoCliente", r.IdTipoCliente);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@IDGrupoCliente", r.IdGrupoCliente);
            cmd.Parameters.AddWithValue("@Nombre", Texto(r.Nombre));
            cmd.Parameters.AddWithValue("@ApellidoPaterno", Texto(r.ApellidoPaterno));
            cmd.Parameters.AddWithValue("@ApellidoMaterno", Texto(r.ApellidoMaterno));
            cmd.Parameters.AddWithValue("@NombreNegocio", Texto(r.NombreNegocio));
            cmd.Parameters.AddWithValue("@NombreFacturacion", NombreFacturacion(r));
            cmd.Parameters.AddWithValue("@RFC", Texto(r.RFC));
            cmd.Parameters.AddWithValue("@CURP", Texto(r.CURP));
            cmd.Parameters.AddWithValue("@Calle", Texto(r.Calle));
            cmd.Parameters.AddWithValue("@NumeroExterior", Texto(r.NumeroExterior));
            cmd.Parameters.AddWithValue("@NumeroInterior", Texto(r.NumeroInterior));
            cmd.Parameters.AddWithValue("@Telefono1", Texto(r.Telefono1));
            cmd.Parameters.AddWithValue("@Telefono2", Texto(r.Telefono2));
            cmd.Parameters.AddWithValue("@Email", Texto(r.Email));
            cmd.Parameters.AddWithValue("@Email2", Texto(r.Email2));
            cmd.Parameters.AddWithValue("@Email3", Texto(r.Email3));
            cmd.Parameters.AddWithValue("@Email4", Texto(r.Email4));
            cmd.Parameters.AddWithValue("@Email5", Texto(r.Email5));
            cmd.Parameters.AddWithValue("@Observaciones", Texto(r.Observaciones));
            cmd.Parameters.AddWithValue("@IDFacturama", Texto(r.IdFacturama));
            cmd.Parameters.AddWithValue("@Equipo", equipo);
            cmd.Parameters.AddWithValue("@Redondear", r.Redondear ? 1 : 0);
            cmd.Parameters.AddWithValue("@LimiteCredito", r.LimiteCredito);
            cmd.Parameters.AddWithValue("@NumeroCapturado", r.NumeroCapturado);
            cmd.Parameters.AddWithValue("@Efectivo", r.Efectivo ? 1 : 0);
            cmd.Parameters.AddWithValue("@Cheque", r.Cheque ? 1 : 0);
            cmd.Parameters.AddWithValue("@Transferencia", r.Transferencia ? 1 : 0);
            cmd.Parameters.AddWithValue("@DepositoEfectivo", r.DepositoEfectivo ? 1 : 0);
            cmd.Parameters.AddWithValue("@FacturaParaPago", r.FacturaParaPago ? 1 : 0);
            cmd.Parameters.AddWithValue("@Referencia", Texto(r.Referencia));
        }, ct);

    public Task<ResultadoLegacy> ActualizarClienteAsync(
        int id, ActualizarClienteRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarCliente", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCliente", id);
            cmd.Parameters.AddWithValue("@IDColonia", r.IdColonia);
            cmd.Parameters.AddWithValue("@IDCondicionesCredito", r.IdCondicionesCredito);
            cmd.Parameters.AddWithValue("@IDCondicionesCreditoAceites", r.IdCondicionesCreditoAceites);
            cmd.Parameters.AddWithValue("@IDCondicionesCreditoCascos", r.IdCondicionesCreditoCascos);
            cmd.Parameters.AddWithValue("@IDAgente", r.IdAgente);
            cmd.Parameters.AddWithValue("@IDTipoCliente", r.IdTipoCliente);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@IDGrupoCliente", r.IdGrupoCliente);
            cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);
            cmd.Parameters.AddWithValue("@Nombre", Texto(r.Nombre));
            cmd.Parameters.AddWithValue("@ApellidoPaterno", Texto(r.ApellidoPaterno));
            cmd.Parameters.AddWithValue("@ApellidoMaterno", Texto(r.ApellidoMaterno));
            cmd.Parameters.AddWithValue("@NombreNegocio", Texto(r.NombreNegocio));
            cmd.Parameters.AddWithValue("@NombreFacturacion", NombreFacturacion(r));
            cmd.Parameters.AddWithValue("@RFC", Texto(r.RFC));
            cmd.Parameters.AddWithValue("@CURP", Texto(r.CURP));
            cmd.Parameters.AddWithValue("@Calle", Texto(r.Calle));
            cmd.Parameters.AddWithValue("@NumeroExterior", Texto(r.NumeroExterior));
            cmd.Parameters.AddWithValue("@NumeroInterior", Texto(r.NumeroInterior));
            cmd.Parameters.AddWithValue("@Telefono1", Texto(r.Telefono1));
            cmd.Parameters.AddWithValue("@Telefono2", Texto(r.Telefono2));
            cmd.Parameters.AddWithValue("@Email", Texto(r.Email));
            cmd.Parameters.AddWithValue("@Email2", Texto(r.Email2));
            cmd.Parameters.AddWithValue("@Email3", Texto(r.Email3));
            cmd.Parameters.AddWithValue("@Email4", Texto(r.Email4));
            cmd.Parameters.AddWithValue("@Email5", Texto(r.Email5));
            cmd.Parameters.AddWithValue("@Observaciones", Texto(r.Observaciones));
            cmd.Parameters.AddWithValue("@IDFacturama", Texto(r.IdFacturama));
            cmd.Parameters.AddWithValue("@Equipo", equipo);
            // 0 = actualizar todo. La rama 1 solo cambia el IDFacturama y vive
            // en su propio endpoint.
            cmd.Parameters.AddWithValue("@Accion", 0);
            cmd.Parameters.AddWithValue("@Redondear", r.Redondear ? 1 : 0);
            cmd.Parameters.AddWithValue("@LimiteCredito", r.LimiteCredito);
            cmd.Parameters.AddWithValue("@CascosCambio", r.CascosCambio ? 1 : 0);
            cmd.Parameters.AddWithValue("@NumeroCapturado", r.NumeroCapturado);
            cmd.Parameters.AddWithValue("@Efectivo", r.Efectivo ? 1 : 0);
            cmd.Parameters.AddWithValue("@Cheque", r.Cheque ? 1 : 0);
            cmd.Parameters.AddWithValue("@Transferencia", r.Transferencia ? 1 : 0);
            cmd.Parameters.AddWithValue("@DepositoEfectivo", r.DepositoEfectivo ? 1 : 0);
            cmd.Parameters.AddWithValue("@FacturaParaPago", r.FacturaParaPago ? 1 : 0);
            cmd.Parameters.AddWithValue("@Referencia", Texto(r.Referencia));
        }, ct);

    /// <summary>
    /// Cambia SOLO el IDFacturama: la rama @Accion = 1 del procedimiento, que
    /// hace un UPDATE de esa columna y regresa sin tocar nada mas.
    /// </summary>
    public Task<ResultadoLegacy> CambiarFacturamaClienteAsync(
        int id, string? idFacturama, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarCliente", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCliente", id);
            cmd.Parameters.AddWithValue("@IDFacturama", Texto(idFacturama));
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
            cmd.Parameters.AddWithValue("@Accion", 1);
        }, ct);

    public Task<ResultadoLegacy?> CambiarStatusClienteAsync(
        int id, int idStatus, int idUsuario, string equipo, CancellationToken ct)
        => CambiarStatusAsync(StatusCliente, id, idStatus, idUsuario, equipo, ct);

    /// <summary>
    /// Bonificaciones por tipo de usado.
    ///
    /// El procedimiento recibe DOS cadenas unidas con '~' y las aparea por
    /// posicion. Aqui las dos salen de la MISMA lista de pares, asi que no
    /// pueden quedar desalineadas: ese era el riesgo de legacy, donde el front
    /// armaba las dos cadenas por separado y nadie comprobaba que tuvieran el
    /// mismo numero de elementos. Una bonificacion puesta al tipo de casco
    /// equivocado es dinero mal dado, y no avisa.
    /// </summary>
    public Task<ResultadoLegacy> GuardarBonificacionesAsync(
        int idCliente, BonificacionesClienteRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarClienteBonificacion", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCliente", idCliente);
            cmd.Parameters.AddWithValue("@IDsTipoUsado",
                Unir(r.Bonificaciones.Select(x => x.IdTipoUsado.ToString(System.Globalization.CultureInfo.InvariantCulture))));
            cmd.Parameters.AddWithValue("@Bonificaciones",
                Unir(r.Bonificaciones.Select(x => Numero(x.Bonificacion))));
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    /// <summary>
    /// Centros de servicio permitidos. Mismo caso que las bonificaciones: las
    /// dos cadenas '~' salen de la misma lista de pares.
    /// </summary>
    public Task<ResultadoLegacy> GuardarCentrosServicioAsync(
        int idCliente, CentrosServicioClienteRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarClienteCentroServicio", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCliente", idCliente);
            cmd.Parameters.AddWithValue("@IDsCentro",
                Unir(r.Centros.Select(x => x.IdCentro.ToString(System.Globalization.CultureInfo.InvariantCulture))));
            cmd.Parameters.AddWithValue("@Permitidos",
                Unir(r.Centros.Select(x => x.Permitido ? "1" : "0")));
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    public Task<ResultadoLegacy> InsertarClienteFacturacionAsync(
        CrearClienteFacturacionRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_InsertarClienteFacturacion", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDCliente", r.IdCliente);
            cmd.Parameters.AddWithValue("@IDColonia", r.IdColonia);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@NombreFacturacion", Texto(r.NombreFacturacion));
            cmd.Parameters.AddWithValue("@RFC", Texto(r.RFC));
            cmd.Parameters.AddWithValue("@Calle", Texto(r.Calle));
            cmd.Parameters.AddWithValue("@NumeroExterior", Texto(r.NumeroExterior));
            cmd.Parameters.AddWithValue("@NumeroInterior", Texto(r.NumeroInterior));
            cmd.Parameters.AddWithValue("@IDFEFormaPago", Texto(r.IdFEFormaPago));
            cmd.Parameters.AddWithValue("@IDFERegimenFiscal", Texto(r.IdFERegimenFiscal));
            cmd.Parameters.AddWithValue("@IDFEUsoCFDI", Texto(r.IdFEUsoCFDI));
            cmd.Parameters.AddWithValue("@IDFEMetodoPago", Texto(r.IdFEMetodoPago));
            cmd.Parameters.AddWithValue("@Equipo", equipo);
        }, ct);

    /// <summary>
    /// Modificacion del domicilio de facturacion.
    ///
    /// AQUI EL APOSTROFO SI SE ESCAPA, y es el unico lugar de este repositorio
    /// donde pasa. sp_n_ActualizarClienteFacturacion no usa parametros: arma el
    /// UPDATE pegando las cadenas con comillas simples alrededor y lo corre con
    /// EXEC(@Sql). Un apostrofo en la razon social rompe la sentencia, asi que
    /// se duplica, que es como se escapa en T-SQL. El dato se guarda completo:
    /// "O'BRIEN S.A." sigue siendo "O'BRIEN S.A.".
    ///
    /// Ese procedimiento ademas puede apuntar a OTRA base segun
    /// Constantes.Funcionalidad (BDZara / BDLuro): el @BD que arma adentro. No se
    /// toca; solo hay que saber que el renglon no siempre se escribe en la base
    /// a la que esta conectado el backend.
    /// </summary>
    public Task<ResultadoLegacy> ActualizarClienteFacturacionAsync(
        int id, ActualizarClienteFacturacionRequest r, int idUsuario, string equipo, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarClienteFacturacion", cmd =>
        {
            cmd.Parameters.AddWithValue("@IDClienteFacturacion", id);
            cmd.Parameters.AddWithValue("@IDColonia", r.IdColonia);
            cmd.Parameters.AddWithValue("@IDUsuario", idUsuario);
            cmd.Parameters.AddWithValue("@IDStatus", r.IdStatus);
            cmd.Parameters.AddWithValue("@NombreFacturacion", TextoParaSqlDinamico(r.NombreFacturacion));
            cmd.Parameters.AddWithValue("@RFC", TextoParaSqlDinamico(r.RFC));
            cmd.Parameters.AddWithValue("@Calle", TextoParaSqlDinamico(r.Calle));
            cmd.Parameters.AddWithValue("@NumeroExterior", TextoParaSqlDinamico(r.NumeroExterior));
            cmd.Parameters.AddWithValue("@NumeroInterior", TextoParaSqlDinamico(r.NumeroInterior));
            cmd.Parameters.AddWithValue("@IDFEFormaPago", TextoParaSqlDinamico(r.IdFEFormaPago));
            cmd.Parameters.AddWithValue("@IDFERegimenFiscal", TextoParaSqlDinamico(r.IdFERegimenFiscal));
            cmd.Parameters.AddWithValue("@IDFEUsoCFDI", TextoParaSqlDinamico(r.IdFEUsoCFDI));
            cmd.Parameters.AddWithValue("@IDFEMetodoPago", TextoParaSqlDinamico(r.IdFEMetodoPago));
            cmd.Parameters.AddWithValue("@Equipo", TextoParaSqlDinamico(equipo));
            cmd.Parameters.AddWithValue("@IDEmpresaCS", r.IdEmpresaCS);
        }, ct);

    /// <summary>
    /// Marca el cliente como facturacion de mostrador. El procedimiento recibe
    /// un unico parametro y no deja rastro de quien lo hizo: legacy tampoco le
    /// manda usuario ni equipo porque no los tiene. Queda anotado como lo que
    /// es, una operacion sin auditoria en la base.
    /// </summary>
    public Task<ResultadoLegacy> MarcarFacturacionMostradorAsync(int idCliente, CancellationToken ct)
        => EjecutarAsync("dbo.sp_n_ActualizarClienteFacturacionMostrador",
            cmd => cmd.Parameters.AddWithValue("@IDCliente", idCliente), ct);

    /// <summary>
    /// El nombre con el que se factura. Si no viene, se arma con nombre y
    /// apellidos, igual que en legacy: la factura sale con ese texto y dejarlo
    /// en blanco deja una factura sin razon social.
    /// </summary>
    private static string NombreFacturacion(CrearClienteRequest r)
    {
        var dado = Texto(r.NombreFacturacion);
        if (dado.Length > 0) return dado;

        return $"{Texto(r.Nombre)} {Texto(r.ApellidoPaterno)} {Texto(r.ApellidoMaterno)}".Trim();
    }
}
