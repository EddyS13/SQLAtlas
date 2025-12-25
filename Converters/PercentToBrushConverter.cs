using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SQLAtlas.Converters
{
    public class PercentToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            double percent = (double)value;
            if (percent > 90) return Brushes.Red;
            if (percent > 75) return Brushes.Orange;
            return (SolidColorBrush)App.Current.FindResource("AccentColor");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}