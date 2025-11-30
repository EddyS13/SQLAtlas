// Converters/ScriptToEnabledConverter.cs

using System;
using System.Globalization;
using System.Windows.Data;

namespace SQLAtlas.Converters
{
    public class ScriptToEnabledConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // If the MaintenanceScript is a string and is NOT "N/A" or empty, return true.
            if (value is string script)
            {
                return !string.IsNullOrEmpty(script) && !script.Contains("N/A");
            }
            // Default to disabled
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}