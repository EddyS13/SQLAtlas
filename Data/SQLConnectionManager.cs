using System;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using System.Collections.Generic;

namespace SQLAtlas.Data
{
    public static class SqlConnectionManager
    {
        private static string? _connectionString;
        private const int CommandTimeoutSeconds = 300;

        public static string? GetLastConnectionString() => _connectionString;
        public static string? GetCurrentConnectionString() => _connectionString;
        public static string? CurrentDatabaseName { get; private set; }

        public static async Task<bool> TestConnection(string server, string database, string user, string password, bool useWindowsAuth)
        {
            try
            {
                SqlConnectionStringBuilder builder = new SqlConnectionStringBuilder
                {
                    DataSource = server,
                    InitialCatalog = database,
                    TrustServerCertificate = true,
                    Encrypt = false,
                    ConnectTimeout = 10
                };

                if (useWindowsAuth) builder.IntegratedSecurity = true;
                else { builder.UserID = user; builder.Password = password; }

                using (var connection = new SqlConnection(builder.ConnectionString))
                {
                    await connection.OpenAsync();
                }

                _connectionString = builder.ConnectionString;
                CurrentDatabaseName = database;
                return true;
            }
            catch { return false; }
        }

        public static async Task<Dictionary<string, List<SQLAtlas.Models.DatabaseObject>>> GetGroupedObjects(string connStr)
        {
            var grouped = new Dictionary<string, List<SQLAtlas.Models.DatabaseObject>>();

            // Ensure these keys match exactly what you want the Sidebar to show
            var categories = new Dictionary<string, string>
                {
                    { "U", "Tables" },
                    { "V", "Views" },
                    { "P", "Stored Procedures" },
                    { "FN", "Scalar Functions" },
                    { "TF", "Table Functions" }, // Standard Table Function
                    { "IF", "Table Functions" }  // Inline Table Function
                };

            // Initialize the dictionary keys based on UNIQUE friendly names
            foreach (var catName in categories.Values.Distinct())
            {
                if (!grouped.ContainsKey(catName)) grouped.Add(catName, new List<SQLAtlas.Models.DatabaseObject>());
            }

            using (var conn = new SqlConnection(connStr))
            {
                await conn.OpenAsync();
                // Updated SQL to include 'IF' (Inline Functions)
                string sql = "SELECT name, type, SCHEMA_NAME(schema_id) as SchemaName, SCHEMA_NAME(schema_id) + '.' + name as FullName FROM sys.objects WHERE type IN ('U', 'V', 'P', 'FN', 'TF', 'IF') AND is_ms_shipped = 0 ORDER BY name";

                using (var cmd = new SqlCommand(sql, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        string type = reader["type"].ToString()?.Trim() ?? "";
                        if (categories.TryGetValue(type, out string? catName))
                        {
                            grouped[catName].Add(new SQLAtlas.Models.DatabaseObject
                            {
                                Name = reader["name"].ToString()!,
                                SchemaName = reader["SchemaName"].ToString()!,
                                FullName = reader["FullName"].ToString()!,
                                // Assign the friendly name so the UI knows which folder to use
                                Type = catName,
                                TypeDescription = catName
                            });
                        }
                    }
                }
            }
            return grouped;
        }

        public static object? ExecuteScalar(string sqlQuery, Dictionary<string, object>? parameters = null)
        {
            if (string.IsNullOrEmpty(_connectionString)) return null;

            using (var connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString))
            {
                connection.Open();
                using (var command = new Microsoft.Data.SqlClient.SqlCommand(sqlQuery, connection))
                {
                    command.CommandTimeout = CommandTimeoutSeconds;
                    if (parameters != null)
                    {
                        foreach (var p in parameters)
                            command.Parameters.AddWithValue(p.Key, p.Value ?? DBNull.Value);
                    }
                    return command.ExecuteScalar();
                }
            }
        }

        public static DataTable ExecuteQuery(string sqlQuery, Dictionary<string, object>? parameters = null)
        {
            if (string.IsNullOrEmpty(_connectionString))
            {
                // Return an empty table rather than null to prevent crashes in MetadataServices
                return new DataTable();
            }

            DataTable dataTable = new DataTable();
            try
            {
                using (var connection = new SqlConnection(_connectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(sqlQuery, connection))
                    {
                        // Set a generous timeout for complex metadata queries
                        command.CommandTimeout = CommandTimeoutSeconds;

                        // Add parameters if any (prevents SQL injection)
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
                return new DataTable();
            }
        }

        public static void Disconnect()
        {
            _connectionString = null;
            CurrentDatabaseName = null;
            Microsoft.Data.SqlClient.SqlConnection.ClearAllPools();
        }

        public static void ExecuteNonQuery(string sql)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                using (SqlCommand cmd = new SqlCommand(sql, conn))
                {
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
        }

        public static DataTable ExecuteQueryOnSpecificConnection(string query, string connectionString)
        {
            DataTable dt = new DataTable();
            // Using 'using' ensures the connection is closed and disposed even if an error occurs
            using (Microsoft.Data.SqlClient.SqlConnection conn = new Microsoft.Data.SqlClient.SqlConnection(connectionString))
            {
                using (Microsoft.Data.SqlClient.SqlCommand cmd = new Microsoft.Data.SqlClient.SqlCommand(query, conn))
                {
                    cmd.CommandTimeout = 60; // Schema queries can take a moment on large DBs
                    conn.Open();
                    using (Microsoft.Data.SqlClient.SqlDataAdapter adapter = new Microsoft.Data.SqlClient.SqlDataAdapter(cmd))
                    {
                        adapter.Fill(dt);
                    }
                }
            }
            return dt;
        }

    }
}