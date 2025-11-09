using System.Collections.Generic;
using System.Threading.Tasks;

namespace OMSIProfileManager.Services;

/// <summary>
/// Service for detecting and validating OMSI 2 installation paths.
/// </summary>
/// <remarks>
/// This service provides comprehensive OMSI 2 installation detection using multiple strategies:
/// - Windows Registry lookups (both Steam and manual installations)
/// - Common Steam installation paths
/// - Custom Steam library folders (read from libraryfolders.vdf)
/// - Manual path validation
/// 
/// The service is designed to handle various installation scenarios and provide detailed
/// feedback about why a path might be invalid.
/// </remarks>
public interface IOMSIPathDetector
{
    /// <summary>
    /// Attempts to automatically detect the OMSI 2 installation path.
    /// Checks registry, common Steam paths, and custom Steam libraries.
    /// </summary>
    /// <returns>The detected path as a string, or null if not found.</returns>
    /// <remarks>
    /// This method performs detection in the following order:
    /// 1. Windows Registry search (fastest if available)
    /// 2. Common Steam paths (C:\Program Files (x86)\Steam\steamapps\common\OMSI 2)
    /// 3. Custom Steam library folders parsed from libraryfolders.vdf
    /// 
    /// Each potential path is validated using <see cref="ValidateOMSI2Path"/> before being returned.
    /// The method returns the first valid path found, or null if no valid installation is detected.
    /// 
    /// This is typically the first method called when the application needs to find OMSI 2,
    /// such as during first-run or when auto-detecting from the Settings dialog.
    /// </remarks>
    Task<string?> DetectOMSI2PathAsync();

    /// <summary>
    /// Validates that a path is a valid OMSI 2 installation.
    /// Checks for required subdirectories (Vehicles/, Maps/, Sceneryobjects/).
    /// </summary>
    /// <param name="path">The path to validate. Must not be null or empty.</param>
    /// <returns>True if valid OMSI 2 installation with all required directories; false otherwise.</returns>
    /// <exception cref="ArgumentException">Thrown when path is null or empty.</exception>
    /// <remarks>
    /// This method performs a lightweight validation by checking for the existence of three
    /// critical OMSI 2 subdirectories:
    /// - Vehicles/ (contains vehicle addons)
    /// - Maps/ (contains map addons)
    /// - Sceneryobjects/ (contains scenery objects)
    /// 
    /// All three directories must exist for the path to be considered valid.
    /// The method does not verify the contents of these directories or the presence of
    /// OMSI 2 executables, as these can vary between installations.
    /// 
    /// Use <see cref="ValidateWithDetails"/> if you need detailed information about what's
    /// missing from an invalid path.
    /// </remarks>
    bool ValidateOMSI2Path(string path);

    /// <summary>
    /// Gets a list of all potential OMSI 2 installation paths to check.
    /// Includes common Steam locations and custom library paths.
    /// </summary>
    /// <returns>A list of potential paths as strings. List may be empty if no paths are found.</returns>
    /// <remarks>
    /// This method generates a comprehensive list of paths where OMSI 2 might be installed:
    /// - Default Steam path: C:\Program Files (x86)\Steam\steamapps\common\OMSI 2
    /// - Custom Steam library paths read from libraryfolders.vdf
    /// - Alternative Program Files locations
    /// 
    /// The method does not validate these paths; it only generates potential candidates.
    /// Callers should use <see cref="ValidateOMSI2Path"/> to check each path.
    /// 
    /// This is useful for implementing custom search UI or debugging installation detection issues.
    /// </remarks>
    Task<List<string>> GetPotentialPathsAsync();

    /// <summary>
    /// Searches for OMSI 2 installation via Windows Registry.
    /// Checks both Steam registry keys and potential manual installations.
    /// </summary>
    /// <returns>Path from registry if found and valid, null otherwise.</returns>
    /// <remarks>
    /// This method queries the Windows Registry for OMSI 2 installation information.
    /// It checks:
    /// - Steam app registry keys (HKLM and HKCU)
    /// - Uninstall registry entries
    /// - Steam installation paths
    /// 
    /// Registry-based detection is the most reliable method when available, as it represents
    /// the officially registered installation path.
    /// 
    /// The method returns null if registry access fails (e.g., on non-Windows platforms or
    /// when registry permissions are restricted) or if no valid path is found in the registry.
    /// 
    /// Note: This method only works on Windows systems with proper registry access.
    /// </remarks>
    Task<string?> SearchRegistryAsync();

    /// <summary>
    /// Gets detailed validation information about a path.
    /// </summary>
    /// <param name="path">The path to validate. Must not be null or empty.</param>
    /// <returns>A <see cref="ValidationResult"/> object with detailed information about the validation.</returns>
    /// <exception cref="ArgumentException">Thrown when path is null or empty.</exception>
    /// <remarks>
    /// This method provides comprehensive validation feedback, including:
    /// - Whether the path is valid
    /// - List of missing required components
    /// - List of found required components
    /// - Detailed error messages explaining validation failures
    /// 
    /// Use this method when you need to provide detailed feedback to the user about why
    /// a path is invalid, such as in the Settings dialog or during troubleshooting.
    /// 
    /// For simple yes/no validation, use <see cref="ValidateOMSI2Path"/> instead for better performance.
    /// </remarks>
    ValidationResult ValidateWithDetails(string path);
}

/// <summary>
/// Result of path validation with detailed information.
/// </summary>
/// <remarks>
/// This record provides comprehensive feedback about OMSI 2 path validation.
/// It includes both positive (what was found) and negative (what's missing) information
/// to help diagnose installation issues.
/// </remarks>
public sealed record ValidationResult
{
    /// <summary>
    /// Gets whether the path is a valid OMSI 2 installation.
    /// </summary>
    public required bool IsValid { get; init; }
    
    /// <summary>
    /// Gets the path that was validated.
    /// </summary>
    public required string Path { get; init; }
    
    /// <summary>
    /// Gets the list of missing required components (e.g., "Vehicles", "Maps", "Sceneryobjects").
    /// </summary>
    public List<string> MissingComponents { get; init; } = new();
    
    /// <summary>
    /// Gets the list of required components that were found.
    /// </summary>
    public List<string> FoundComponents { get; init; } = new();
    
    /// <summary>
    /// Gets a human-readable error message explaining validation failure, or null if validation succeeded.
    /// </summary>
    public string? ErrorMessage { get; init; }
}
