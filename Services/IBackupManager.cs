using OMSIProfileManager.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OMSIProfileManager.Services;

/// <summary>
/// Service for managing backups of addon states.
/// </summary>
/// <remarks>
/// This service creates and manages backups of the current addon configuration.
/// Backups capture which addons are enabled/disabled at a specific point in time,
/// allowing users to restore their addon state if something goes wrong during profile switching.
/// 
/// Backups are automatically created before profile loads (if auto-backup is enabled)
/// and can be manually restored by the user.
/// </remarks>
public interface IBackupManager
{
    /// <summary>
    /// Creates a backup of the current addon state.
    /// </summary>
    /// <param name="omsi2Path">The root path to the OMSI 2 installation directory. Must be a valid path.</param>
    /// <param name="profileId">Optional profile ID to associate with this backup.</param>
    /// <param name="profileName">Optional profile name to associate with this backup for display purposes.</param>
    /// <returns>A <see cref="BackupState"/> object representing the created backup with a unique ID and timestamp.</returns>
    /// <exception cref="DirectoryNotFoundException">Thrown when the OMSI 2 directories do not exist.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to directories is denied.</exception>
    /// <remarks>
    /// This method scans the current addon state and saves it to a JSON file in the AppData directory.
    /// Each backup includes:
    /// - Unique backup ID
    /// - Creation timestamp
    /// - Lists of enabled vehicles and maps
    /// - Optional profile association for context
    /// 
    /// Backups are typically created automatically before loading a profile to allow easy restoration
    /// if the user wants to revert changes.
    /// </remarks>
    Task<BackupState> CreateBackupAsync(string omsi2Path, string? profileId = null, string? profileName = null);

    /// <summary>
    /// Restores addons from a backup state.
    /// </summary>
    /// <param name="backup">The backup state to restore. Must not be null.</param>
    /// <param name="omsi2Path">The root path to the OMSI 2 installation directory. Must be a valid path.</param>
    /// <exception cref="ArgumentNullException">Thrown when backup is null.</exception>
    /// <exception cref="DirectoryNotFoundException">Thrown when the OMSI 2 directories do not exist.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to directories is denied.</exception>
    /// <remarks>
    /// This method restores the addon state from a backup by:
    /// 1. Disabling ALL current addons (adds ".disabled" suffix)
    /// 2. Enabling only the addons that were enabled in the backup (removes ".disabled" suffix)
    /// 
    /// This operation is atomic-like: if an error occurs, it attempts to leave the system
    /// in a consistent state. However, it's recommended to create a backup before restoring
    /// another backup.
    /// </remarks>
    Task RestoreBackupAsync(BackupState backup, string omsi2Path);

    /// <summary>
    /// Lists all available backups.
    /// </summary>
    /// <returns>A list of all <see cref="BackupState"/> objects, ordered by creation date (newest first).</returns>
    /// <exception cref="IOException">Thrown when there's an error reading the backup directory.</exception>
    /// <remarks>
    /// This method scans the backup directory in AppData and loads all backup JSON files.
    /// Invalid or corrupted backup files are logged and skipped.
    /// 
    /// The returned list includes all backup metadata including creation timestamps,
    /// associated profile names, and enabled addon lists.
    /// </remarks>
    Task<List<BackupState>> ListBackupsAsync();

    /// <summary>
    /// Deletes a backup by ID.
    /// </summary>
    /// <param name="backupId">The unique ID of the backup to delete. Must not be null or empty.</param>
    /// <exception cref="ArgumentException">Thrown when backupId is null or empty.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the backup file does not exist.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to delete the file is denied.</exception>
    /// <remarks>
    /// This method permanently deletes a backup file from disk.
    /// The operation cannot be undone, so care should be taken when deleting backups.
    /// 
    /// Typical usage is for manual cleanup or when implementing automatic cleanup policies.
    /// </remarks>
    Task DeleteBackupAsync(string backupId);

    /// <summary>
    /// Cleans up old backups, keeping only the most recent N backups.
    /// </summary>
    /// <param name="maxBackupsToKeep">The maximum number of backups to retain. Must be greater than 0.</param>
    /// <exception cref="ArgumentException">Thrown when maxBackupsToKeep is less than 1.</exception>
    /// <exception cref="UnauthorizedAccessException">Thrown when access to delete files is denied.</exception>
    /// <remarks>
    /// This method implements an automatic cleanup policy to prevent backup files from
    /// accumulating indefinitely. It:
    /// 1. Lists all backups sorted by creation date (newest first)
    /// 2. Identifies backups beyond the retention limit
    /// 3. Deletes the excess backups
    /// 
    /// This is typically called periodically (e.g., on application startup) to maintain
    /// a reasonable number of backups. The default configuration keeps the 10 most recent backups.
    /// </remarks>
    Task CleanupOldBackupsAsync(int maxBackupsToKeep);
}
