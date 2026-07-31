using System.Windows;

namespace JiraDashboardApp.Wpf.Services;

public class ThemeService
{
    public void ApplyTheme(string themeName)
    {
        var app = Application.Current;
        if (app == null) return;

        var dictionaries = app.Resources.MergedDictionaries;
        var existingTheme = dictionaries.FirstOrDefault(d =>
            d.Source != null &&
            (d.Source.OriginalString.Contains("JiraLight.xaml") || d.Source.OriginalString.Contains("JiraDark.xaml")));

        if (existingTheme != null)
            dictionaries.Remove(existingTheme);

        var source = themeName == "JiraDark"
            ? new Uri("Themes/JiraDark.xaml", UriKind.Relative)
            : new Uri("Themes/JiraLight.xaml", UriKind.Relative);

        dictionaries.Add(new ResourceDictionary { Source = source });
    }
}