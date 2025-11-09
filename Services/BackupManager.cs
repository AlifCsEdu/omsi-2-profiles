using Microsoft.Extensions.Logging;
using OMSIProfileManager.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace OMSIProfileManager.Services;

/// <summary>
/// Implementation of backup manager service with enhanced reliability.
/// </summary>
public class BackupManager : IBackupManager
{
    private readonly ILogger<BackupManager> _logger;
    private readonly IAddonScanner _addonScanner;
    private readonly string _backupFolderPath;
    private readonly int _maxRetries = 3;
    private readonly TimeSpan _retryDelay = TimeSpan.FromMilliseconds(500);

    public BackupManager(ILogger<BackupManager> logger, IAddonScanner addonScanner)
    {
        _logger = logger;
        _addonScanner = addonScanner;
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        _backupFolderPath = Path.Combine(appDataPath, "OMSIProfileManager", "backups");
        Directory.CreateDirectory(_backupFolderPath);
    }

    public async Task<BackupState> CreateBackupAsync(string omsi2Path, string? profileId = null, string? profileName = null)
    {
        if (string.IsNullOrWhiteSpace(omsi2Path))
        {
            throw new ArgumentException("OMSI 2 path cannot be empty", nameof(omsi2Path));
        }

        if (!Directory.Exists(omsi2Path))
        {
            throw new DirectoryNotFoundException($"OMSI 2 directory not found: {omsi2Path}");
        }

        try
        {
            _logger.LogInformation("Creating backup from {Path}", omsi2Path);
            var (enabledVehicles, enabledMaps) = await _addonScanner.GetCurrentAddonStateAsync(omsi2Path);

            var backup = new BackupState
            {
                Id = GenerateBackupId(),
                CreatedAt = DateTime.UtcNow,
                EnabledVehicles = enabledVehicles,
                EnabledMaps = enabledMaps,
                ProfileId = profileId,
                ProfileName = profileName,
                Description = profileName != null ? $"Before loading '{profileName}'" : "Manual backup"
            };

            var backupFilePath = Path.Combine(_backupFolderPath, $"{backup.Id}.json");
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(backup, options);
            
            // Write to temp file then move (atomic operation)
            var tempPath = backupFilePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json);
            File.Move(tempPath, backupFilePath, overwrite: true);

            _logger.LogInformation("Created backup: {Id} ({VehicleCount} vehicles, {MapCount} maps)", 
                backup.Id, enabledVehicles.Count, enabledMaps.Count);
            return backup;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create backup from {Path}", omsi2Path);
            throw;
        }
    }

    public async Task RestoreBackupAsync(BackupState backup, string omsi2Path)
    {
        if (backup == null)
        {
            throw new ArgumentNullException(nameof(backup));
        }

        if (string.IsNullOrWhiteSpace(omsi2Path))
        {
            throw new ArgumentException("OMSI 2 path cannot be empty", nameof(omsi2Path));
        }

        if (!Directory.Exists(omsi2Path))
        {
            throw new DirectoryNotFoundException($"OMSI 2 directory not found: {omsi2Path}");
        }

        try
        {
            _logger.LogInformation("Restoring backup: {Id} ({VehicleCount} vehicles, {MapCount} maps)", 
                backup.Id, backup.EnabledVehicles.Count, backup.EnabledMaps.Count);

            var vehiclesPath = Path.Combine(omsi2Path, "Vehicles");
            var mapsPath = Path.Combine(omsi2Path, "Maps");

            // Validate paths exist
            if (!Directory.Exists(vehiclesPath))
            {
                throw new DirectoryNotFoundException($"Vehicles directory not found: {vehiclesPath}");
            }

            if (!Directory.Exists(mapsPath))
            {
                throw new DirectoryNotFoundException($"Maps directory not found: {mapsPath}");
            }

            // Create a safety backup before restoring
            var safetyBackup = await CreateBackupAsync(omsi2Path, null, "Auto-backup before restore");
            _logger.LogInformation("Created safety backup: {Id}", safetyBackup.Id);

            try
            {
                // Disable all addons first
                await DisableAllAddonsAsync(vehiclesPath);
                await DisableAllAddonsAsync(mapsPath);

                // Enable backed-up addons
                await EnableAddonsAsync(vehiclesPath, backup.EnabledVehicles.ToArray());
                await EnableAddonsAsync(mapsPath, backup.EnabledMaps.ToArray());

                _logger.LogInformation("Backup restored successfully: {Id}", backup.Id);
            }
            catch (Exception)
            {
                // If restore fails, try to restore the safety backup
                _logger.LogWarning("Restore failed, attempting to restore safety backup: {Id}", safetyBackup.Id);
                await RestoreSafetyBackupAsync(safetyBackup, omsi2Path);
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore backup: {Id}", backup.Id);
            throw;
        }
    }

    private async Task RestoreSafetyBackupAsync(BackupState backup, string omsi2Path)
    {
        try
        {
            var vehiclesPath = Path.Combine(omsi2Path, "Vehicles");
            var mapsPath = Path.Combine(omsi2Path, "Maps");

            await DisableAllAddonsAsync(vehiclesPath);
            await DisableAllAddonsAsync(mapsPath);
            await EnableAddonsAsync(vehiclesPath, backup.EnabledVehicles.ToArray());
            await EnableAddonsAsync(mapsPath, backup.EnabledMaps.ToArray());

            _logger.LogInformation("Successfully restored safety backup");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore safety backup - manual intervention required");
        }
    }

    public async Task<List<BackupState>> ListBackupsAsync()
    {
        try
        {
            var backups = new List<BackupState>();
            
            if (!Directory.Exists(_backupFolderPath))
            {
                _logger.LogDebug("Backup folder does not exist yet: {Path}", _backupFolderPath);
                return backups;
            }

            var backupFiles = Directory.GetFiles(_backupFolderPath, "*.json")
                .Where(f => !f.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            _logger.LogDebug("Found {Count} backup files", backupFiles.Length);

            foreach (var file in backupFiles)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var backup = JsonSerializer.Deserialize<BackupState>(json);
                    
                    if (backup != null)
                    {
                        backups.Add(backup);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to deserialize backup file (null result): {File}", Path.GetFileName(file));
                    }
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Corrupted backup file: {File}", Path.GetFileName(file));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load backup file: {File}", Path.GetFileName(file));
                }
            }

            var result = backups.OrderByDescending(b => b.CreatedAt).ToList();
            _logger.LogInformation("Listed {Count} valid backups", result.Count);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list backups");
            return new List<BackupState>();
        }
    }

    public async Task DeleteBackupAsync(string backupId)
    {
        if (string.IsNullOrWhiteSpace(backupId))
        {
            throw new ArgumentException("Backup ID cannot be empty", nameof(backupId));
        }

        try
        {
            var backupFilePath = Path.Combine(_backupFolderPath, $"{backupId}.json");
            
            if (!File.Exists(backupFilePath))
            {
                _logger.LogWarning("Backup file not found: {Id}", backupId);
                return;
            }

            File.Delete(backupFilePath);
            _logger.LogInformation("Deleted backup: {Id}", backupId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete backup: {Id}", backupId);
            throw;
        }
    }

    public async Task CleanupOldBackupsAsync(int maxBackupsToKeep)
    {
        if (maxBackupsToKeep < 1)
        {
            throw new ArgumentException("Must keep at least 1 backup", nameof(maxBackupsToKeep));
        }

        try
        {
            var backups = await ListBackupsAsync();
            var backupsToDelete = backups.Skip(maxBackupsToKeep).ToList();

            if (backupsToDelete.Count == 0)
            {
                _logger.LogDebug("No old backups to clean up ({Current} <= {Max})", backups.Count, maxBackupsToKeep);
                return;
            }

            _logger.LogInformation("Cleaning up {Count} old backups (keeping {Max})", backupsToDelete.Count, maxBackupsToKeep);

            foreach (var backup in backupsToDelete)
            {
                try
                {
                    await DeleteBackupAsync(backup.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete old backup: {Id}", backup.Id);
                }
            }

            _logger.LogInformation("Cleanup complete: {Count} backups deleted", backupsToDelete.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old backups");
        }
    }

    private string GenerateBackupId()
    {
        return $"backup_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid().ToString("N")[..6]}";
    }

    private async Task DisableAllAddonsAsync(string addonDirectory)
    {
        if (!Directory.Exists(addonDirectory))
        {
            _logger.LogWarning("Addon directory not found: {Path}", addonDirectory);
            return;
        }

        await Task.Run(() =>
        {
            var directories = Directory.GetDirectories(addonDirectory);
            var disabledCount = 0;
            var failedCount = 0;

            foreach (var dir in directories)
            {
                var folderName = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(folderName) || 
                    folderName.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var targetPath = dir + ".disabled";

                // Retry logic for locked folders
                for (int retry = 0; retry < _maxRetries; retry++)
                {
                    try
                    {
                        Directory.Move(dir, targetPath);
                        disabledCount++;
                        break;
                    }
                    catch (IOException ex) when (retry < _maxRetries - 1)
                    {
                        _logger.LogDebug("Retry {Retry}/{Max}: Failed to disable {Name}, waiting...", 
                            retry + 1, _maxRetries, folderName);
                        Thread.Sleep(_retryDelay);
                    }
                    catch (IOException ex) when (retry == _maxRetries - 1)
                    {
                        _logger.LogWarning(ex, "Failed to disable addon after {Max} retries: {Name}", 
                            _maxRetries, folderName);
                        failedCount++;
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        _logger.LogWarning(ex, "Access denied when disabling addon: {Name}", folderName);
                        failedCount++;
                        break;
                    }
                }
            }

            _logger.LogInformation("Disabled {Count} addons in {Path} ({Failed} failed)", 
                disabledCount, Path.GetFileName(addonDirectory), failedCount);
        });
    }

    private async Task EnableAddonsAsync(string addonDirectory, string[] addonNames)
    {
        if (!Directory.Exists(addonDirectory))
        {
            _logger.LogWarning("Addon directory not found: {Path}", addonDirectory);
            return;
        }

        await Task.Run(() =>
        {
            var enabledCount = 0;
            var notFoundCount = 0;
            var failedCount = 0;

            foreach (var addonName in addonNames)
            {
                if (string.IsNullOrWhiteSpace(addonName))
                    continue;

                var disabledPath = Path.Combine(addonDirectory, addonName + ".disabled");
                var enabledPath = Path.Combine(addonDirectory, addonName);

                if (!Directory.Exists(disabledPath))
                {
                    _logger.LogDebug("Addon not found (may already be enabled or missing): {Name}", addonName);
                    notFoundCount++;
                    continue;
                }

                // Retry logic for locked folders
                for (int retry = 0; retry < _maxRetries; retry++)
                {
                    try
                    {
                        Directory.Move(disabledPath, enabledPath);
                        enabledCount++;
                        break;
                    }
                    catch (IOException ex) when (retry < _maxRetries - 1)
                    {
                        _logger.LogDebug("Retry {Retry}/{Max}: Failed to enable {Name}, waiting...", 
                            retry + 1, _maxRetries, addonName);
                        Thread.Sleep(_retryDelay);
                    }
                    catch (IOException ex) when (retry == _maxRetries - 1)
                    {
                        _logger.LogWarning(ex, "Failed to enable addon after {Max} retries: {Name}", 
                            _maxRetries, addonName);
                        failedCount++;
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        _logger.LogWarning(ex, "Access denied when enabling addon: {Name}", addonName);
                        failedCount++;
                        break;
                    }
                }
            }

            _logger.LogInformation("Enabled {Count}/{Total} addons in {Path} ({NotFound} not found, {Failed} failed)", 
                enabledCount, addonNames.Length, Path.GetFileName(addonDirectory), notFoundCount, failedCount);
        });
    }
}
