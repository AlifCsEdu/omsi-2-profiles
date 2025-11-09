using Microsoft.Extensions.Logging;
using OMSIProfileManager.Models;
using OMSIProfileManager.Services;
using System.Threading.Tasks;

namespace OMSIProfileManager.ViewModels;

/// <summary>
/// ViewModel for the settings window.
/// </summary>
public class SettingsViewModel : ViewModelBase
{
    private readonly ILogger<SettingsViewModel> _logger;
    private readonly IConfigurationService _configService;

    private string _omsi2Path = string.Empty;
    private bool _autoBackupBeforeLoad = true;
    private bool _confirmBeforeLoad = false;
    private bool _showNotifications = true;
    private bool _autoLaunchOmsi = true;

    public SettingsViewModel(
        ILogger<SettingsViewModel> logger,
        IConfigurationService configService)
    {
        _logger = logger;
        _configService = configService;
    }

    public string Omsi2Path
    {
        get => _omsi2Path;
        set => SetProperty(ref _omsi2Path, value);
    }

    public bool AutoBackupBeforeLoad
    {
        get => _autoBackupBeforeLoad;
        set => SetProperty(ref _autoBackupBeforeLoad, value);
    }

    public bool ConfirmBeforeLoad
    {
        get => _confirmBeforeLoad;
        set => SetProperty(ref _confirmBeforeLoad, value);
    }

    public bool ShowNotifications
    {
        get => _showNotifications;
        set => SetProperty(ref _showNotifications, value);
    }

    public bool AutoLaunchOmsi
    {
        get => _autoLaunchOmsi;
        set => SetProperty(ref _autoLaunchOmsi, value);
    }

    public async Task LoadSettingsAsync()
    {
        var config = await _configService.LoadConfigAsync();
        Omsi2Path = config.Omsi2Path ?? string.Empty;
        AutoBackupBeforeLoad = config.AutoBackupBeforeLoad;
        ConfirmBeforeLoad = config.ConfirmBeforeLoad;
        ShowNotifications = config.ShowNotifications;
        AutoLaunchOmsi = config.AutoLaunchOmsi;
    }

    public async Task SaveSettingsAsync()
    {
        var config = new AppConfig
        {
            Omsi2Path = Omsi2Path,
            AutoBackupBeforeLoad = AutoBackupBeforeLoad,
            ConfirmBeforeLoad = ConfirmBeforeLoad,
            ShowNotifications = ShowNotifications,
            AutoLaunchOmsi = AutoLaunchOmsi,
            AppVersion = "1.0.0"
        };

        await _configService.SaveConfigAsync(config);
    }
}
