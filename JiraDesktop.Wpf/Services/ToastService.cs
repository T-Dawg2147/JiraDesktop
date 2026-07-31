using Microsoft.Toolkit.Uwp.Notifications;

namespace JiraDesktop.Wpf.Services;

/// <summary>
/// Sends Windows desktop toast notifications using the UWP Notifications library.
/// Failures are silently swallowed so that missing notification support never crashes the app.
/// </summary>
public class ToastService
{
    /// <summary>
    /// Shows an informational toast notification with the specified title and message.
    /// </summary>
    public void ShowInfo(string title, string message)
    {
        try
        {
            new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .GetToastContent();
        }
        catch
        {
            // swallow if toast unavailable on machine
        }
    }
}
