using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SQLAtlas.Converters
{
    /// <summary>
    /// Converts a Boolean value to a Visibility enumeration.
    /// Used to show/hide the SQL Authentication fields in the UI.
    /// </summary>
    public class BoolToActionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isVisible = (bool)value;

            // If the parameter is "Invert", we flip the logic
            if (parameter != null && parameter.ToString() == "Invert")
            {
                isVisible = !isVisible;
            }

            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Visible;
            }
            return false;
        }
    }
}