using OMSIProfileManager.Models;
using System.Threading.Tasks;

namespace OMSIProfileManager.Services;

/// <summary>
/// Service for loading OMSI 2 profiles and launching the game.
/// Manages addon states by renaming folders with .disabled suffix.
/// Includes retry logic for file operations and process management.
/// </summary>
public interface IOMSILauncher
{
    /// <summary>
    /// Loads a profile by configuring OMSI 2 addons according to profile settings.
    /// First disables all addons in both Vehicles and Maps directories, 
    /// then enables only those specified in the profile.
    /// </summary>
    /// <param name="profile">The profile to load. Must be valid with existing addons.</param>
    /// <param name="omsi2Path">The root path to OMSI 2 installation directory.</param>
    /// <returns>True if profile loaded successfully, false if any errors occurred.</returns>
    /// <remarks>
    /// This method:
    /// - Validates profile and paths
    /// - Checks if OMSI 2 is running (warns but continues)
    /// - Disables all vehicles and maps
    /// - Enables only profile-specific vehicles and maps
    /// - Uses retry logic (3 attempts) for locked files
    /// </remarks>
    Task<bool> LoadProfileAsync(Profile profile, string omsi2Path);

    /// <summary>
    /// Disables all addons in the specified directory by appending .disabled suffix.
    /// Only affects folders that don't already have .disabled suffix.
    /// </summary>
    /// <param name="addonDirectory">Full path to Vehicles or Maps directory.</param>
    /// <returns>Completes when all addons are disabled or max retries exhausted.</returns>
    /// <exception cref="System.ArgumentException">Thrown when addonDirectory is empty.</exception>
    /// <exception cref="System.UnauthorizedAccessException">Thrown when access denied.</exception>
    /// <remarks>
    /// Example: "Vehicles\MAN_NG363" becomes "Vehicles\MAN_NG363.disabled"
    /// </remarks>
    Task DisableAllAddonsAsync(string addonDirectory);

    /// <summary>
    /// Enables specific addons by removing the .disabled suffix from their folders.
    /// Only affects folders that currently have .disabled suffix.
    /// </summary>
    /// <param name="addonDirectory">Full path to Vehicles or Maps directory.</param>
    /// <param name="addonNames">Array of addon folder names to enable (without .disabled suffix).</param>
    /// <returns>Completes when all specified addons are enabled or max retries exhausted.</returns>
    /// <exception cref="System.ArgumentException">Thrown when addonDirectory is empty.</exception>
    /// <exception cref="System.ArgumentNullException">Thrown when addonNames is null.</exception>
    /// <remarks>
    /// Example: "Vehicles\MAN_NG363.disabled" becomes "Vehicles\MAN_NG363"
    /// Skips addons that are already enabled or don't exist.
    /// </remarks>
    Task EnableAddonsAsync(string addonDirectory, string[] addonNames);

    /// <summary>
    /// Launches the OMSI 2 executable (Omsi.exe) from the specified installation path.
    /// Sets working directory correctly and checks for existing instances.
    /// </summary>
    /// <param name="omsi2Path">The root path to OMSI 2 installation directory.</param>
    /// <returns>True if launched successfully, false if already running or error occurred.</returns>
    /// <exception cref="System.ComponentModel.Win32Exception">Thrown when Windows cannot start process.</exception>
    /// <remarks>
    /// This method:
    /// - Validates Omsi.exe exists
    /// - Checks if OMSI 2 is already running
    /// - Sets correct working directory
    /// - Uses UseShellExecute for proper launch
    /// </remarks>
    Task<bool> LaunchOmsi2Async(string omsi2Path);

    /// <summary>
    /// Checks if any OMSI 2 process (Omsi.exe) is currently running on the system.
    /// </summary>
    /// <returns>True if at least one OMSI 2 process is found, false otherwise.</returns>
    /// <remarks>
    /// Looks for process named "Omsi" (without .exe extension).
    /// Properly disposes all process objects after checking.
    /// </remarks>
    bool IsOmsi2Running();

    /// <summary>
    /// Restores all disabled addons to enabled state in the OMSI 2 installation.
    /// Processes both Vehicles and Maps directories in parallel.
    /// Useful for reverting to "all addons enabled" state.
    /// </summary>
    /// <param name="omsi2Path">The root path to OMSI 2 installation directory.</param>
    /// <returns>Completes when all addons are restored in both directories.</returns>
    /// <exception cref="System.ArgumentException">Thrown when omsi2Path is empty.</exception>
    /// <exception cref="System.IO.DirectoryNotFoundException">Thrown when OMSI 2 path doesn't exist.</exception>
    /// <remarks>
    /// This method finds all *.disabled folders and removes the suffix.
    /// Includes retry logic for locked files.
    /// </remarks>
    Task RestoreAllAddonsAsync(string omsi2Path);
}
