using System;
using System.Globalization;
using System.Windows;

namespace GBAC;

public class InvertedBooleanToVisibilityConverter : BaseValueConverter<InvertedBooleanToVisibilityConverter>
{
    public override object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (bool)value! ? Visibility.Collapsed : Visibility.Visible;
    }

    public override object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return (Visibility)value! != Visibility.Visible;
    }
}