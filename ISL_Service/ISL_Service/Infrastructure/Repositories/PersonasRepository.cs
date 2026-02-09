using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using ISL_Service.Application.DTOs.Persona;
using ISL_Service.Application.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ISL_Service.Infrastructure.Repositories
{
    public class PersonasRepository : IPersonasRepository
    {
        private readonly string _connectionString;

        public PersonasRepository(IConfiguration configuration, IHostEnvironment env)
        {
            _connectionString = configuration.GetConnectionString("Main")
                ?? throw new Exception("ConnectionString 'Main' no encontrada.");
        }



        public async Task<List<PersonaDTO>> ConsultarAsync(int? idPersona, int? idStatus)
        {
            var result = new List<PersonaDTO>();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("dbo.sp_n_ConsultarPersonas", conn);

            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add(new SqlParameter("@IDPersona", SqlDbType.Int) { Value = (object?)idPersona ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@IDStatus", SqlDbType.Int) { Value = (object?)idStatus ?? DBNull.Value });

            await conn.OpenAsync();

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                var item = new PersonaDTO
                {
                    IDPersona = reader.GetInt32(reader.GetOrdinal("IDPersona")),
                    IDStatus = reader.GetInt32(reader.GetOrdinal("IDStatus")),
                    Cancelado = reader.GetInt32(reader.GetOrdinal("Cancelado")),
                    Nombre = reader.IsDBNull(reader.GetOrdinal("Nombre")) ? null : reader.GetString(reader.GetOrdinal("Nombre")),
                    IDUsuario = reader.GetInt32(reader.GetOrdinal("IDUsuario")),
                    Fecha = reader.GetDateTime(reader.GetOrdinal("Fecha")),
                    Equipo = reader.GetString(reader.GetOrdinal("Equipo")),
                    IDUsuarioModificacion = reader.IsDBNull(reader.GetOrdinal("IDUsuarioModificacion")) ? null : reader.GetInt32(reader.GetOrdinal("IDUsuarioModificacion")),
                    FechaModificacion = reader.IsDBNull(reader.GetOrdinal("FechaModificacion")) ? null : reader.GetDateTime(reader.GetOrdinal("FechaModificacion")),
                    EquipoModificacion = reader.IsDBNull(reader.GetOrdinal("EquipoModificacion")) ? null : reader.GetString(reader.GetOrdinal("EquipoModificacion"))
                };

                result.Add(item);
            }

            return result;
        }

        public async Task<int> InsertarAsync(PersonaInputDTO input)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("dbo.sp_n_InsertarPersona", conn);

            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add(new SqlParameter("@Nombre", SqlDbType.VarChar, 1000) { Value = (object?)input.Nombre ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@IDUsuario", SqlDbType.Int) { Value = input.IDUsuario });
            cmd.Parameters.Add(new SqlParameter("@Equipo", SqlDbType.VarChar, 400) { Value = input.Equipo });

            await conn.OpenAsync();

            var scalar = await cmd.ExecuteScalarAsync();

            if (scalar == null || scalar == DBNull.Value)
            {
                throw new Exception("No se pudo obtener el IDPersona insertado.");
            }

            return Convert.ToInt32(scalar);
        }

        public async Task ActualizarAsync(PersonaInputDTO input)
        {
            if (!input.IDPersona.HasValue)
            {
                throw new Exception("IDPersona es requerido para actualizar.");
            }

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("dbo.sp_n_ActualizarPersona", conn);

            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add(new SqlParameter("@IDPersona", SqlDbType.Int) { Value = input.IDPersona.Value });
            cmd.Parameters.Add(new SqlParameter("@Nombre", SqlDbType.VarChar, 1000) { Value = (object?)input.Nombre ?? DBNull.Value });
            cmd.Parameters.Add(new SqlParameter("@IDUsuarioModificacion", SqlDbType.Int) { Value = input.IDUsuario });
            cmd.Parameters.Add(new SqlParameter("@EquipoModificacion", SqlDbType.VarChar, 400) { Value = input.Equipo });

            await conn.OpenAsync();

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task CancelarAsync(int idPersona, int idUsuarioModificacion, string equipoModificacion)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand("dbo.sp_n_CancelarPersona", conn);

            cmd.CommandType = CommandType.StoredProcedure;

            cmd.Parameters.Add(new SqlParameter("@IDPersona", SqlDbType.Int) { Value = idPersona });
            cmd.Parameters.Add(new SqlParameter("@IDUsuarioModificacion", SqlDbType.Int) { Value = idUsuarioModificacion });
            cmd.Parameters.Add(new SqlParameter("@EquipoModificacion", SqlDbType.VarChar, 400) { Value = equipoModificacion });

            await conn.OpenAsync();

            await cmd.ExecuteNonQueryAsync();
        }
    }
}
