using System.Drawing;
using System.Windows.Forms;

namespace JiraDesktop.Wpf.Services;

public sealed class ToastService : IDisposable
{
    private readonly NotifyIcon _notifyIcon = new()
    {
        Visible = true,
        Icon = SystemIcons.Information,
        Text = "Jira Desktop"
    };

    public void ShowInfo(string title, string message) => Show(title, message, ToolTipIcon.Info);
    public void ShowSuccess(string title, string message) => Show(title, message, ToolTipIcon.Info);
    public void ShowError(string title, string message) => Show(title, message, ToolTipIcon.Error);

    private void Show(string title, string message, ToolTipIcon icon)
    {
        try
        {
            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText = message;
            _notifyIcon.BalloonTipIcon = icon;
            _notifyIcon.ShowBalloonTip(5000);
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
