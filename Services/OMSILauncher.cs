using Microsoft.Extensions.Logging;
using OMSIProfileManager.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OMSIProfileManager.Services;

/// <summary>
/// Implementation of OMSI launcher service with robust error handling.
/// </summary>
public class OMSILauncher : IOMSILauncher
{
    private readonly ILogger<OMSILauncher> _logger;
    private readonly IAddonScanner _addonScanner;
    private readonly int _maxRetries = 3;
    private readonly TimeSpan _retryDelay = TimeSpan.FromMilliseconds(500);

    public OMSILauncher(ILogger<OMSILauncher> logger, IAddonScanner addonScanner)
    {
        _logger = logger;
        _addonScanner = addonScanner;
    }

    public async Task<bool> LoadProfileAsync(Profile profile, string omsi2Path)
    {
        if (profile == null)
        {
            _logger.LogError("Profile cannot be null");
            return false;
        }

        if (!profile.IsValid(out var errorMessage))
        {
            _logger.LogError("Invalid profile: {Error}", errorMessage);
            return false;
        }

        if (string.IsNullOrWhiteSpace(omsi2Path))
        {
            _logger.LogError("OMSI 2 path cannot be empty");
            return false;
        }

        if (!Directory.Exists(omsi2Path))
        {
            _logger.LogError("OMSI 2 directory not found: {Path}", omsi2Path);
            return false;
        }

        try
        {
            _logger.LogInformation("Loading profile: {Name} ({VehicleCount} vehicles, {MapCount} maps)", 
                profile.Name, profile.Vehicles.Count, profile.Maps.Count);

            if (IsOmsi2Running())
            {
                _logger.LogWarning("OMSI 2 is currently running - addon changes may not take effect until restart");
                // Continue anyway, but warn the user
            }

            var vehiclesPath = Path.Combine(omsi2Path, "Vehicles");
            var mapsPath = Path.Combine(omsi2Path, "Maps");

            // Validate directories exist
            if (!Directory.Exists(vehiclesPath))
            {
                _logger.LogError("Vehicles directory not found: {Path}", vehiclesPath);
                return false;
            }

            if (!Directory.Exists(mapsPath))
            {
                _logger.LogError("Maps directory not found: {Path}", mapsPath);
                return false;
            }

            // Disable all addons first
            await DisableAllAddonsAsync(vehiclesPath);
            await DisableAllAddonsAsync(mapsPath);

            // Enable profile-specific addons
            await EnableAddonsAsync(vehiclesPath, profile.Vehicles.ToArray());
            await EnableAddonsAsync(mapsPath, profile.Maps.ToArray());

            _logger.LogInformation("Profile loaded successfully: {Name}", profile.Name);
            return true;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied when loading profile: {Name}", profile.Name);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load profile: {Name}", profile.Name);
            return false;
        }
    }

    public async Task DisableAllAddonsAsync(string addonDirectory)
    {
        if (string.IsNullOrWhiteSpace(addonDirectory))
        {
            throw new ArgumentException("Addon directory path cannot be empty", nameof(addonDirectory));
        }

        try
        {
            if (!Directory.Exists(addonDirectory))
            {
                _logger.LogWarning("Directory not found: {Path}", addonDirectory);
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

                    var disabledPath = dir + ".disabled";

                    // Check if target already exists
                    if (Directory.Exists(disabledPath))
                    {
                        _logger.LogWarning("Disabled version already exists: {Name}", folderName);
                        failedCount++;
                        continue;
                    }

                    // Retry logic for locked folders
                    bool success = false;
                    for (int retry = 0; retry < _maxRetries; retry++)
                    {
                        try
                        {
                            Directory.Move(dir, disabledPath);
                            disabledCount++;
                            success = true;
                            _logger.LogDebug("Disabled addon: {Name}", folderName);
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disable addons in: {Path}", addonDirectory);
            throw;
        }
    }

    public async Task EnableAddonsAsync(string addonDirectory, string[] addonNames)
    {
        if (string.IsNullOrWhiteSpace(addonDirectory))
        {
            throw new ArgumentException("Addon directory path cannot be empty", nameof(addonDirectory));
        }

        if (addonNames == null)
        {
            throw new ArgumentNullException(nameof(addonNames));
        }

        try
        {
            if (!Directory.Exists(addonDirectory))
            {
                _logger.LogWarning("Directory not found: {Path}", addonDirectory);
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
                    {
                        _logger.LogWarning("Skipping empty addon name");
                        continue;
                    }

                    var disabledPath = Path.Combine(addonDirectory, addonName + ".disabled");
                    var enabledPath = Path.Combine(addonDirectory, addonName);

                    // Check if already enabled
                    if (Directory.Exists(enabledPath) && !Directory.Exists(disabledPath))
                    {
                        _logger.LogDebug("Addon already enabled: {Name}", addonName);
                        enabledCount++;
                        continue;
                    }

                    if (!Directory.Exists(disabledPath))
                    {
                        _logger.LogWarning("Addon not found: {Name}", addonName);
                        notFoundCount++;
                        continue;
                    }

                    // Check if target already exists (conflict)
                    if (Directory.Exists(enabledPath))
                    {
                        _logger.LogWarning("Cannot enable {Name} - enabled version already exists", addonName);
                        failedCount++;
                        continue;
                    }

                    // Retry logic for locked folders
                    bool success = false;
                    for (int retry = 0; retry < _maxRetries; retry++)
                    {
                        try
                        {
                            Directory.Move(disabledPath, enabledPath);
                            enabledCount++;
                            success = true;
                            _logger.LogDebug("Enabled addon: {Name}", addonName);
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to enable addons in: {Path}", addonDirectory);
            throw;
        }
    }

    public async Task<bool> LaunchOmsi2Async(string omsi2Path)
    {
        if (string.IsNullOrWhiteSpace(omsi2Path))
        {
            _logger.LogError("OMSI 2 path cannot be empty");
            return false;
        }

        if (!Directory.Exists(omsi2Path))
        {
            _logger.LogError("OMSI 2 directory not found: {Path}", omsi2Path);
            return false;
        }

        try
        {
            var omsiExePath = Path.Combine(omsi2Path, "Omsi.exe");
            
            if (!File.Exists(omsiExePath))
            {
                _logger.LogError("OMSI 2 executable not found at: {Path}", omsiExePath);
                return false;
            }

            if (IsOmsi2Running())
            {
                _logger.LogWarning("OMSI 2 is already running");
                return false;
            }

            await Task.Run(() =>
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = omsiExePath,
                    WorkingDirectory = omsi2Path,
                    UseShellExecute = true
                };

                var process = Process.Start(startInfo);
                
                if (process == null)
                {
                    throw new InvalidOperationException("Failed to start OMSI 2 process");
                }

                _logger.LogDebug("OMSI 2 process started with ID: {ProcessId}", process.Id);
            });

            _logger.LogInformation("Launched OMSI 2 from: {Path}", omsiExePath);
            return true;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.LogError(ex, "Failed to launch OMSI 2 - Windows error (may need admin rights or file is missing)");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to launch OMSI 2");
            return false;
        }
    }

    public bool IsOmsi2Running()
    {
        try
        {
            var processes = Process.GetProcessesByName("Omsi");
            var isRunning = processes.Length > 0;
            
            if (isRunning)
            {
                _logger.LogDebug("OMSI 2 is running ({Count} process(es) found)", processes.Length);
            }

            // Dispose process objects
            foreach (var process in processes)
            {
                process.Dispose();
            }

            return isRunning;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to check if OMSI 2 is running");
            return false;
        }
    }

    public async Task RestoreAllAddonsAsync(string omsi2Path)
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
            _logger.LogInformation("Restoring all addons in {Path}", omsi2Path);

            var vehiclesPath = Path.Combine(omsi2Path, "Vehicles");
            var mapsPath = Path.Combine(omsi2Path, "Maps");

            await Task.WhenAll(
                RestoreAddonsInDirectoryAsync(vehiclesPath),
                RestoreAddonsInDirectoryAsync(mapsPath)
            );

            _logger.LogInformation("All addons restored");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore addons");
            throw;
        }
    }

    private async Task RestoreAddonsInDirectoryAsync(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            _logger.LogWarning("Directory path is empty, skipping restore");
            return;
        }

        if (!Directory.Exists(directory))
        {
            _logger.LogWarning("Directory not found: {Path}", directory);
            return;
        }

        await Task.Run(() =>
        {
            var disabledDirectories = Directory.GetDirectories(directory, "*.disabled");
            var restoredCount = 0;
            var failedCount = 0;

            _logger.LogDebug("Found {Count} disabled addons in {Path}", 
                disabledDirectories.Length, Path.GetFileName(directory));

            foreach (var disabledDir in disabledDirectories)
            {
                try
                {
                    var folderName = Path.GetFileName(disabledDir);
                    if (string.IsNullOrEmpty(folderName))
                        continue;

                    var enabledPath = disabledDir[..^9]; // Remove ".disabled"

                    // Check if target already exists
                    if (Directory.Exists(enabledPath))
                    {
                        _logger.LogWarning("Cannot restore {Name} - enabled version already exists", folderName);
                        failedCount++;
                        continue;
                    }

                    // Retry logic
                    bool success = false;
                    for (int retry = 0; retry < _maxRetries; retry++)
                    {
                        try
                        {
                            Directory.Move(disabledDir, enabledPath);
                            restoredCount++;
                            success = true;
                            _logger.LogDebug("Restored addon: {Name}", folderName);
                            break;
                        }
                        catch (IOException) when (retry < _maxRetries - 1)
                        {
                            Thread.Sleep(_retryDelay);
                        }
                    }

                    if (!success)
                    {
                        failedCount++;
                    }
                }
                catch (IOException ex)
                {
                    _logger.LogWarning(ex, "Failed to restore addon: {Dir}", Path.GetFileName(disabledDir));
                    failedCount++;
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger.LogWarning(ex, "Access denied when restoring addon: {Dir}", Path.GetFileName(disabledDir));
                    failedCount++;
                }
            }

            _logger.LogInformation("Restored {Count} addons in {Path} ({Failed} failed)", 
                restoredCount, Path.GetFileName(directory), failedCount);
        });
    }
}
