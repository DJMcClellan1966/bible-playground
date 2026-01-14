using System.Globalization;

namespace AI_Bible_App.Maui.Converters;

public class BoolToColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool boolValue)
            return Colors.Gray;
        
        // If parameter provided, use it for custom colors
        if (parameter is string colors)
        {
            var colorStrings = colors.Split(',');
            if (colorStrings.Length == 2)
            {
                var colorStr = boolValue ? colorStrings[0] : colorStrings[1];
                return Color.FromArgb(colorStr.Trim());
            }
        }
        
        // Default: purple for active, transparent for inactive (Hallow style)
        return boolValue ? Color.FromArgb("#7C3AED") : Colors.Transparent;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToStringConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not bool boolValue || parameter is not string strings)
            return string.Empty;

        // Support both ',' and '|' as separators
        var separator = strings.Contains('|') ? '|' : ',';
        var options = strings.Split(separator);
        if (options.Length != 2)
            return string.Empty;

        return boolValue ? options[0].Trim() : options[1].Trim();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class PercentToDecimalConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double percent)
            return percent / 100.0;
        
        return 0.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class BoolToDoubleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return boolValue ? 1.0 : 0.0;
        
        return 0.0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;
        
        return false;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
            return !boolValue;
        
        return true;
    }
}
