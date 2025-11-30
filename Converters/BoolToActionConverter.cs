// Converters/BoolToActionConverter.cs

using System;
using System.Globalization;
using System.Windows.Data;

namespace SQLAtlas.Converters
{
    /// <summary>
    /// Converts boolean values to action strings for UI display.
    /// </summary>
    public class BoolToActionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isMasked)
            {
                return isMasked ? "REMOVE MASK" : "APPLY MASK";
            }
            return "CHECK MASK";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}