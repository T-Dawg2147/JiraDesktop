using Microsoft.Toolkit.Uwp.Notifications;

namespace JiraDesktop.Wpf.Services;

public sealed class ToastService
{
    public void ShowInfo(string title, string message) => Show(title, message);
    public void ShowSuccess(string title, string message) => Show(title, message);
    public void ShowError(string title, string message) => Show(title, message);

    private static void Show(string title, string message)
    {
        try
        {
            new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .Show();
        }
        catch
        {
        }
    }
}
