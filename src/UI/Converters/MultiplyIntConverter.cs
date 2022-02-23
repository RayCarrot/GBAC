using System;
using System.Globalization;

namespace GBAC;

public class MultiplyIntConverter : BaseValueConverter<MultiplyIntConverter>
{
    public override object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (int)value! * Int32.Parse(parameter!.ToString());
}