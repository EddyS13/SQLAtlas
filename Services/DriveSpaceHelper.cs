using System;
using System.Collections.Generic;
using System.Management;

namespace SQLAtlas.Services
{
    /// <summary>
    /// Helper class for retrieving drive space information using WMI.
    /// This allows retrieval of total capacity and free space for physical drives.
    /// </summary>
    public static class DriveSpaceHelper
    {
        /// <summary>
        /// Retrieves drive space information for all physical drives using WMI.
        /// </summary>
        /// <returns>Dictionary with drive letter as key and tuple of (TotalGB, FreeGB)</returns>
        public static Dictionary<string, (decimal TotalGB, decimal FreeGB)> GetDriveSpaceFromWMI()
        {
            var driveInfo = new Dictionary<string, (decimal, decimal)>();

            try
            {
                // WMI query to get logical disk information
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(
                    "SELECT Name, Size, FreeSpace FROM Win32_LogicalDisk WHERE DriveType = 3");

                using (searcher)
                {
                    ManagementObjectCollection results = searcher.Get();

                    foreach (ManagementObject disk in results)
                    {
                        try
                        {
                            string driveLetter = disk["Name"]?.ToString() ?? string.Empty;
                            
                            // Get size in bytes and convert to GB
                            if (ulong.TryParse(disk["Size"]?.ToString() ?? "0", out ulong sizeBytes))
                            {
                                decimal totalGB = sizeBytes / (1024m * 1024m * 1024m);

                                if (ulong.TryParse(disk["FreeSpace"]?.ToString() ?? "0", out ulong freeBytes))
                                {
                                    decimal freeGB = freeBytes / (1024m * 1024m * 1024m);

                                    if (!string.IsNullOrEmpty(driveLetter))
                                    {
                                        driveInfo[driveLetter] = (totalGB, freeGB);
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // Skip individual disks that fail
                            continue;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // WMI access failed - will be logged in calling method
                throw new InvalidOperationException($"WMI query failed: {ex.Message}", ex);
            }

            return driveInfo;
        }
    }
}