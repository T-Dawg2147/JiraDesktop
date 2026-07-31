using System.Windows;
using JiraDesktop.Core.Configuration;
using JiraDesktop.Core.Interfaces;
using JiraDesktop.Core.Services;
using JiraDesktop.Wpf.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JiraDesktop.Wpf;

/// <summary>
/// Application entry point. Bootstraps the .NET Generic Host, registers all services
/// via dependency injection, and launches the main window.
/// </summary>
public partial class App : Application
{
    /// <summary>The application's DI host, accessible for manual service resolution if needed.</summary>
    public static IHost AppHost { get; private set; } = null!;

    public App()
    {
        AppHost = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration((_, config) =>
            {
                config.SetBasePath(AppContext.BaseDirectory);
                config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                // Configuration
                services.Configure<JiraOptions>(context.Configuration.GetSection("Jira"));
                services.Configure<JiraOAuthOptions>(context.Configuration.GetSection("JiraOAuth"));

                // Core services
                services.AddSingleton<IWorkItemCacheService, FileWorkItemCacheService>();
                services.AddHttpClient<IJiraOAuthService, JiraOAuthService>();
                services.AddHttpClient<IJiraService, JiraService>();
                services.AddSingleton<DashboardService>();
                services.AddSingleton<UserSettingsService>();

                // WPF-specific services
                services.AddSingleton<ThemeService>();
                services.AddSingleton<ToastService>();

                // View models and windows
                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await AppHost.StartAsync();
        var window = AppHost.Services.GetRequiredService<MainWindow>();
        window.Show();
        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await AppHost.StopAsync();
        AppHost.Dispose();
        base.OnExit(e);
    }
}
