using System;
using System.Globalization;
using System.Windows;

namespace GBAC;

public class BooleanToVisibilityHiddenConverter : BaseValueConverter<BooleanToVisibilityHiddenConverter>
{
    public override object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (bool)value! ? Visibility.Visible : Visibility.Hidden;
    }

    public override object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (Visibility)value! == Visibility.Visible;
    }
}