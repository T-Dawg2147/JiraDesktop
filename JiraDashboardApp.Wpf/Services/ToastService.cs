using Microsoft.Toolkit.Uwp.Notifications;

namespace JiraDashboardApp.Wpf.Services;

public class ToastService
{
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