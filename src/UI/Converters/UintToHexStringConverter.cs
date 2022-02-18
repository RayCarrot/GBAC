using System;
using System.Globalization;

namespace GBAC;

public class UintToHexStringConverter : BaseValueConverter<UintToHexStringConverter>
{
    public override object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return $"{(uint)value!:X8}";
    }

    public override object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        try
        {
            return System.Convert.ToUInt32((string?)value, 16);
        }
        catch
        {
            return 0;
        }
    }
}