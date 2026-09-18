using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace JiraDesktop.Core.Models;

public sealed class UserProfile : INotifyPropertyChanged
{
    private string _id = Guid.NewGuid().ToString("n");
    private string _displayName = string.Empty;
    private UserRole _role = UserRole.Viewer;
    private string _managedProductManager = string.Empty;
    private UserWidgetSettings _settings = new();

    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public UserRole Role
    {
        get => _role;
        set => SetProperty(ref _role, value);
    }

    public string ManagedProductManager
    {
        get => _managedProductManager;
        set => SetProperty(ref _managedProductManager, value);
    }

    public UserWidgetSettings Settings
    {
        get => _settings;
        set => SetProperty(ref _settings, value ?? new UserWidgetSettings());
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
