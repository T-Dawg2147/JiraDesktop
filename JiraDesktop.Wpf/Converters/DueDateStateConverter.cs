using System.Globalization;
using Binding = System.Windows.Data.Binding;
using IValueConverter = System.Windows.Data.IValueConverter;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Color = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace JiraDesktop.Wpf.Converters;

/// <summary>
/// Returns a background <see cref="Brush"/> that visually highlights a due-date cell
/// based on its urgency: overdue (red tint), due within 3 days (amber tint), or transparent.
/// </summary>
public class DueDateStateConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DateTime due) return Brushes.Transparent;

        var today = DateTime.Today;
        if (due.Date < today) return new SolidColorBrush(Color.FromRgb(254, 226, 226));       // overdue — red tint
        if (due.Date <= today.AddDays(3)) return new SolidColorBrush(Color.FromRgb(254, 243, 199)); // soon — amber tint
        return Brushes.Transparent;
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
