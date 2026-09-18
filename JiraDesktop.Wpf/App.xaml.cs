using System.Windows;
using JiraDesktop.Core.Configuration;
using JiraDesktop.Core.Interfaces;
using JiraDesktop.Core.Services;
using JiraDesktop.Wpf.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JiraDesktop.Wpf;

public partial class App : System.Windows.Application
{
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
                services.Configure<JiraOptions>(context.Configuration.GetSection("Jira"));
                services.Configure<JiraOAuthOptions>(context.Configuration.GetSection("JiraOAuth"));

                services.AddSingleton<IWorkItemCacheService, FileWorkItemCacheService>();
                services.AddHttpClient<IJiraOAuthService, JiraOAuthService>();
                services.AddHttpClient<IJiraService, JiraService>();
                services.AddSingleton<DashboardService>();
                services.AddSingleton<UserProfileService>();

                services.AddSingleton<ThemeService>();
                services.AddSingleton<ToastService>();

                services.AddSingleton<MainWindowViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await AppHost.StartAsync();
        AppHost.Services.GetRequiredService<MainWindow>().Show();
        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await AppHost.StopAsync();
        AppHost.Dispose();
        base.OnExit(e);
    }
}
