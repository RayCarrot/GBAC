using System;
using System.Globalization;
using System.Windows;

namespace GBAC;

public class BooleanToVisibilityConverter : BaseValueConverter<BooleanToVisibilityConverter>
{
    public override object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (bool)value! ? Visibility.Visible : Visibility.Collapsed;
    }

    public override object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (Visibility)value! == Visibility.Visible;
    }
}