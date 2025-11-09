using Microsoft.Extensions.Logging;
using OMSIProfileManager.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OMSIProfileManager.Services;

/// <summary>
/// Implementation of addon scanner service with caching and validation.
/// </summary>
public class AddonScanner : IAddonScanner
{
    private readonly ILogger<AddonScanner> _logger;
    private readonly SemaphoreSlim _cacheLock = new(1, 1);
    
    // Cache to avoid repeated scans
    private CachedScanResult? _cachedVehicles;
    private CachedScanResult? _cachedMaps;
    private readonly TimeSpan _cacheExpiration = TimeSpan.FromMinutes(5);

    public AddonScanner(ILogger<AddonScanner> logger)
    {
        _logger = logger;
    }

    public async Task<List<AddonInfo>> ScanVehiclesAsync(string omsi2Path)
    {
        var vehiclesPath = Path.Combine(omsi2Path, "Vehicles");
        var (result, cache) = await ScanAddonDirectoryAsync(vehiclesPath, AddonType.Vehicle, _cachedVehicles);
        _cachedVehicles = cache;
        return result;
    }

    public async Task<List<AddonInfo>> ScanMapsAsync(string omsi2Path)
    {
        var mapsPath = Path.Combine(omsi2Path, "Maps");
        var (result, cache) = await ScanAddonDirectoryAsync(mapsPath, AddonType.Map, _cachedMaps);
        _cachedMaps = cache;
        return result;
    }

    public async Task<(List<AddonInfo> Vehicles, List<AddonInfo> Maps)> ScanAllAddonsAsync(string omsi2Path)
    {
        var vehiclesTask = ScanVehiclesAsync(omsi2Path);
        var mapsTask = ScanMapsAsync(omsi2Path);

        await Task.WhenAll(vehiclesTask, mapsTask);

        return (await vehiclesTask, await mapsTask);
    }

    public async Task<(List<string> EnabledVehicles, List<string> EnabledMaps)> GetCurrentAddonStateAsync(string omsi2Path)
    {
        var vehiclesTask = ScanVehiclesAsync(omsi2Path);
        var mapsTask = ScanMapsAsync(omsi2Path);

        await Task.WhenAll(vehiclesTask, mapsTask);

        var enabledVehicles = (await vehiclesTask)
            .Where(v => v.IsEnabled)
            .Select(v => v.FolderName)
            .ToList();

        var enabledMaps = (await mapsTask)
            .Where(m => m.IsEnabled)
            .Select(m => m.FolderName)
            .ToList();

        return (enabledVehicles, enabledMaps);
    }

    /// <summary>
    /// Clears the internal cache to force a fresh scan on next request.
    /// </summary>
    public void ClearCache()
    {
        _cacheLock.Wait();
        try
        {
            _cachedVehicles = null;
            _cachedMaps = null;
            _logger.LogDebug("Addon scanner cache cleared");
        }
        finally
        {
            _cacheLock.Release();
        }
    }

    private async Task<(List<AddonInfo> Addons, CachedScanResult? Cache)> ScanAddonDirectoryAsync(
        string directoryPath, 
        AddonType type, 
        CachedScanResult? cache)
    {
        // Check cache validity
        await _cacheLock.WaitAsync();
        try
        {
            if (cache != null && 
                cache.DirectoryPath == directoryPath && 
                DateTime.UtcNow - cache.ScanTime < _cacheExpiration)
            {
                _logger.LogDebug("Returning cached {Type} scan results (age: {Age:F1}s)", 
                    type, (DateTime.UtcNow - cache.ScanTime).TotalSeconds);
                return (cache.Addons.ToList(), cache); // Return copy
            }
        }
        finally
        {
            _cacheLock.Release();
        }

        // Perform fresh scan
        var addons = new List<AddonInfo>();
        var scanStartTime = DateTime.UtcNow;

        try
        {
            if (!Directory.Exists(directoryPath))
            {
                _logger.LogWarning("Directory not found: {Path}", directoryPath);
                return addons;
            }

            var directories = Directory.GetDirectories(directoryPath);
            _logger.LogInformation("Scanning {Count} {Type} folders in {Path}", 
                directories.Length, type, directoryPath);

            await Task.Run(() =>
            {
                var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var dir in directories)
                {
                    try
                    {
                        var folderName = Path.GetFileName(dir);
                        if (string.IsNullOrEmpty(folderName))
                            continue;

                        var isDisabled = folderName.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
                        var actualName = isDisabled ? folderName[..^9] : folderName;

                        // Check for duplicate names (both enabled and disabled versions)
                        if (!seenNames.Add(actualName))
                        {
                            _logger.LogWarning("Duplicate {Type} addon detected: {Name} (path: {Path})", 
                                type, actualName, dir);
                        }

                        // Validate folder accessibility
                        DateTime lastModified;
                        try
                        {
                            lastModified = Directory.GetLastWriteTime(dir);
                            
                            // Check if folder is empty
                            var hasFiles = Directory.EnumerateFileSystemEntries(dir).Any();
                            if (!hasFiles)
                            {
                                _logger.LogWarning("Empty {Type} addon folder: {Name}", type, actualName);
                            }
                        }
                        catch (UnauthorizedAccessException)
                        {
                            _logger.LogWarning("Access denied to {Type} addon folder: {Path}", type, dir);
                            lastModified = DateTime.MinValue;
                        }

                        var addon = new AddonInfo
                        {
                            FolderName = actualName,
                            Type = type,
                            IsEnabled = !isDisabled,
                            FullPath = dir,
                            LastModified = lastModified
                        };

                        addons.Add(addon);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to process {Type} addon directory: {Path}", type, dir);
                    }
                }
            });

            addons = addons.OrderBy(a => a.FolderName, StringComparer.OrdinalIgnoreCase).ToList();
            
            var enabledCount = addons.Count(a => a.IsEnabled);
            var disabledCount = addons.Count - enabledCount;
            _logger.LogInformation("Scan complete: {Total} {Type} addons ({Enabled} enabled, {Disabled} disabled)", 
                addons.Count, type, enabledCount, disabledCount);

            // Update cache
            await _cacheLock.WaitAsync();
            try
            {
                cache = new CachedScanResult
                {
                    DirectoryPath = directoryPath,
                    Addons = addons.ToList(),
                    ScanTime = scanStartTime
                };
            }
            finally
            {
                _cacheLock.Release();
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied to {Type} directory: {Path}", type, directoryPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan {Type} directory: {Path}", type, directoryPath);
        }

        return (addons, cache);
    }

    /// <summary>
    /// Internal cache structure.
    /// </summary>
    private class CachedScanResult
    {
        public required string DirectoryPath { get; init; }
        public required List<AddonInfo> Addons { get; init; }
        public required DateTime ScanTime { get; init; }
    }
}
