using System;
using System.Globalization;
using System.Windows.Data;

namespace SQLAtlas.Converters
{
    public class SqlDataTypeConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is string dataType)
            {
                return dataType switch
                {
                    "INT" => "Integer",
                    "VARCHAR" => "String",
                    "DATETIME" => "DateTime",
                    "DECIMAL" => "Decimal",
                    "BIT" => "Boolean",
                    _ => dataType
                };
            }
            return value ?? string.Empty;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
