using System.Windows;

namespace JiraDesktop.Wpf.Services;

/// <summary>
/// Switches the active WPF theme by swapping the top-level theme <see cref="ResourceDictionary"/>
/// in <see cref="Application.Current"/> resources.
/// </summary>
public class ThemeService
{
    /// <summary>
    /// Applies the named theme by replacing the currently loaded theme dictionary.
    /// Supported names: "JiraLight", "JiraDark".
    /// </summary>
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
