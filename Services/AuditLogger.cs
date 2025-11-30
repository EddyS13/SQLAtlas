using System;
using System.IO;

namespace SQLAtlas.Services
{
    /// <summary>
    /// Provides audit logging for sensitive operations.
    /// </summary>
    public static class AuditLogger
    {
        private static readonly string _logPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SQLAtlas", "Logs", "audit.log");

        static AuditLogger()
        {
            string? logDirectory = Path.GetDirectoryName(_logPath);

            if (logDirectory is not null)
            {
                Directory.CreateDirectory(logDirectory);
            }
        }

        /// <summary>
        /// Logs a sensitive operation.
        /// </summary>
        public static void LogSensitiveOperation(string operationType, string objectName, string details, bool success = true)
        {
            try
            {
                string logEntry = $"[{DateTime.UtcNow:O}] | Operation: {operationType} | Object: {objectName} | Status: {(success ? "SUCCESS" : "FAILED")} | Details: {details}";
                
                lock (_logPath)
                {
                    File.AppendAllText(_logPath, logEntry + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Audit log error: {ex.Message}");
            }
        }

        /// <summary>
        /// Logs data masking operations.
        /// </summary>
        public static void LogMaskingOperation(string tableName, string columnName, bool isMasking)
        {
            LogSensitiveOperation(
                "DATA_MASKING",
                $"{tableName}.{columnName}",
                $"Action: {(isMasking ? "APPLIED" : "REMOVED")}");
        }

        /// <summary>
        /// Logs permission changes.
        /// </summary>
        public static void LogPermissionChange(string principal, string objectName, string permission, string action)
        {
            LogSensitiveOperation(
                "PERMISSION_CHANGE",
                objectName,
                $"Principal: {principal} | Permission: {permission} | Action: {action}");
        }

        /// <summary>
        /// Logs configuration changes.
        /// </summary>
        public static void LogConfigurationChange(string settingName, string oldValue, string newValue)
        {
            LogSensitiveOperation(
                "CONFIG_CHANGE",
                settingName,
                $"From: {oldValue} | To: {newValue}");
        }
    }
}