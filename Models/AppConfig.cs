using System;

namespace OMSIProfileManager.Models;

/// <summary>
/// Application configuration and user settings.
/// Stored in %APPDATA%\OMSIProfileManager\config.json
/// </summary>
public sealed record AppConfig
{
    /// <summary>
    /// Path to the OMSI 2 installation directory.
    /// </summary>
    public string? Omsi2Path { get; init; }

    /// <summary>
    /// Whether to auto-backup before loading a profile.
    /// </summary>
    public bool AutoBackupBeforeLoad { get; init; } = true;

    /// <summary>
    /// Whether to show confirmation dialog before loading a profile.
    /// </summary>
    public bool ConfirmBeforeLoad { get; init; } = false;

    /// <summary>
    /// Whether to show toast notifications.
    /// </summary>
    public bool ShowNotifications { get; init; } = true;

    /// <summary>
    /// Whether to automatically launch OMSI 2 after loading a profile.
    /// </summary>
    public bool AutoLaunchOmsi { get; init; } = true;

    /// <summary>
    /// Maximum number of backups to keep.
    /// </summary>
    public int MaxBackupCount { get; init; } = 10;

    /// <summary>
    /// When the configuration was last saved.
    /// </summary>
    public DateTime? LastUpdated { get; init; }

    /// <summary>
    /// Application version that last saved this config.
    /// </summary>
    public string? AppVersion { get; init; }

    /// <summary>
    /// Validates that the OMSI 2 path exists and contains required subdirectories.
    /// </summary>
    public bool IsOmsi2PathValid()
    {
        if (string.IsNullOrWhiteSpace(Omsi2Path))
            return false;

        if (!System.IO.Directory.Exists(Omsi2Path))
            return false;

        var vehiclesPath = System.IO.Path.Combine(Omsi2Path, "Vehicles");
        var mapsPath = System.IO.Path.Combine(Omsi2Path, "Maps");

        return System.IO.Directory.Exists(vehiclesPath) && System.IO.Directory.Exists(mapsPath);
    }
}
