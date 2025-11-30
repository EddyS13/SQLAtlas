// Converters/CollectionToVisibilityConverter.cs

using System;
using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SQLAtlas.Converters
{
    public class CollectionToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ICollection collection)
            {
                // Return Visible if the collection has items, Collapsed otherwise
                return collection.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            // If the binding failed or value is null, treat as collapsed
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}