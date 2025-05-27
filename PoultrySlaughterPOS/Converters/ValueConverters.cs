using System;
using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PoultrySlaughterPOS.Converters
{
    /// <summary>
    /// Enterprise-grade WPF value converters providing type-safe data transformation
    /// for UI binding scenarios in the Poultry Slaughter POS system
    /// </summary>

    #region Boolean Converters

    /// <summary>
    /// Converts boolean values to their inverse for UI binding scenarios
    /// </summary>
    [ValueConversion(typeof(bool), typeof(bool))]
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return Binding.DoNothing;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue;
            }
            return Binding.DoNothing;
        }
    }

    /// <summary>
    /// Converts boolean values to Visibility enumeration for UI element control
    /// </summary>
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
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

    /// <summary>
    /// Converts inverse boolean values to Visibility enumeration
    /// </summary>
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class InverseBooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return !boolValue ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility != Visibility.Visible;
            }
            return true;
        }
    }

    #endregion

    #region Null and Empty Converters

    /// <summary>
    /// Converts null objects to Visibility enumeration for conditional UI display
    /// </summary>
    [ValueConversion(typeof(object), typeof(Visibility))]
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack not supported for NullToVisibilityConverter");
        }
    }

    /// <summary>
    /// Converts null objects to inverse Visibility enumeration
    /// </summary>
    [ValueConversion(typeof(object), typeof(Visibility))]
    public class NullToInverseVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value == null ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack not supported for NullToInverseVisibilityConverter");
        }
    }

    #endregion

    #region Numeric Converters

    /// <summary>
    /// Converts zero numeric values to Visibility enumeration for conditional display
    /// </summary>
    [ValueConversion(typeof(object), typeof(Visibility))]
    public class ZeroToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return Visibility.Collapsed;

            switch (value)
            {
                case int intValue:
                    return intValue != 0 ? Visibility.Visible : Visibility.Collapsed;
                case double doubleValue:
                    return Math.Abs(doubleValue) > 0.001 ? Visibility.Visible : Visibility.Collapsed;
                case decimal decimalValue:
                    return decimalValue != 0 ? Visibility.Visible : Visibility.Collapsed;
                case float floatValue:
                    return Math.Abs(floatValue) > 0.001f ? Visibility.Visible : Visibility.Collapsed;
                default:
                    return Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack not supported for ZeroToVisibilityConverter");
        }
    }

    /// <summary>
    /// Converts non-zero numeric values to inverse Visibility enumeration
    /// </summary>
    [ValueConversion(typeof(object), typeof(Visibility))]
    public class ZeroToInverseVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return Visibility.Visible;

            switch (value)
            {
                case int intValue:
                    return intValue == 0 ? Visibility.Visible : Visibility.Collapsed;
                case double doubleValue:
                    return Math.Abs(doubleValue) <= 0.001 ? Visibility.Visible : Visibility.Collapsed;
                case decimal decimalValue:
                    return decimalValue == 0 ? Visibility.Visible : Visibility.Collapsed;
                case float floatValue:
                    return Math.Abs(floatValue) <= 0.001f ? Visibility.Visible : Visibility.Collapsed;
                default:
                    return Visibility.Visible;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack not supported for ZeroToInverseVisibilityConverter");
        }
    }

    #endregion

    #region Collection Converters

    /// <summary>
    /// Converts collection counts to Visibility enumeration for conditional UI display
    /// </summary>
    [ValueConversion(typeof(int), typeof(Visibility))]
    public class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch (value)
            {
                case int count:
                    return count > 0 ? Visibility.Visible : Visibility.Collapsed;
                case ICollection collection:
                    return collection.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                case IEnumerable enumerable:
                    return enumerable.Cast<object>().Any() ? Visibility.Visible : Visibility.Collapsed;
                default:
                    return Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack not supported for CountToVisibilityConverter");
        }
    }

    /// <summary>
    /// Converts empty collections to inverse Visibility enumeration
    /// </summary>
    [ValueConversion(typeof(int), typeof(Visibility))]
    public class EmptyCollectionToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch (value)
            {
                case int count:
                    return count == 0 ? Visibility.Visible : Visibility.Collapsed;
                case ICollection collection:
                    return collection.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                case IEnumerable enumerable:
                    return !enumerable.Cast<object>().Any() ? Visibility.Visible : Visibility.Collapsed;
                default:
                    return Visibility.Visible;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack not supported for EmptyCollectionToVisibilityConverter");
        }
    }

    #endregion

    #region String Converters

    /// <summary>
    /// Converts string values to Visibility enumeration based on null/empty state
    /// </summary>
    [ValueConversion(typeof(string), typeof(Visibility))]
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return !string.IsNullOrWhiteSpace(value as string) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack not supported for StringToVisibilityConverter");
        }
    }

    /// <summary>
    /// Converts empty string values to Visibility enumeration
    /// </summary>
    [ValueConversion(typeof(string), typeof(Visibility))]
    public class EmptyStringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrWhiteSpace(value as string) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack not supported for EmptyStringToVisibilityConverter");
        }
    }

    #endregion

    #region Arithmetic Converters

    /// <summary>
    /// Performs mathematical operations on numeric values for UI calculations
    /// </summary>
    [ValueConversion(typeof(double), typeof(double))]
    public class MathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double doubleValue && parameter is string operation)
            {
                var parts = operation.Split(',');
                if (parts.Length == 2 && double.TryParse(parts[1], out var operand))
                {
                    return parts[0].ToLower() switch
                    {
                        "add" => doubleValue + operand,
                        "subtract" => doubleValue - operand,
                        "multiply" => doubleValue * operand,
                        "divide" => operand != 0 ? doubleValue / operand : doubleValue,
                        _ => doubleValue
                    };
                }
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack not supported for MathConverter");
        }
    }

    /// <summary>
    /// Formats decimal values for currency display with Arabic localization
    /// </summary>
    [ValueConversion(typeof(decimal), typeof(string))]
    public class CurrencyConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal decimalValue)
            {
                return decimalValue.ToString("N2", new CultureInfo("ar-IQ")) + " د.ع";
            }
            if (value is double doubleValue)
            {
                return doubleValue.ToString("N2", new CultureInfo("ar-IQ")) + " د.ع";
            }
            return "0.00 د.ع";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                var cleanValue = stringValue.Replace("د.ع", "").Replace(",", "").Trim();
                if (decimal.TryParse(cleanValue, out var result))
                {
                    return result;
                }
            }
            return 0m;
        }
    }

    #endregion

    #region Date Converters

    /// <summary>
    /// Converts DateTime values to Arabic date format strings
    /// </summary>
    [ValueConversion(typeof(DateTime), typeof(string))]
    public class ArabicDateConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dateValue)
            {
                var format = parameter as string ?? "dd/MM/yyyy";
                return dateValue.ToString(format, new CultureInfo("ar-IQ"));
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string stringValue)
            {
                if (DateTime.TryParse(stringValue, new CultureInfo("ar-IQ"), DateTimeStyles.None, out var result))
                {
                    return result;
                }
            }
            return DateTime.Now;
        }
    }

    /// <summary>
    /// Converts DateTime values to relative time strings (e.g., "منذ ساعتين")
    /// </summary>
    [ValueConversion(typeof(DateTime), typeof(string))]
    public class RelativeTimeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dateValue)
            {
                var timeSpan = DateTime.Now - dateValue;

                if (timeSpan.TotalMinutes < 1)
                    return "الآن";
                if (timeSpan.TotalMinutes < 60)
                    return $"منذ {(int)timeSpan.TotalMinutes} دقيقة";
                if (timeSpan.TotalHours < 24)
                    return $"منذ {(int)timeSpan.TotalHours} ساعة";
                if (timeSpan.TotalDays < 30)
                    return $"منذ {(int)timeSpan.TotalDays} يوم";

                return dateValue.ToString("dd/MM/yyyy", new CultureInfo("ar-IQ"));
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack not supported for RelativeTimeConverter");
        }
    }

    #endregion
}