using System;
using System.Globalization;
using System.Windows.Data;

namespace SQLAtlas.Converters
{
    /// <summary>
    /// Converter that returns the type name of an object for use in XAML bindings and triggers.
    /// </summary>
    public class TypeConverter : IValueConverter
    {
        /// <summary>
        /// Converts an object to its type name.
        /// </summary>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Return the type name of the value, or "Null" if value is null
            return value?.GetType().Name ?? "Null";
        }

        /// <summary>
        /// Not implemented - this converter is one-way only.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
