using Microsoft.Extensions.Logging;
using OMSIProfileManager.Models;
using OMSIProfileManager.Services;
using System.Collections.Generic;

namespace OMSIProfileManager.ViewModels;

/// <summary>
/// ViewModel for creating/editing profiles.
/// </summary>
public class NewProfileViewModel : ViewModelBase
{
    private readonly ILogger<NewProfileViewModel> _logger;
    private readonly IProfileManager _profileManager;

    private string _profileName = string.Empty;
    private string _profileDescription = string.Empty;
    private List<string> _selectedVehicles = new();
    private List<string> _selectedMaps = new();

    public NewProfileViewModel(
        ILogger<NewProfileViewModel> logger,
        IProfileManager profileManager)
    {
        _logger = logger;
        _profileManager = profileManager;
    }

    public string ProfileName
    {
        get => _profileName;
        set => SetProperty(ref _profileName, value);
    }

    public string ProfileDescription
    {
        get => _profileDescription;
        set => SetProperty(ref _profileDescription, value);
    }

    public List<string> SelectedVehicles
    {
        get => _selectedVehicles;
        set => SetProperty(ref _selectedVehicles, value);
    }

    public List<string> SelectedMaps
    {
        get => _selectedMaps;
        set => SetProperty(ref _selectedMaps, value);
    }
}
