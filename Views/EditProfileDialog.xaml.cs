using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OMSIProfileManager.Models;
using OMSIProfileManager.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace OMSIProfileManager.Views;

/// <summary>
/// Dialog for editing an existing profile.
/// </summary>
public sealed partial class EditProfileDialog : ContentDialog
{
    private readonly ILogger<EditProfileDialog> _logger;
    private readonly IProfileManager _profileManager;
    private readonly IAddonScanner _addonScanner;
    private readonly IConfigurationService _configService;
    
    private readonly Profile _originalProfile;
    private List<AddonInfo> _availableVehicles = new();
    private List<AddonInfo> _availableMaps = new();
    private readonly Dictionary<string, CheckBox> _vehicleCheckboxes = new();
    private readonly Dictionary<string, CheckBox> _mapCheckboxes = new();

    public Profile? UpdatedProfile { get; private set; }

    public EditProfileDialog(
        ILogger<EditProfileDialog> logger,
        IProfileManager profileManager,
        IAddonScanner addonScanner,
        IConfigurationService configService,
        Profile profile)
    {
        _logger = logger;
        _profileManager = profileManager;
        _addonScanner = addonScanner;
        _configService = configService;
        _originalProfile = profile;
        
        InitializeComponent();
        
        // Set initial values
        ProfileNameTextBox.Text = profile.Name;
        ProfileDescriptionTextBox.Text = profile.Description ?? string.Empty;
        
        // Load addons asynchronously
        Loaded += async (s, e) => await LoadAddonsAsync();
    }

    private async Task LoadAddonsAsync()
    {
        try
        {
            LoadingPanel.Visibility = Visibility.Visible;
            
            var config = _configService.GetCurrentConfig();
            if (string.IsNullOrEmpty(config.Omsi2Path))
            {
                _logger.LogWarning("OMSI 2 path not configured");
                return;
            }

            var (vehicles, maps) = await _addonScanner.ScanAllAddonsAsync(config.Omsi2Path);
            _availableVehicles = vehicles;
            _availableMaps = maps;

            PopulateVehiclesList();
            PopulateMapsLi();
            UpdateCounts();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load addons");
        }
        finally
        {
            LoadingPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void PopulateVehiclesList()
    {
        VehiclesPanel.Children.Clear();
        _vehicleCheckboxes.Clear();

        foreach (var vehicle in _availableVehicles.OrderBy(v => v.Name))
        {
            var checkbox = new CheckBox
            {
                Content = vehicle.Name,
                Tag = vehicle.Path,
                Margin = new Thickness(0, 4, 0, 4),
                IsChecked = _originalProfile.Vehicles.Contains(vehicle.Path)
            };
            checkbox.Checked += (s, e) => UpdateCounts();
            checkbox.Unchecked += (s, e) => UpdateCounts();

            VehiclesPanel.Children.Add(checkbox);
            _vehicleCheckboxes[vehicle.Path] = checkbox;
        }
    }

    private void PopulateMapsLi()
    {
        MapsPanel.Children.Clear();
        _mapCheckboxes.Clear();

        foreach (var map in _availableMaps.OrderBy(m => m.Name))
        {
            var checkbox = new CheckBox
            {
                Content = map.Name,
                Tag = map.Path,
                Margin = new Thickness(0, 4, 0, 4),
                IsChecked = _originalProfile.Maps.Contains(map.Path)
            };
            checkbox.Checked += (s, e) => UpdateCounts();
            checkbox.Unchecked += (s, e) => UpdateCounts();

            MapsPanel.Children.Add(checkbox);
            _mapCheckboxes[map.Path] = checkbox;
        }
    }

    private void UpdateCounts()
    {
        var vehicleCount = _vehicleCheckboxes.Values.Count(cb => cb.IsChecked == true);
        var mapCount = _mapCheckboxes.Values.Count(cb => cb.IsChecked == true);

        VehicleCountText.Text = $"{vehicleCount} selected";
        MapCountText.Text = $"{mapCount} selected";
    }

    private void ProfileNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ValidateProfileName();
    }

    private bool ValidateProfileName()
    {
        var name = ProfileNameTextBox.Text.Trim();
        
        if (string.IsNullOrWhiteSpace(name))
        {
            NameErrorText.Text = "Profile name is required";
            NameErrorText.Visibility = Visibility.Visible;
            IsPrimaryButtonEnabled = false;
            return false;
        }

        if (name.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
        {
            NameErrorText.Text = "Profile name contains invalid characters";
            NameErrorText.Visibility = Visibility.Visible;
            IsPrimaryButtonEnabled = false;
            return false;
        }

        NameErrorText.Visibility = Visibility.Collapsed;
        IsPrimaryButtonEnabled = true;
        return true;
    }

    private async void SaveButton_Click(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var deferral = args.GetDeferral();

        try
        {
            if (!ValidateProfileName())
            {
                args.Cancel = true;
                deferral.Complete();
                return;
            }

            var name = ProfileNameTextBox.Text.Trim();
            var description = ProfileDescriptionTextBox.Text.Trim();
            
            var selectedVehicles = _vehicleCheckboxes
                .Where(kvp => kvp.Value.IsChecked == true)
                .Select(kvp => kvp.Key)
                .ToList();

            var selectedMaps = _mapCheckboxes
                .Where(kvp => kvp.Value.IsChecked == true)
                .Select(kvp => kvp.Key)
                .ToList();

            var updatedProfile = _originalProfile with
            {
                Name = name,
                Description = description,
                Vehicles = selectedVehicles,
                Maps = selectedMaps,
                LastModified = DateTime.UtcNow
            };

            await _profileManager.UpdateProfileAsync(updatedProfile);
            UpdatedProfile = updatedProfile;

            _logger.LogInformation("Updated profile: {ProfileName} with {VehicleCount} vehicles and {MapCount} maps",
                name, selectedVehicles.Count, selectedMaps.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update profile");
            
            args.Cancel = true;
            var errorDialog = new ContentDialog
            {
                Title = "Error",
                Content = $"Failed to update profile: {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = XamlRoot
            };
            await errorDialog.ShowAsync();
        }
        finally
        {
            deferral.Complete();
        }
    }

    private void SelectAllVehicles_Click(object sender, RoutedEventArgs e)
    {
        foreach (var checkbox in _vehicleCheckboxes.Values)
        {
            checkbox.IsChecked = true;
        }
    }

    private void ClearVehicles_Click(object sender, RoutedEventArgs e)
    {
        foreach (var checkbox in _vehicleCheckboxes.Values)
        {
            checkbox.IsChecked = false;
        }
    }

    private void SelectAllMaps_Click(object sender, RoutedEventArgs e)
    {
        foreach (var checkbox in _mapCheckboxes.Values)
        {
            checkbox.IsChecked = true;
        }
    }

    private void ClearMaps_Click(object sender, RoutedEventArgs e)
    {
        foreach (var checkbox in _mapCheckboxes.Values)
        {
            checkbox.IsChecked = false;
        }
    }
}
