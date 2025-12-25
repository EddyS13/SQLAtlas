using System;
using System.Globalization;
using System.Windows.Data;

namespace SQLAtlas.Converters
{
    public class SqlTypeToFriendlyNameConverter : IValueConverter
    {
        public SqlTypeToFriendlyNameConverter()
        {
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string sqlType)
            {
                string upperType = sqlType.ToUpper().Trim();

                return upperType switch
                {
                    // Handle Short Codes
                    "U" or "USER_TABLE" => "Tables",
                    "V" or "VIEW" => "Views",
                    "P" or "SQL_STORED_PROCEDURE" => "Stored Procedures",
                    "FN" or "SQL_SCALAR_FUNCTION" => "Scalar Functions",
                    "TF" or "IF" or "SQL_TABLE_VALUED_FUNCTION" or "SQL_INLINE_TABLE_VALUED_FUNCTION" => "Table Functions",
                    _ => sqlType // Fallback to the original string if unknown
                };
            }
            return value?.ToString() ?? "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) 
            => throw new NotImplementedException();
    }
}
