using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace manitaDeGatoWeb.Data
{
    public class DataBaseHelper
    {
        private readonly string _connectionString;

        public DataBaseHelper(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// Obtiene una nueva conexion a la base de datos.
        /// </summary>
        public SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }

        /// <summary>
        /// Ejecuta una consulta SELECT y devuelve un DataTable.
        /// </summary>
        public async Task<DataTable> ExecuteQueryAsync(string query, params SqlParameter[] parameters)
        {
            using (var connection = GetConnection())
            {
                using (var command = new SqlCommand(query, connection))
                {
                    if (parameters != null)
                    {
                        command.Parameters.AddRange(parameters);
                    }

                    using (var adapter = new SqlDataAdapter(command))
                    {
                        var dataTable = new DataTable();
                        // El SqlDataAdapter no tiene metodos asincronos nativos de relleno en .NET Standard/Framework con SqlClient clasico,
                        // pero podemos abrir la conexion asincronamente y luego llenar.
                        await connection.OpenAsync();
                        adapter.Fill(dataTable);
                        return dataTable;
                    }
                }
            }
        }

        /// <summary>
        /// Ejecuta una instruccion INSERT, UPDATE o DELETE y devuelve el numero de filas afectadas.
        /// </summary>
        public async Task<int> ExecuteNonQueryAsync(string query, params SqlParameter[] parameters)
        {
            using (var connection = GetConnection())
            {
                using (var command = new SqlCommand(query, connection))
                {
                    if (parameters != null)
                    {
                        command.Parameters.AddRange(parameters);
                    }

                    await connection.OpenAsync();
                    return await command.ExecuteNonQueryAsync();
                }
            }
        }

        /// <summary>
        /// Ejecuta una consulta que devuelve un unico valor escalar.
        /// </summary>
        public async Task<object> ExecuteScalarAsync(string query, params SqlParameter[] parameters)
        {
            using (var connection = GetConnection())
            {
                using (var command = new SqlCommand(query, connection))
                {
                    if (parameters != null)
                    {
                        command.Parameters.AddRange(parameters);
                    }

                    await connection.OpenAsync();
                    return await command.ExecuteScalarAsync();
                }
            }
        }
    }
}
