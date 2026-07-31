using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace JiraDashboardApp.Wpf.Converters;

public class DueDateStateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not DateTime due) return Brushes.Transparent;

        var today = DateTime.Today;
        if (due.Date < today) return new SolidColorBrush(Color.FromRgb(254, 226, 226));      // overdue red tint
        if (due.Date <= today.AddDays(3)) return new SolidColorBrush(Color.FromRgb(254, 243, 199)); // soon amber tint
        return Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => Binding.DoNothing;
}