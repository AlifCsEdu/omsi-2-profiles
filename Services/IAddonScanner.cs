using OMSIProfileManager.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OMSIProfileManager.Services;

/// <summary>
/// Service for scanning OMSI 2 addon directories and detecting addon states.
/// </summary>
/// <remarks>
/// This service scans the OMSI 2 installation directories to discover installed addons.
/// It reads folder structures in the Vehicles/ and Maps/ directories and identifies
/// disabled addons by the presence of the ".disabled" suffix on folder names.
/// </remarks>
public interface IAddonScanner
{
    /// <summary>
    /// Scans the Vehicles directory for all vehicle addons.
    /// </summary>
    /// <param name="omsi2Path">The root path to the OMSI 2 installation directory. Must be a valid path.</param>
    /// <returns>A list of <see cref="AddonInfo"/> objects representing all discovered vehicle addons.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the Vehicles directory does not exist.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to the Vehicles directory is denied.</exception>
    /// <remarks>
    /// This method scans all subdirectories in the {omsi2Path}/Vehicles folder.
    /// Folders ending with ".disabled" are marked as disabled in the returned AddonInfo objects.
    /// The method filters out system folders and hidden directories.
    /// </remarks>
    Task<List<AddonInfo>> ScanVehiclesAsync(string omsi2Path);

    /// <summary>
    /// Scans the Maps directory for all map addons.
    /// </summary>
    /// <param name="omsi2Path">The root path to the OMSI 2 installation directory. Must be a valid path.</param>
    /// <returns>A list of <see cref="AddonInfo"/> objects representing all discovered map addons.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the Maps directory does not exist.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to the Maps directory is denied.</exception>
    /// <remarks>
    /// This method scans all subdirectories in the {omsi2Path}/Maps folder.
    /// Folders ending with ".disabled" are marked as disabled in the returned AddonInfo objects.
    /// The method filters out system folders and hidden directories.
    /// </remarks>
    Task<List<AddonInfo>> ScanMapsAsync(string omsi2Path);

    /// <summary>
    /// Scans both Vehicles and Maps directories in a single operation.
    /// </summary>
    /// <param name="omsi2Path">The root path to the OMSI 2 installation directory. Must be a valid path.</param>
    /// <returns>A tuple containing lists of vehicle and map addons: (Vehicles, Maps).</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when either the Vehicles or Maps directory does not exist.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to either directory is denied.</exception>
    /// <remarks>
    /// This is a convenience method that internally calls both <see cref="ScanVehiclesAsync"/> 
    /// and <see cref="ScanMapsAsync"/> concurrently for improved performance.
    /// Use this method when you need to scan all addons at once, such as during initial load
    /// or when refreshing the addon list.
    /// </remarks>
    Task<(List<AddonInfo> Vehicles, List<AddonInfo> Maps)> ScanAllAddonsAsync(string omsi2Path);

    /// <summary>
    /// Gets the current enabled/disabled state of all addons without full scanning.
    /// </summary>
    /// <param name="omsi2Path">The root path to the OMSI 2 installation directory. Must be a valid path.</param>
    /// <returns>A tuple containing lists of enabled addon names: (EnabledVehicles, EnabledMaps).</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when either the Vehicles or Maps directory does not exist.</exception>
    /// <remarks>
    /// This method provides a lightweight way to check which addons are currently enabled
    /// without performing a full scan. It only returns the names of addons that do NOT have
    /// the ".disabled" suffix on their folder names.
    /// 
    /// This is useful for quick state checks before profile operations or for backup creation.
    /// </remarks>
    Task<(List<string> EnabledVehicles, List<string> EnabledMaps)> GetCurrentAddonStateAsync(string omsi2Path);
}
