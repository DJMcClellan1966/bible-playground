using System.Globalization;

namespace AI_Bible_App.Maui.Converters;

/// <summary>
/// Converts DateTime to relative time string (e.g., "2h ago", "Yesterday", "3 days ago")
/// </summary>
public class RelativeTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not DateTime dateTime)
            return "Unknown";

        var now = DateTime.Now;
        var diff = now - dateTime;

        if (diff.TotalMinutes < 1)
            return "Just now";
        if (diff.TotalMinutes < 60)
            return $"{(int)diff.TotalMinutes}m ago";
        if (diff.TotalHours < 24)
            return $"{(int)diff.TotalHours}h ago";
        if (diff.TotalDays < 2)
            return "Yesterday";
        if (diff.TotalDays < 7)
            return $"{(int)diff.TotalDays}d ago";
        if (diff.TotalDays < 30)
            return $"{(int)(diff.TotalDays / 7)}w ago";
        if (diff.TotalDays < 365)
            return dateTime.ToString("MMM d");
        
        return dateTime.ToString("MMM d, yyyy");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
