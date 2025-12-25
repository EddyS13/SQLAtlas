using System;

namespace SQLAtlas
{
    /// <summary>
    /// Global state container for the active database session.
    /// This allows all views (Performance, Security, Schema Explorer) 
    /// to access the connection string without re-passing it.
    /// </summary>
    public static class CurrentSession
    {
        /// <summary>
        /// The active connection string used to communicate with SQL Server.
        /// </summary>
        public static string? ConnectionString { get; set; }

        /// <summary>
        /// The name of the database currently being explored.
        /// </summary>
        public static string? DatabaseName { get; set; }

        /// <summary>
        /// The name or IP address of the SQL Server instance.
        /// </summary>
        public static string? ServerName { get; set; }

        /// <summary>
        /// Optional: Tracks the last time a successful connection was established.
        /// </summary>
        public static DateTime? LastConnected { get; set; }
    }
}