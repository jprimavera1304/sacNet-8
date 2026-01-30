using ISL_Service.Application.DTOs.ProveedoresPagos;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;

namespace ISL_Service.Infrastructure.Repositories
{
    /// <summary>
    /// Repository de ProveedoresPagos
    /// Cambia automáticamente entre ConnectionString Local y Mac3
    /// según el ambiente (ASPNETCORE_ENVIRONMENT).
    /// </summary>
    public class ProveedoresPagosRepository
    {
        private readonly IConfiguration _configuration;

        public DataTable dtProveedoresPagos;
        public List<ProveedorPagoDTO> proveedorPagoDTOList = new List<ProveedorPagoDTO>();

        public string sp = "";
        public string msgError = "";
        public string debug = "";

        private readonly string spConsultar = "sp_n_ConsultarProveedoresPagos";
        private readonly string spInsertar = "sp_n_InsertarProveedorPago";
        private readonly string spActualizar = "sp_n_ActualizarProveedorPago";
        private readonly string spCancelar = "sp_n_CancelarProveedorPago";

        public ProveedoresPagosRepository(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        #region Connection

        private SqlConnection GetConnection()
        {
            string connectionString = GetConnectionString();
            Mac3SqlServerConnector objConn = new Mac3SqlServerConnector(connectionString);
            return objConn.GetConnection;
        }

        private string GetConnectionString()
        {
            string env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            if (!string.IsNullOrWhiteSpace(env) &&
                env.Equals("Development", StringComparison.OrdinalIgnoreCase))
            {
                return _configuration.GetConnectionString("Local");
            }

            return _configuration.GetConnectionString("Mac3");
        }

        #endregion

        #region Consultar

        public List<ProveedorPagoDTO> Consultar(ProveedorPagoInputDTO input)
        {
            using SqlConnection Conn = GetConnection();

            try
            {
                Conn.Open();

                DataSet ds = new DataSet();

                sp = spConsultar;
                using SqlCommand command = new SqlCommand(sp, Conn);
                command.CommandType = CommandType.StoredProcedure;

                DateTime? fechaInicial = TryParseDate(input.FechaInicial);
                DateTime? fechaFinal = TryParseDate(input.FechaFinal);

                command.Parameters.AddWithValue("@IDProveedorPago", input.IDProveedorPago);
                command.Parameters.AddWithValue("@IDPersona", input.IDPersona);
                command.Parameters.AddWithValue("@IDStatus", input.IDStatus);
                command.Parameters.AddWithValue("@FechaInicial", (object?)fechaInicial ?? DBNull.Value);
                command.Parameters.AddWithValue("@FechaFinal", (object?)fechaFinal ?? DBNull.Value);
                command.Parameters.AddWithValue("@IncluirCancelados", input.IncluirCancelados);

                debug = $"{sp} {input.IDProveedorPago}, {input.IDPersona}, {input.IDStatus}, {input.IncluirCancelados}";

                using SqlDataAdapter da = new SqlDataAdapter(command);
                da.Fill(ds);

                proveedorPagoDTOList = Funciones.DataTableToList<ProveedorPagoDTO>(ds.Tables[0]);
                dtProveedoresPagos = ds.Tables[0];

                return proveedorPagoDTOList;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                msgError = ex.Message;
                throw new Exception("[Error Consultar ProveedoresPagos] " + ex.Message);
            }
        }

        #endregion

        #region Insertar

        public int Insertar(ProveedorPagoInputDTO input)
        {
            using SqlConnection Conn = GetConnection();

            try
            {
                Conn.Open();

                sp = spInsertar;
                using SqlCommand command = new SqlCommand(sp, Conn);
                command.CommandType = CommandType.StoredProcedure;

                DateTime fechaPago = ParseRequiredDate(input.FechaPago, "FechaPago");

                command.Parameters.AddWithValue("@IDPersona", input.IDPersona);
                //command.Parameters.AddWithValue("@IDProveedor", input.IDProveedor);

                command.Parameters.AddWithValue("@Facturas", (object?)NullIfEmpty(input.Facturas) ?? DBNull.Value);
                command.Parameters.AddWithValue("@TotalFactura", (object?)input.TotalFactura ?? DBNull.Value);
                command.Parameters.AddWithValue("@FechaPago", fechaPago);
                command.Parameters.AddWithValue("@Observaciones", (object?)NullIfEmpty(input.Observaciones) ?? DBNull.Value);
                command.Parameters.AddWithValue("@IDUsuario", input.IDUsuario);
                command.Parameters.AddWithValue("@Equipo", input.Equipo);

                SqlParameter outId = new SqlParameter("@IDProveedorPago", SqlDbType.Int)
                {
                    Direction = ParameterDirection.Output
                };
                command.Parameters.Add(outId);

                command.ExecuteNonQuery();

                return (outId.Value == DBNull.Value) ? 0 : Convert.ToInt32(outId.Value);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                msgError = ex.Message;
                throw new Exception("[Error Insertar ProveedorPago] " + ex.Message);
            }
        }


        #endregion


        #region Actualizar

        public void Actualizar(ProveedorPagoInputDTO input)
        {
            using SqlConnection Conn = GetConnection();

            try
            {
                Conn.Open();

                sp = spActualizar;
                using SqlCommand command = new SqlCommand(sp, Conn);
                command.CommandType = CommandType.StoredProcedure;

                DateTime? fechaPago = TryParseDate(input.FechaPago);

                command.Parameters.AddWithValue("@IDProveedorPago", input.IDProveedorPago);
                command.Parameters.AddWithValue("@IDPersona", input.IDPersona == 0 ? DBNull.Value : (object)input.IDPersona);
                command.Parameters.AddWithValue("@Facturas", (object?)NullIfEmpty(input.Facturas) ?? DBNull.Value);
                command.Parameters.AddWithValue("@TotalFactura", (object?)input.TotalFactura ?? DBNull.Value);
                command.Parameters.AddWithValue("@FechaPago", (object?)fechaPago ?? DBNull.Value);
                command.Parameters.AddWithValue("@Observaciones", (object?)NullIfEmpty(input.Observaciones) ?? DBNull.Value);
                command.Parameters.AddWithValue("@IDUsuarioModificacion", input.IDUsuarioModificacion);
                command.Parameters.AddWithValue("@EquipoModificacion", input.EquipoModificacion);

                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                msgError = ex.Message;
                throw new Exception("[Error Actualizar ProveedorPago] " + ex.Message);
            }
        }

        #endregion

        #region Cancelar

        public void Cancelar(ProveedorPagoInputDTO input)
        {
            using SqlConnection Conn = GetConnection();

            try
            {
                Conn.Open();

                sp = spCancelar;
                using SqlCommand command = new SqlCommand(sp, Conn);
                command.CommandType = CommandType.StoredProcedure;

                command.Parameters.AddWithValue("@IDProveedorPago", input.IDProveedorPago);
                command.Parameters.AddWithValue("@MotivoCancelacion", (object?)NullIfEmpty(input.MotivoCancelacion) ?? DBNull.Value);
                command.Parameters.AddWithValue("@IDUsuarioCancelacion", input.IDUsuarioCancelacion);
                command.Parameters.AddWithValue("@EquipoCancelacion", input.EquipoCancelacion);

                command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                msgError = ex.Message;
                throw new Exception("[Error Cancelar ProveedorPago] " + ex.Message);
            }
        }

        #endregion

        #region Helpers

        private static string? NullIfEmpty(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return value.Trim();
        }

        private static DateTime? TryParseDate(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            return DateTime.TryParse(value, out var dt) ? dt : null;
        }

        private static DateTime ParseRequiredDate(string? value, string fieldName)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new Exception($"{fieldName} es obligatoria.");

            if (!DateTime.TryParse(value, out var dt))
                throw new Exception($"{fieldName} tiene formato inválido.");

            return dt;
        }

        #endregion
    }
}
