using ISL_Service.Application.DTOs.Recaudacion;
using ISL_Service.Infrastructure.Data;
using ISL_Service.Utils;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Diagnostics;

namespace ISL_Service.Infrastructure.Repositories
{
    public class RecaudacionRepository
    {
        private readonly AppDbContext _dbContext;

        public DataTable dtRecaudaciones;
        public List<RecaudacionDTO> recaudacionDTOList = new List<RecaudacionDTO>();
        public List<DataRow> recaudacionDTOListUpd = new List<DataRow>();

        public string spParams = "";
        public string sp = "";
        public string msgError = "";

        public string IDResult = "";
        public int result = 0;
        public string mensaje = "";
        public string debug = "";

        private readonly string spConsultaRecaudaciones = "sp_n_ConsultaRecaudaciones";
        private readonly string spConsultaRecaudacionesRecurrentes = "sp_n_ConsultaRecaudacionesRecurrentes";
        private readonly string spConsultaRecaudacionesNextID = "sp_n_ConsultaRecaudacionNextID";
        private readonly string spConsultaRecaudacionesCortes = "sp_n_ConsultaRecaudacionesCortes";

        private readonly string spInsertarRecaudacion = "sp_n_InsertarRecaudacion";
        private readonly string spInsertarRecaudacionCorte = "sp_n_InsertarRecaudacionCorte";

        ////private readonly string spActualizarRecaudacion = "sp_n_ActualizarEconomico";
        private readonly string spCancelarRecaudacion = "sp_n_CancelarRecaudacion";

        public RecaudacionRepository(AppDbContext _dbContext)
        {
            this._dbContext = _dbContext;
        }


        public List<RecaudacionDTO> GetById(RecaudacionInputDTO recaudacionInputDTO)
        {

            Mac3SqlServerConnector objConn = new Mac3SqlServerConnector(_dbContext._connectionString);
            SqlConnection Conn = objConn.GetConnection;


            try
            {
                Conn.Open();

                if (Conn.State != ConnectionState.Open) Conn.Open();

                DataSet ds = new DataSet();
                SqlDataAdapter da = new SqlDataAdapter();

                sp = spConsultaRecaudaciones;
                SqlCommand command = new SqlCommand(sp, Conn);
                command.CommandType = CommandType.StoredProcedure;

                command.Parameters.AddWithValue("@IDRecaudacion", recaudacionInputDTO.IDRecaudacion);
                command.Parameters.AddWithValue("@IDsRecaudacion", recaudacionInputDTO.IDsRecaudacion);

                command.Parameters.AddWithValue("@IDStatus", recaudacionInputDTO.IDStatus);

                command.Parameters.AddWithValue("@IDCaja", recaudacionInputDTO.IDCaja);
                command.Parameters.AddWithValue("@IDsCaja", recaudacionInputDTO.IDsCaja);
                command.Parameters.AddWithValue("@NumCajaInicial", recaudacionInputDTO.NumCajaInicial);
                command.Parameters.AddWithValue("@NumCajaFinal", recaudacionInputDTO.NumCajaFinal);

                command.Parameters.AddWithValue("@IDConcepto", recaudacionInputDTO.IDConcepto);
                command.Parameters.AddWithValue("@IDsConcepto", recaudacionInputDTO.IDsConcepto);
                command.Parameters.AddWithValue("@NumConceptoInicial", recaudacionInputDTO.NumConceptoInicial);
                command.Parameters.AddWithValue("@NumConceptoFinal", recaudacionInputDTO.NumConceptoFinal);

                command.Parameters.AddWithValue("@IDCuenta", recaudacionInputDTO.IDCuenta);
                command.Parameters.AddWithValue("@IDsCuenta", recaudacionInputDTO.IDsCuenta);
                command.Parameters.AddWithValue("@NumCuentaInicial", recaudacionInputDTO.NumCuentaInicial);
                command.Parameters.AddWithValue("@NumCuentaFinal", recaudacionInputDTO.NumCuentaFinal);

                command.Parameters.AddWithValue("@IDEconomico", recaudacionInputDTO.IDEconomico);
                command.Parameters.AddWithValue("@IDsEconomico", recaudacionInputDTO.IDsEconomico);
                command.Parameters.AddWithValue("@NumEconomicoInicial", recaudacionInputDTO.NumEconomicoInicial);
                command.Parameters.AddWithValue("@NumEconomicoFinal", recaudacionInputDTO.NumEconomicoFinal);

                command.Parameters.AddWithValue("@IDPermisionario", recaudacionInputDTO.IDPermisionario);
                command.Parameters.AddWithValue("@IDsPermisionario", recaudacionInputDTO.IDsPermisionario);
                command.Parameters.AddWithValue("@NumPermisionarioInicial", recaudacionInputDTO.NumPermisionarioInicial);
                command.Parameters.AddWithValue("@NumPermisionarioFinal", recaudacionInputDTO.NumPermisionarioFinal);

                command.Parameters.AddWithValue("@IDOperador", recaudacionInputDTO.IDOperador);
                command.Parameters.AddWithValue("@IDsOperador", recaudacionInputDTO.IDsOperador);
                command.Parameters.AddWithValue("@NumNominaInicial", recaudacionInputDTO.NumNominaInicial);
                command.Parameters.AddWithValue("@NumNominaFinal", recaudacionInputDTO.NumNominaFinal);

                command.Parameters.AddWithValue("@FolioInicial", recaudacionInputDTO.FolioInicial);
                command.Parameters.AddWithValue("@FolioFinal", recaudacionInputDTO.FolioFinal);

                command.Parameters.AddWithValue("@FechaInicial", recaudacionInputDTO.FechaInicial);
                command.Parameters.AddWithValue("@FechaFinal", recaudacionInputDTO.FechaFinal);

                debug = sp + "  " +
                            recaudacionInputDTO.IDRecaudacion + ", '" + recaudacionInputDTO.IDsRecaudacion + "', " +
                            recaudacionInputDTO.IDStatus + ", " +

                            recaudacionInputDTO.IDCaja + ", '" + recaudacionInputDTO.IDsCaja + "', " +
                            recaudacionInputDTO.NumCajaInicial + ", " + recaudacionInputDTO.NumCajaFinal + ", " +

                            recaudacionInputDTO.IDConcepto + ", '" + recaudacionInputDTO.IDsConcepto + "', " +
                            recaudacionInputDTO.NumConceptoInicial + ", " + recaudacionInputDTO.NumConceptoFinal + ", " +

                            recaudacionInputDTO.IDCuenta + ", '" + recaudacionInputDTO.IDsCuenta + "', " +
                            recaudacionInputDTO.NumCuentaInicial + ", " + recaudacionInputDTO.NumCuentaFinal + ", " +

                            recaudacionInputDTO.IDEconomico + ", '" + recaudacionInputDTO.IDsEconomico + "', " +
                            recaudacionInputDTO.NumEconomicoInicial + ", " + recaudacionInputDTO.NumEconomicoFinal + ", " +

                            recaudacionInputDTO.IDPermisionario + ", '" + recaudacionInputDTO.IDsPermisionario + "', " +
                            recaudacionInputDTO.NumPermisionarioInicial + ", " + recaudacionInputDTO.NumPermisionarioFinal + ", " +

                            recaudacionInputDTO.IDOperador + ", '" + recaudacionInputDTO.IDsOperador + "', " +
                            recaudacionInputDTO.NumNominaInicial + ", " + recaudacionInputDTO.NumNominaFinal + ", " +

                            recaudacionInputDTO.FolioInicial + ", " + recaudacionInputDTO.FolioFinal + ", '" +
                            recaudacionInputDTO.FechaInicial + "', '" + recaudacionInputDTO.FechaFinal + "'  ";

                da = new SqlDataAdapter(command);
                da.Fill(ds);

                // Recaudacion Resultado
                recaudacionDTOList = Funciones.DataTableToList<RecaudacionDTO>(ds.Tables[0]);
                dtRecaudaciones = ds.Tables[0];

                return recaudacionDTOList;
            }
            catch (SqlException ex) // Manejo de errores de conexión y SP
            {
                // Aquí podrías registrar el error si es necesario
                Debug.WriteLine("============= Exception =============");
                Debug.WriteLine(ex.ToString());
                msgError = ex.Message;
                throw new Exception("[No se pudo procesar " + debug + "] - " + ex.Message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("============= Exception =============");
                Debug.WriteLine(ex.ToString());
                msgError = ex.Message;
                throw new Exception("[No se pudo procesar " + debug + "] - " + ex.Message);
            }
            finally
            {
                if (Conn != null)
                {
                    if (Conn.State == ConnectionState.Open)
                    {
                        Conn.Close();
                        Conn.Dispose();
                    }
                }
            }
        }

    }
}