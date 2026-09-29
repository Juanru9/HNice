using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace HNice.Util;

/// <summary>
/// Binds a group of RadioButtons (segmented control) to one value: checked when the bound value equals
/// ConverterParameter; checking it writes the parameter back, converted to the property type.
/// </summary>
public sealed class EqualsConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not true || parameter is null) return Binding.DoNothing;
        var underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
        if (underlying == typeof(string)) return parameter.ToString();
        if (underlying.IsEnum) return Enum.Parse(underlying, parameter.ToString()!);
        return System.Convert.ChangeType(parameter, underlying, CultureInfo.InvariantCulture);
    }
}

/// <summary>Visible when the value is non-null / non-empty / true (or the opposite with Invert).</summary>
public sealed class HasValueToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var has = value switch
        {
            null => false,
            bool b => b,
            string s => !string.IsNullOrWhiteSpace(s),
            int i => i != 0,
            _ => true,
        };
        return has ^ Invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
