using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace JiraDesktop.Wpf.Converters;

/// <summary>
/// Converts a <see cref="bool"/> or <see cref="Nullable{Boolean}"/> to a <see cref="Visibility"/> value.
/// <c>true</c> → <see cref="Visibility.Visible"/>; <c>false</c> or <c>null</c> → <see cref="Visibility.Collapsed"/>.
/// </summary>
public sealed class BooleanToVisibilityConverter : IValueConverter
{
    /// <summary>Shared singleton instance for use in XAML without requiring a resource entry.</summary>
    public static BooleanToVisibilityConverter Instance = new BooleanToVisibilityConverter();

    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool bValue = false;
        if (value is bool b)
        {
            bValue = b;
        }
        else if (value is bool nb)
        {
            bValue = nb;
        }
        return bValue ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Visibility v)
            return v == Visibility.Visible;
        return false;
    }
}
