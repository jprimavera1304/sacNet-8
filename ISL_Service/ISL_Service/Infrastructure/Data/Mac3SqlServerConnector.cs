using ISL_Service.Utils;
using Microsoft.Data.SqlClient;

namespace ISL_Service.Infrastructure.Data
{
    public class Mac3SqlServerConnector
    {


        private string ConnectionString = "Mac3";
        //private string ConnectionString = ConfigurationManager.ConnectionStrings["Mac3"].ConnectionString;

        private SqlConnection SqlConn = null;


        public SqlConnection GetConnection
        {
            get { return SqlConn; }
            set { SqlConn = value; }
        }

        public Mac3SqlServerConnector()
        {

            if (this.ConnectionString.Contains("Aviacion"))
            {
                string cadena = this.ConnectionString.Substring(0, 20);

                if (cadena.Contains("Aviacion"))
                {
                    // Cadena de Conexion Encriptada
                    this.ConnectionString = this.ConnectionString.Replace("Aviacion", "");

                    Encryptacion encryptacion = new Encryptacion();
                    string cadenaDesencriptada = encryptacion.Decrypt(this.ConnectionString);

                    this.ConnectionString = cadenaDesencriptada;
                }

            }

            if (this.ConnectionString.Contains("@Info"))
                this.ConnectionString = this.ConnectionString.Replace("@Info", "User Id=sa; Password=Hope5y2k");

            SqlConn = new SqlConnection(this.ConnectionString);
        }

        public Mac3SqlServerConnector(int NumCaja)
        {
            //string ConnectionStringServer = ConfigurationManager.ConnectionStrings["Mac3ServerAviacion"].ConnectionString;
            string ConnectionStringServer = "";
            ConnectionStringServer = ConnectionStringServer.Replace("@Info", "Initial Catalog=MacAviacion_SC" + NumCaja.ToString() + ";  User Id=sa; Password=Hope5y2k");

            SqlConn = new SqlConnection(ConnectionStringServer);
        }

        public Mac3SqlServerConnector(string connectionString)
        {
            SqlConn = new SqlConnection(connectionString);
        }

    }
}