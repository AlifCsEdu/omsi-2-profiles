using OMSIProfileManager.Models;
using System.Threading.Tasks;

namespace OMSIProfileManager.Services;

/// <summary>
/// Service for managing application configuration and settings.
/// </summary>
/// <remarks>
/// This service handles loading, saving, and caching of application configuration.
/// Configuration is persisted to a JSON file in the AppData directory and includes
/// settings such as OMSI 2 path, auto-launch preferences, backup settings, and UI preferences.
/// 
/// The service maintains an in-memory cache of the configuration for performance,
/// updating it when changes are saved.
/// </remarks>
public interface IConfigurationService
{
    /// <summary>
    /// Loads the application configuration from disk.
    /// </summary>
    /// <returns>The loaded <see cref="AppConfig"/> object with all application settings.</returns>
    /// <exception cref="IOException">Thrown when there's an error reading the configuration file.</exception>
    /// <exception cref="System.Text.Json.JsonException">Thrown when the configuration file contains invalid JSON.</exception>
    /// <remarks>
    /// This method reads the configuration from the appsettings.json file in the AppData directory.
    /// If the file doesn't exist, it returns a new default configuration with reasonable defaults.
    /// 
    /// The method also attempts to auto-detect the OMSI 2 path if it's not set in the configuration.
    /// After loading, the configuration is cached in memory for quick access via <see cref="GetCurrentConfig"/>.
    /// 
    /// Typical usage is during application startup to initialize settings.
    /// </remarks>
    Task<AppConfig> LoadConfigAsync();

    /// <summary>
    /// Saves the application configuration to disk.
    /// </summary>
    /// <param name="config">The configuration object to save. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown when config is null.</exception>
    /// <exception cref="IOException">Thrown when there's an error writing the configuration file.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to write the file is denied.</exception>
    /// <remarks>
    /// This method serializes the configuration to JSON and saves it to the AppData directory.
    /// The configuration file is created if it doesn't exist, and existing files are overwritten.
    /// 
    /// After saving, the in-memory cache is updated to reflect the new configuration.
    /// 
    /// Typical usage is when the user modifies settings in the Settings dialog or when
    /// the application auto-detects the OMSI 2 path.
    /// </remarks>
    Task SaveConfigAsync(AppConfig config);

    /// <summary>
    /// Gets the current configuration (cached in memory).
    /// </summary>
    /// <returns>The cached <see cref="AppConfig"/> object, or a new default configuration if not yet loaded.</returns>
    /// <remarks>
    /// This method returns the in-memory cached configuration without any disk I/O.
    /// It's safe to call frequently throughout the application lifecycle.
    /// 
    /// The cache is populated when <see cref="LoadConfigAsync"/> is called and updated
    /// when <see cref="SaveConfigAsync"/> is called.
    /// 
    /// If called before the configuration has been loaded, it returns a new default
    /// configuration object.
    /// </remarks>
    AppConfig GetCurrentConfig();

    /// <summary>
    /// Attempts to auto-detect the OMSI 2 installation path.
    /// </summary>
    /// <returns>The detected path as a string, or null if detection failed.</returns>
    /// <remarks>
    /// This method delegates to <see cref="IOMSIPathDetector.DetectOMSI2PathAsync"/> to find
    /// the OMSI 2 installation. It checks:
    /// 1. Windows Registry (Steam and manual installations)
    /// 2. Common Steam installation paths
    /// 3. Custom Steam library folders
    /// 
    /// The method does not modify the current configuration; it only returns the detected path.
    /// Callers should use <see cref="SaveConfigAsync"/> if they want to persist the detected path.
    /// 
    /// This is typically called during first-run or when the user clicks "Auto-Detect" in settings.
    /// </remarks>
    Task<string?> DetectOmsi2PathAsync();

    /// <summary>
    /// Validates that the specified path is a valid OMSI 2 installation.
    /// </summary>
    /// <param name="path">The path to validate. Must not be null or empty.</param>
    /// <returns>True if the path is a valid OMSI 2 installation; false otherwise.</returns>
    /// <exception cref="ArgumentException">Thrown when path is null or empty.</exception>
    /// <remarks>
    /// This method checks for the presence of required OMSI 2 subdirectories:
    /// - Vehicles/
    /// - Maps/
    /// - Sceneryobjects/
    /// 
    /// All three directories must exist for the path to be considered valid.
    /// 
    /// This validation is used in the Settings dialog to provide immediate feedback
    /// when the user manually enters or browses for an OMSI 2 path.
    /// </remarks>
    bool ValidateOmsi2Path(string path);
}
