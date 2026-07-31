using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace JiraDesktop.Wpf.Converters;

/// <summary>
/// Converts a Jira status name to a coloured background <see cref="Brush"/> for the status badge in the grid.
/// </summary>
public class StatusToBrushConverter : IValueConverter
{
    /// <inheritdoc/>
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var status = (value?.ToString() ?? "").Trim().ToLowerInvariant();

        if (status == "done") return new SolidColorBrush(Color.FromRgb(34, 197, 94)); // green
        if (status.Contains("in progress")) return new SolidColorBrush(Color.FromRgb(59, 130, 246)); // blue
        if (status.Contains("under query") || status.Contains("pm to approve") || status.Contains("web range review"))
            return new SolidColorBrush(Color.FromRgb(245, 158, 11)); // amber
        if (status == "to do") return new SolidColorBrush(Color.FromRgb(107, 114, 128)); // gray

        return new SolidColorBrush(Color.FromRgb(148, 163, 184)); // neutral
    }

    /// <inheritdoc/>
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}
