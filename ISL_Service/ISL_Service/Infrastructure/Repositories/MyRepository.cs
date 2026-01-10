using System.Data;
using Microsoft.Data.SqlClient;
using ISL_Service.Application.DTOs.MyEntity;
using ISL_Service.Infrastructure.Data;

namespace ISL_Service.Infrastructure.Repositories
{

    public class MyRepository
    {

        private readonly string _connectionString;

        public MyRepository(string connectionString)
        {
            _connectionString = connectionString;

            //Mac3SqlServerConnector objConn = new Mac3SqlServerConnector();
            //SqlConnection Conn = objConn.GetConnection;
        }

        public MyEntity GetById(int id)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand("sp_GetMyEntityById", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@Id", id);

                    using (var reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return new MyEntity
                            {
                                Id = (int)reader["Id"],
                                Name = (string)reader["Name"]
                            };
                        }
                    }
                }
            }
            return null;
        }

    }

}