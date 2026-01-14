using System.Globalization;

namespace AI_Bible_App.Maui.Converters;

/// <summary>
/// Converts message role to appropriate background color for ChatGPT-style layout
/// User messages: transparent, Assistant messages: card background
/// </summary>
public class RoleToBackgroundConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var role = value as string;
        return role switch
        {
            "user" => Colors.Transparent,
            "assistant" => Application.Current?.Resources["HallowCardBackground"] ?? Color.FromArgb("#1E1833"),
            _ => Colors.Transparent
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts message role to avatar emoji (user gets person, assistant gets character emoji)
/// </summary>
public class RoleToAvatarConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var role = value as string;
        return role switch
        {
            "user" => "👤",
            "assistant" => "📖", // Will be overridden by character emoji in actual use
            _ => "💬"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts message role to avatar background color
/// </summary>
public class RoleToAvatarColorConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var role = value as string;
        return role switch
        {
            "user" => Color.FromArgb("#6B46C1"), // Purple for user
            "assistant" => Colors.Transparent, // Gradient applied separately
            _ => Color.FromArgb("#505050")
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts message role to display name
/// </summary>
public class RoleToNameConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var role = value as string;
        return role switch
        {
            "user" => "You",
            "assistant" => "Assistant", // Will be overridden by character name
            _ => "System"
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
