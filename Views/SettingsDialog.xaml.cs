using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OMSIProfileManager.Models;
using OMSIProfileManager.Services;
using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace OMSIProfileManager.Views;

/// <summary>
/// Dialog for application settings.
/// </summary>
public sealed partial class SettingsDialog : ContentDialog
{
    private readonly ILogger<SettingsDialog> _logger;
    private readonly IConfigurationService _configService;
    private readonly IOMSIPathDetector _pathDetector;
    private readonly IBackupManager _backupManager;

    public bool SettingsSaved { get; private set; }

    public SettingsDialog(
        ILogger<SettingsDialog> logger,
        IConfigurationService configService,
        IOMSIPathDetector pathDetector,
        IBackupManager backupManager)
    {
        _logger = logger;
        _configService = configService;
        _pathDetector = pathDetector;
        _backupManager = backupManager;

        InitializeComponent();
        
        // Load settings when dialog is shown
        Loaded += async (s, e) => await LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        try
        {
            var config = _configService.GetCurrentConfig();
            
            Omsi2PathTextBox.Text = config.Omsi2Path ?? string.Empty;
            AutoBackupToggle.IsOn = config.AutoBackupBeforeLoad;
            ConfirmBeforeLoadToggle.IsOn = config.ConfirmBeforeLoad;
            ShowNotificationsToggle.IsOn = config.ShowNotifications;
            AutoLaunchToggle.IsOn = config.AutoLaunchOmsi;

            ValidatePath(config.Omsi2Path);
            await UpdateBackupInfoAsync(config.Omsi2Path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load settings");
        }
    }

    private void Omsi2PathTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var path = Omsi2PathTextBox.Text;
        ValidatePath(path);
    }

    private void ValidatePath(string? path)
    {
        PathValidationPanel.Visibility = Visibility.Visible;

        if (string.IsNullOrWhiteSpace(path))
        {
            PathValidationIcon.Glyph = "\uE7BA"; // Warning icon
            PathValidationIcon.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorAttentionBrush"];
            PathValidationText.Text = "OMSI 2 path not set";
            PathValidationText.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorAttentionBrush"];
            return;
        }

        var result = _pathDetector.ValidateOmsi2Path(path);
        
        if (result.IsValid)
        {
            PathValidationIcon.Glyph = "\uE73E"; // Checkmark icon
            PathValidationIcon.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
            PathValidationText.Text = "Valid OMSI 2 installation";
            PathValidationText.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorSuccessBrush"];
        }
        else
        {
            PathValidationIcon.Glyph = "\uE783"; // Error icon
            PathValidationIcon.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
            PathValidationText.Text = result.ErrorMessage ?? "Invalid OMSI 2 installation";
            PathValidationText.Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SystemFillColorCriticalBrush"];
        }
    }

    private async Task UpdateBackupInfoAsync(string? omsi2Path)
    {
        if (string.IsNullOrEmpty(omsi2Path))
        {
            BackupInfoText.Text = "No backups available (OMSI 2 path not configured)";
            return;
        }

        try
        {
            var backups = await _backupManager.ListBackupsAsync(omsi2Path);
            BackupInfoText.Text = $"{backups.Count} backup(s) available";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list backups");
            BackupInfoText.Text = "Error loading backup information";
        }
    }

    private async void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folderPicker = new FolderPicker();
            folderPicker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
            folderPicker.FileTypeFilter.Add("*");

            // Initialize the folder picker with the window handle
            var hwnd = WindowNative.GetWindowHandle((Application.Current as App)?.MainWindow);
            InitializeWithWindow.Initialize(folderPicker, hwnd);

            var folder = await folderPicker.PickSingleFolderAsync();
            if (folder != null)
            {
                Omsi2PathTextBox.Text = folder.Path;
                await UpdateBackupInfoAsync(folder.Path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to browse for folder");
            await ShowErrorAsync("Failed to open folder picker");
        }
    }

    private async void DetectButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            LoadingPanel.Visibility = Visibility.Visible;

            var result = await Task.Run(() => _pathDetector.DetectOmsi2Path());
            
            if (result.IsValid && result.Path != null)
            {
                Omsi2PathTextBox.Text = result.Path;
                await UpdateBackupInfoAsync(result.Path);

                var method = result.DetectionMethod switch
                {
                    "Registry" => "Found via Windows Registry",
                    "Steam" => "Found via Steam library",
                    "CommonPath" => "Found in common location",
                    _ => "Found"
                };

                await ShowInfoAsync($"OMSI 2 detected successfully!\n\n{method}\n{result.Path}");
            }
            else
            {
                await ShowErrorAsync($"Could not auto-detect OMSI 2 installation.\n\n{result.ErrorMessage}\n\nPlease use the Browse button to locate it manually.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to detect OMSI 2");
            await ShowErrorAsync("Failed to detect OMSI 2 installation");
        }
        finally
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
        }
    }

    private async void ManageBackupsButton_Click(object sender, RoutedEventArgs e)
    {
        var path = Omsi2PathTextBox.Text;
        if (string.IsNullOrEmpty(path))
        {
            await ShowErrorAsync("Please configure OMSI 2 path first");
            return;
        }

        try
        {
            var backups = await _backupManager.ListBackupsAsync(path);
            
            var content = new StackPanel { Spacing = 8 };
            
            if (backups.Count == 0)
            {
                content.Children.Add(new TextBlock 
                { 
                    Text = "No backups found",
                    TextWrapping = TextWrapping.Wrap
                });
            }
            else
            {
                foreach (var backup in backups)
                {
                    var panel = new StackPanel { Spacing = 4 };
                    panel.Children.Add(new TextBlock 
                    { 
                        Text = backup.ProfileName ?? backup.ProfileId,
                        FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
                    });
                    panel.Children.Add(new TextBlock 
                    { 
                        Text = $"Created: {backup.CreatedAt:g}",
                        FontSize = 12,
                        Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                    });
                    panel.Children.Add(new TextBlock 
                    { 
                        Text = $"{backup.VehicleCount} vehicles, {backup.MapCount} maps",
                        FontSize = 12,
                        Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorSecondaryBrush"]
                    });
                    
                    content.Children.Add(new Border
                    {
                        Child = panel,
                        Padding = new Thickness(12),
                        Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
                        BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                        BorderThickness = new Thickness(1),
                        CornerRadius = new Microsoft.UI.Xaml.CornerRadius(4),
                        Margin = new Thickness(0, 0, 0, 8)
                    });
                }
            }

            var scrollViewer = new ScrollViewer
            {
                Content = content,
                MaxHeight = 400
            };

            var dialog = new ContentDialog
            {
                Title = $"Backups ({backups.Count})",
                Content = scrollViewer,
                CloseButtonText = "Close",
                XamlRoot = XamlRoot
            };

            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list backups");
            await ShowErrorAsync("Failed to load backups");
        }
    }

    private async void SaveButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();

        try
        {
            var path = Omsi2PathTextBox.Text.Trim();
            
            // Validate path if provided
            if (!string.IsNullOrEmpty(path))
            {
                var result = _pathDetector.ValidateOmsi2Path(path);
                if (!result.IsValid)
                {
                    args.Cancel = true;
                    await ShowErrorAsync($"Invalid OMSI 2 path:\n\n{result.ErrorMessage}");
                    deferral.Complete();
                    return;
                }
            }

            var config = new AppConfig
            {
                Omsi2Path = string.IsNullOrEmpty(path) ? null : path,
                AutoBackupBeforeLoad = AutoBackupToggle.IsOn,
                ConfirmBeforeLoad = ConfirmBeforeLoadToggle.IsOn,
                ShowNotifications = ShowNotificationsToggle.IsOn,
                AutoLaunchOmsi = AutoLaunchToggle.IsOn,
                AppVersion = "1.0.0"
            };

            await _configService.SaveConfigAsync(config);
            SettingsSaved = true;

            _logger.LogInformation("Settings saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            args.Cancel = true;
            await ShowErrorAsync($"Failed to save settings:\n\n{ex.Message}");
        }
        finally
        {
            deferral.Complete();
        }
    }

    private async Task ShowErrorAsync(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Error",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async Task ShowInfoAsync(string message)
    {
        var dialog = new ContentDialog
        {
            Title = "Information",
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = XamlRoot
        };
        await dialog.ShowAsync();
    }
}
