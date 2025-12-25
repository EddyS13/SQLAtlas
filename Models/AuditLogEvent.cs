using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLAtlas.Models
{
    public class AuditLogEvent
    {
        /// <summary>
        /// The timestamp of the event from the SQL Error Log.
        /// </summary>
        public DateTime LogDate { get; set; }

        /// <summary>
        /// Identifies the internal SQL process or SPID (e.g., 'Logon', 'spid52').
        /// </summary>
        public string ProcessInfo { get; set; } = string.Empty;

        /// <summary>
        /// The full descriptive text of the error or audit event.
        /// </summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Logic helper for the UI to determine if the message should be highlighted.
        /// This flags failed logins, high-severity errors, and permission denials.
        /// </summary>
        public bool IsCritical
        {
            get
            {
                if (string.IsNullOrEmpty(Message)) return false;

                // Look for common security/error keywords
                return Message.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
                       Message.Contains("Error", StringComparison.OrdinalIgnoreCase) ||
                       Message.Contains("denied", StringComparison.OrdinalIgnoreCase) ||
                       Message.Contains("Severity: 1", StringComparison.OrdinalIgnoreCase) || // Catches 11-19
                       Message.Contains("Severity: 2", StringComparison.OrdinalIgnoreCase);   // Catches 20-25
            }
        }

        /// <summary>
        /// Returns a cleaner version of the ProcessInfo for the UI.
        /// </summary>
        public string DisplayProcess => ProcessInfo?.Replace("spid", "ID: ") ?? "System";
    }
}
