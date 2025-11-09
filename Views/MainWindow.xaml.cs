using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OMSIProfileManager.Models;
using OMSIProfileManager.ViewModels;
using System;

namespace OMSIProfileManager.Views;

/// <summary>
/// Main window for the application.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindowViewModel ViewModel { get; }

    public MainWindow(MainWindowViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        
        // Initialize the view model
        _ = ViewModel.InitializeAsync();
        
        // Set up keyboard shortcuts
        SetupKeyboardShortcuts();
    }

    private void SetupKeyboardShortcuts()
    {
        // Ctrl+N - New Profile
        var newProfileAccelerator = new Microsoft.UI.Xaml.Input.KeyboardAccelerator
        {
            Key = Windows.System.VirtualKey.N,
            Modifiers = Windows.System.VirtualKeyModifiers.Control
        };
        newProfileAccelerator.Invoked += (sender, args) =>
        {
            NewProfileButton_Click(this, new RoutedEventArgs());
            args.Handled = true;
        };
        KeyboardAccelerators.Add(newProfileAccelerator);

        // Ctrl+R - Refresh
        var refreshAccelerator = new Microsoft.UI.Xaml.Input.KeyboardAccelerator
        {
            Key = Windows.System.VirtualKey.R,
            Modifiers = Windows.System.VirtualKeyModifiers.Control
        };
        refreshAccelerator.Invoked += (sender, args) =>
        {
            RefreshButton_Click(this, new RoutedEventArgs());
            args.Handled = true;
        };
        KeyboardAccelerators.Add(refreshAccelerator);

        // Ctrl+F - Focus Search
        var searchAccelerator = new Microsoft.UI.Xaml.Input.KeyboardAccelerator
        {
            Key = Windows.System.VirtualKey.F,
            Modifiers = Windows.System.VirtualKeyModifiers.Control
        };
        searchAccelerator.Invoked += (sender, args) =>
        {
            // Focus the search box
            var searchBox = FindSearchBox(Content);
            searchBox?.Focus(FocusState.Programmatic);
            args.Handled = true;
        };
        KeyboardAccelerators.Add(searchAccelerator);

        // Ctrl+, - Settings
        var settingsAccelerator = new Microsoft.UI.Xaml.Input.KeyboardAccelerator
        {
            Key = Windows.System.VirtualKey.OemComma,
            Modifiers = Windows.System.VirtualKeyModifiers.Control
        };
        settingsAccelerator.Invoked += (sender, args) =>
        {
            SettingsButton_Click(this, new RoutedEventArgs());
            args.Handled = true;
        };
        KeyboardAccelerators.Add(settingsAccelerator);
    }

    private TextBox? FindSearchBox(UIElement element)
    {
        if (element is TextBox textBox && textBox.PlaceholderText?.Contains("Search") == true)
        {
            return textBox;
        }

        var childrenCount = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(element);
        for (int i = 0; i < childrenCount; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(element, i);
            if (child is UIElement uiChild)
            {
                var result = FindSearchBox(uiChild);
                if (result != null) return result;
            }
        }

        return null;
    }

    private void HomeButton_Click(object sender, RoutedEventArgs e)
    {
        // Already on home screen
    }

    private async void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = App.Current.Services.GetRequiredService<SettingsDialog>();
        dialog.XamlRoot = Content.XamlRoot;
        
        var result = await dialog.ShowAsync();
        
        // Refresh the UI if settings were saved
        if (dialog.SettingsSaved)
        {
            await ViewModel.InitializeAsync();
        }
    }

    private async void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "About",
            Content = "OMSI 2 Profile Manager v1.0.0\n\nA modern profile management tool for OMSI 2.",
            CloseButtonText = "Close",
            XamlRoot = Content.XamlRoot
        };
        await dialog.ShowAsync();
    }

    private async void NewProfileButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = App.Current.Services.GetRequiredService<NewProfileDialog>();
        dialog.XamlRoot = Content.XamlRoot;
        
        var result = await dialog.ShowAsync();
        
        // Refresh the profiles list if a new profile was created
        if (dialog.CreatedProfile != null)
        {
            await ViewModel.InitializeAsync();
        }
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
    }

    private async void LoadProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Profile profile)
        {
            // Check if confirmation is required
            var configService = App.Current.Services.GetRequiredService<Services.IConfigurationService>();
            var config = configService.GetCurrentConfig();

            if (config.ShowConfirmationBeforeLoad)
            {
                var confirmDialog = new ContentDialog
                {
                    Title = "Load Profile",
                    Content = $"Load profile '{profile.Name}'?\n\n" +
                             $"This will disable all current addons and enable:\n" +
                             $"• {profile.Vehicles.Count} vehicle(s)\n" +
                             $"• {profile.Maps.Count} map(s)\n\n" +
                             (config.AutoBackupBeforeLoad ? "A backup will be created automatically.\n" : "") +
                             (config.AutoLaunchOmsi ? "OMSI 2 will launch automatically." : ""),
                    PrimaryButtonText = "Load",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Primary,
                    XamlRoot = Content.XamlRoot
                };

                var result = await confirmDialog.ShowAsync();
                if (result != ContentDialogResult.Primary)
                {
                    return; // User cancelled
                }
            }

            await ViewModel.LoadProfileAsync(profile);
        }
    }

    private async void EditProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Profile profile)
        {
            // Create EditProfileDialog with factory pattern since it needs parameters
            var logger = App.Current.Services.GetRequiredService<ILogger<EditProfileDialog>>();
            var profileManager = App.Current.Services.GetRequiredService<Services.IProfileManager>();
            var addonScanner = App.Current.Services.GetRequiredService<Services.IAddonScanner>();
            var configService = App.Current.Services.GetRequiredService<Services.IConfigurationService>();
            
            var dialog = new EditProfileDialog(logger, profileManager, addonScanner, configService, profile)
            {
                XamlRoot = Content.XamlRoot
            };
            
            var result = await dialog.ShowAsync();
            
            // Refresh the profiles list if the profile was updated
            if (dialog.UpdatedProfile != null)
            {
                await ViewModel.InitializeAsync();
            }
        }
    }

    private async void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Profile profile)
        {
            // Show confirmation dialog
            var confirmDialog = new ContentDialog
            {
                Title = "Delete Profile",
                Content = $"Are you sure you want to delete the profile '{profile.Name}'?\n\nThis action cannot be undone.",
                PrimaryButtonText = "Delete",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = Content.XamlRoot
            };

            var result = await confirmDialog.ShowAsync();
            
            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    var profileManager = App.Current.Services.GetRequiredService<Services.IProfileManager>();
                    await profileManager.DeleteProfileAsync(profile.Id);
                    await ViewModel.InitializeAsync();
                }
                catch (Exception ex)
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = "Error",
                        Content = $"Failed to delete profile: {ex.Message}",
                        CloseButtonText = "OK",
                        XamlRoot = Content.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                }
            }
        }
    }

    private void SortComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox comboBox && comboBox.SelectedItem is ComboBoxItem item && item.Tag is string sortOption)
        {
            ViewModel.SortOption = sortOption;
        }
    }

    private async void DuplicateProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is Profile profile)
        {
            // Prompt for new profile name
            var inputTextBox = new TextBox
            {
                PlaceholderText = "Enter new profile name",
                Text = profile.Name + " (Copy)"
            };

            var dialog = new ContentDialog
            {
                Title = "Duplicate Profile",
                Content = inputTextBox,
                PrimaryButtonText = "Duplicate",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = Content.XamlRoot
            };

            var result = await dialog.ShowAsync();

            if (result == ContentDialogResult.Primary && !string.IsNullOrWhiteSpace(inputTextBox.Text))
            {
                try
                {
                    var profileManager = App.Current.Services.GetRequiredService<Services.IProfileManager>();
                    var newProfile = await profileManager.DuplicateProfileAsync(profile.Id, inputTextBox.Text);
                    
                    if (newProfile != null)
                    {
                        await ViewModel.InitializeAsync();
                        
                        var successDialog = new ContentDialog
                        {
                            Title = "Success",
                            Content = $"Profile '{newProfile.Name}' created successfully!",
                            CloseButtonText = "OK",
                            XamlRoot = Content.XamlRoot
                        };
                        await successDialog.ShowAsync();
                    }
                }
                catch (Exception ex)
                {
                    var errorDialog = new ContentDialog
                    {
                        Title = "Error",
                        Content = $"Failed to duplicate profile: {ex.Message}",
                        CloseButtonText = "OK",
                        XamlRoot = Content.XamlRoot
                    };
                    await errorDialog.ShowAsync();
                }
            }
        }
    }
}
