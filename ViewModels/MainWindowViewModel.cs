using Microsoft.Extensions.Logging;
using OMSIProfileManager.Models;
using OMSIProfileManager.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace OMSIProfileManager.ViewModels;

/// <summary>
/// ViewModel for the main window.
/// </summary>
public class MainWindowViewModel : ViewModelBase
{
    private readonly ILogger<MainWindowViewModel> _logger;
    private readonly IProfileManager _profileManager;
    private readonly IAddonScanner _addonScanner;
    private readonly IOMSILauncher _omsiLauncher;
    private readonly IConfigurationService _configService;
    private readonly IBackupManager _backupManager;

    private ObservableCollection<Profile> _profiles = new();
    private ObservableCollection<Profile> _allProfiles = new();
    private Profile? _selectedProfile;
    private bool _isLoading;
    private string _statusMessage = "Ready";
    private int _vehicleCount;
    private int _mapCount;
    private string _searchText = string.Empty;
    private string _sortOption = "Name";

    public MainWindowViewModel(
        ILogger<MainWindowViewModel> logger,
        IProfileManager profileManager,
        IAddonScanner addonScanner,
        IOMSILauncher omsiLauncher,
        IConfigurationService configService,
        IBackupManager backupManager)
    {
        _logger = logger;
        _profileManager = profileManager;
        _addonScanner = addonScanner;
        _omsiLauncher = omsiLauncher;
        _configService = configService;
        _backupManager = backupManager;
    }

    public ObservableCollection<Profile> Profiles
    {
        get => _profiles;
        set => SetProperty(ref _profiles, value);
    }

    public Profile? SelectedProfile
    {
        get => _selectedProfile;
        set => SetProperty(ref _selectedProfile, value);
    }

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public int VehicleCount
    {
        get => _vehicleCount;
        set => SetProperty(ref _vehicleCount, value);
    }

    public int MapCount
    {
        get => _mapCount;
        set => SetProperty(ref _mapCount, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                ApplyFiltersAndSort();
            }
        }
    }

    public string SortOption
    {
        get => _sortOption;
        set
        {
            if (SetProperty(ref _sortOption, value))
            {
                ApplyFiltersAndSort();
            }
        }
    }

    public async Task InitializeAsync()
    {
        IsLoading = true;
        StatusMessage = "Loading...";

        try
        {
            // Load configuration
            var config = await _configService.LoadConfigAsync();

            // Load profiles
            var profiles = await _profileManager.LoadProfilesAsync();
            _allProfiles = new ObservableCollection<Profile>(profiles);
            ApplyFiltersAndSort();

            // Scan addons if path is configured
            if (config.IsOmsi2PathValid() && config.Omsi2Path != null)
            {
                var (vehicles, maps) = await _addonScanner.ScanAllAddonsAsync(config.Omsi2Path);
                VehicleCount = vehicles.Count;
                MapCount = maps.Count;
                StatusMessage = $"Ready - {VehicleCount} vehicles, {MapCount} maps detected";
            }
            else
            {
                StatusMessage = "OMSI 2 path not configured";
            }
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize main window");
            StatusMessage = "Error loading data";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task LoadProfileAsync(Profile profile)
    {
        IsLoading = true;
        StatusMessage = $"Loading profile '{profile.Name}'...";

        try
        {
            var config = _configService.GetCurrentConfig();
            if (string.IsNullOrEmpty(config.Omsi2Path))
            {
                StatusMessage = "OMSI 2 path not configured";
                return;
            }

            // Create backup if enabled
            if (config.AutoBackupBeforeLoad)
            {
                await _backupManager.CreateBackupAsync(config.Omsi2Path, profile.Id, profile.Name);
            }

            // Load the profile
            var success = await _omsiLauncher.LoadProfileAsync(profile, config.Omsi2Path);

            if (success)
            {
                // Update last used timestamp
                var updatedProfile = profile with { LastUsed = System.DateTime.UtcNow };
                await _profileManager.UpdateProfileAsync(updatedProfile);

                // Launch OMSI 2 if configured
                if (config.AutoLaunchOmsi)
                {
                    await _omsiLauncher.LaunchOmsi2Async(config.Omsi2Path);
                }

                StatusMessage = $"Profile '{profile.Name}' loaded successfully";
            }
            else
            {
                StatusMessage = "Failed to load profile";
            }
        }
        catch (System.Exception ex)
        {
            _logger.LogError(ex, "Failed to load profile: {ProfileName}", profile.Name);
            StatusMessage = $"Error loading profile: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ApplyFiltersAndSort()
    {
        var filtered = _allProfiles.AsEnumerable();

        // Apply search filter
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var searchLower = SearchText.ToLowerInvariant();
            filtered = filtered.Where(p => 
                p.Name.ToLowerInvariant().Contains(searchLower) ||
                (!string.IsNullOrEmpty(p.Description) && p.Description.ToLowerInvariant().Contains(searchLower)));
        }

        // Apply sorting
        filtered = SortOption switch
        {
            "Name" => filtered.OrderBy(p => p.Name),
            "DateCreated" => filtered.OrderByDescending(p => p.CreatedAt),
            "LastUsed" => filtered.OrderByDescending(p => p.LastUsed ?? System.DateTime.MinValue),
            "VehicleCount" => filtered.OrderByDescending(p => p.Vehicles.Count),
            "MapCount" => filtered.OrderByDescending(p => p.Maps.Count),
            _ => filtered.OrderBy(p => p.Name)
        };

        Profiles = new ObservableCollection<Profile>(filtered);
    }
}
