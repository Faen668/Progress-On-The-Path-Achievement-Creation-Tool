using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AchievementCreationTool.Controls;

/// <summary>True shows it; with Invert, true hides it. Null and empty text count as false.</summary>
public sealed class Shows : IValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var on = value switch
        {
            bool b => b,
            string s => s.Length > 0,
            int n => n > 0,
            null => false,
            _ => true,
        };
        return on != Invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class Not : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is bool b ? !b : value;
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is bool b ? !b : value;
}
