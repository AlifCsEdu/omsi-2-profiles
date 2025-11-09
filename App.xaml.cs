using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using OMSIProfileManager.Services;
using OMSIProfileManager.ViewModels;
using OMSIProfileManager.Views;
using Serilog;
using System;
using System.IO;

namespace OMSIProfileManager;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private Window? _mainWindow;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Gets the current App instance as a strongly-typed object.
    /// </summary>
    public static new App Current => (App)Application.Current;

    /// <summary>
    /// Gets the service provider for dependency injection.
    /// </summary>
    public IServiceProvider Services => _serviceProvider;

    /// <summary>
    /// Gets the main window.
    /// </summary>
    public Window? MainWindow => _mainWindow;

    /// <summary>
    /// Initializes the singleton application object.
    /// This is the first line of authored code executed, and as such
    /// is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();

        // Configure logging first
        ConfigureLogging();

        // Build service provider with DI container
        _serviceProvider = ConfigureServices();

        // Log application startup
        var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
        logger.LogInformation("OMSI Profile Manager starting up...");
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        _mainWindow.Activate();
    }

    /// <summary>
    /// Configures Serilog logging to file.
    /// </summary>
    private void ConfigureLogging()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "OMSIProfileManager");
        var logsFolder = Path.Combine(appFolder, "logs");

        // Ensure logs directory exists
        Directory.CreateDirectory(logsFolder);

        var logFilePath = Path.Combine(logsFolder, $"app_{DateTime.Now:yyyyMMdd}.log");

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                logFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("Logging initialized at: {LogPath}", logFilePath);
    }

    /// <summary>
    /// Configures dependency injection services.
    /// </summary>
    private IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Configuration
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "OMSIProfileManager");
        Directory.CreateDirectory(appFolder);

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true)
            .Build();

        services.AddSingleton<IConfiguration>(configuration);

        // Logging
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddSerilog(dispose: true);
        });

        // Register services (interfaces will be created in next step)
        services.AddSingleton<IOMSIPathDetector, OMSIPathDetector>();
        services.AddSingleton<IConfigurationService, ConfigurationService>();
        services.AddSingleton<IProfileManager, ProfileManager>();
        services.AddSingleton<IAddonScanner, AddonScanner>();
        services.AddSingleton<IOMSILauncher, OMSILauncher>();
        services.AddSingleton<IBackupManager, BackupManager>();

        // Register ViewModels
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<NewProfileViewModel>();
        services.AddTransient<SettingsViewModel>();

        // Register Views
        services.AddSingleton<MainWindow>();
        services.AddTransient<NewProfileDialog>();
        services.AddTransient<EditProfileDialog>();
        services.AddTransient<SettingsDialog>();

        return services.BuildServiceProvider();
    }
}
