using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;

namespace DatabaseVisualizer.Data
{
    public static class SqlConnectionManager
    {
        private static string? _connectionString;

        /// <summary>
        /// Clears the stored connection string, allowing the user to connect to a new database.
        /// </summary>
        public static void Disconnect()
        {
            _connectionString = null;
        }

        /// <summary>
        /// Executes a simple command that does not return data (e.g., DDL like ALTER INDEX).
        /// </summary>
        public static void ExecuteNonQuery(string sqlQuery)
        {
            if (string.IsNullOrEmpty(_connectionString))
            {
                throw new InvalidOperationException("Cannot execute query: Database connection is not established.");
            }

            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new SqlCommand(sqlQuery, connection))
                {
                    command.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Executes a query and returns the single value in the first column of the first row (e.g., OBJECT_ID, COUNT).
        /// </summary>
        public static object? ExecuteScalar(string sqlQuery)
        {
            if (string.IsNullOrEmpty(_connectionString))
            {
                return null; // Connection not established
            }

            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(sqlQuery, connection))
                    {
                        // Returns object or DBNull.Value (which is correctly handled as nullable object?)
                        return command.ExecuteScalar();
                    }
                }
            }
            catch (SqlException ex)
            {
                Console.WriteLine($"ExecuteScalar Failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Attempts to establish and store a connection to the SQL Server database.
        /// </summary>
        public static async Task<bool> TestConnection(string server, string database, string user, string password, bool useWindowsAuth)
        {
            try
            {
                SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder
                {
                    DataSource = server,
                    InitialCatalog = database,
                    TrustServerCertificate = true,
                    Encrypt = false
                };

                if (useWindowsAuth)
                {
                    builder.IntegratedSecurity = true;
                }
                else
                {
                    builder.UserID = user;
                    builder.Password = password;
                }

                using (var connection = new SqlConnection(builder.ConnectionString))
                {
                    await connection.OpenAsync();
                }

                _connectionString = builder.ConnectionString;
                return true;
            }
            catch (SqlException ex)
            {
                Console.WriteLine($"Connection Failed: {ex.Message}");
                _connectionString = null;
                return false;
            }
        }

        /// <summary>
        /// Executes a T-SQL query and returns the results as a DataTable.
        /// </summary>
        public static DataTable ExecuteQuery(string sqlQuery, Dictionary<string, object>? parameters = null)
        {
            if (string.IsNullOrEmpty(_connectionString))
            {
                // CRITICAL FIX: Use null-forgiving operator (!)
                return null!;
            }

            DataTable dataTable = new DataTable();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(sqlQuery, connection))
                    {
                        if (parameters != null)
                        {
                            foreach (var param in parameters)
                            {
                                command.Parameters.AddWithValue(param.Key, param.Value ?? DBNull.Value);
                            }
                        }

                        using (var adapter = new SqlDataAdapter(command))
                        {
                            adapter.Fill(dataTable);
                        }
                    }
                }
                return dataTable;
            }
            catch (SqlException ex)
            {
                Console.WriteLine($"Query Execution Failed: {ex.Message}");
                // CRITICAL FIX: Use null-forgiving operator (!)
                return null!;
            }
        }

    }
}