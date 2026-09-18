using System.Drawing;
using System.Windows;
using System.Windows.Forms;

namespace JiraDesktop.Wpf.Services;

public sealed class ToastService : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private CancellationTokenSource? _disposeCts;

    public void ShowInfo(string title, string message) => Show(title, message, ToolTipIcon.Info);
    public void ShowSuccess(string title, string message) => Show(title, message, ToolTipIcon.Info);
    public void ShowError(string title, string message) => Show(title, message, ToolTipIcon.Error);

    private void Show(string title, string message, ToolTipIcon icon)
    {
        try
        {
            _disposeCts?.Cancel();
            _notifyIcon ??= new NotifyIcon
            {
                Icon = SystemIcons.Information,
                Text = "Jira Desktop"
            };

            _notifyIcon.Visible = true;
            _notifyIcon.BalloonTipTitle = title;
            _notifyIcon.BalloonTipText = message;
            _notifyIcon.BalloonTipIcon = icon;
            _notifyIcon.ShowBalloonTip(5000);

            _disposeCts = new CancellationTokenSource();
            _ = DisposeIconLaterAsync(_disposeCts.Token);
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        _disposeCts?.Cancel();
        DisposeIcon();
    }

    private async Task DisposeIconLaterAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(6000, cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
            {
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher is null || dispatcher.CheckAccess())
                    DisposeIcon();
                else
                    await dispatcher.InvokeAsync(DisposeIcon);
            }
        }
        catch
        {
        }
    }

    private void DisposeIcon()
    {
        if (_notifyIcon is null)
            return;

        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _notifyIcon = null;
    }
}
