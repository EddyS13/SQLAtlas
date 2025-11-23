// Converters/BoolToActionConverter.cs

using System;
using System.Globalization;
using System.Windows.Data;

namespace DatabaseVisualizer.Converters
{
    public class BoolToActionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isMasked)
            {
                // If the column is currently masked (True), the action is to REMOVE the mask.
                // If the column is not masked (False), the action is to APPLY the mask.
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