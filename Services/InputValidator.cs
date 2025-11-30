using System;
using System.Text.RegularExpressions;

namespace SQLAtlas.Services
{
    /// <summary>
    /// Provides input validation utilities for security purposes.
    /// </summary>
    public static class InputValidator
    {
        /// <summary>
        /// Validates SQL Server object names (tables, columns, schemas).
        /// </summary>
        public static bool IsValidObjectName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 128)
                return false;

            // Allow alphanumeric, underscore, and @/# for temp objects
            return Regex.IsMatch(name, @"^[a-zA-Z_@#][a-zA-Z0-9_@#]*$");
        }

        /// <summary>
        /// Validates schema names.
        /// </summary>
        public static bool IsValidSchemaName(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length > 128)
                return false;

            return Regex.IsMatch(name, @"^[a-zA-Z_][a-zA-Z0-9_]*$");
        }

        /// <summary>
        /// Sanitizes error messages to prevent information disclosure.
        /// </summary>
        public static string SanitizeErrorMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return "An error occurred.";

            // Remove SQL Server specific error details
            return message
                .Replace("FROM sys.", "")
                .Replace("OBJECT_ID", "")
                .Replace("SQL Server", "");
        }

        /// <summary>
        /// Validates connection string doesn't contain hardcoded credentials.
        /// </summary>
        public static bool IsSecureConnectionString(string connectionString)
        {
            if (string.IsNullOrEmpty(connectionString))
                return false;

            // Should use Integrated Security or encrypted passwords
            bool hasIntegratedSecurity = connectionString.Contains("Integrated Security=true", StringComparison.OrdinalIgnoreCase);
            bool hasEncryption = connectionString.Contains("Encrypt=true", StringComparison.OrdinalIgnoreCase);
            bool hasPlainPassword = Regex.IsMatch(connectionString, @"Password\s*=\s*[^;]+", RegexOptions.IgnoreCase);

            return hasIntegratedSecurity || (hasEncryption && !hasPlainPassword);
        }
    }
}