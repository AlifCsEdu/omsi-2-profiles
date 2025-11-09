using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OMSIProfileManager.Services;

/// <summary>
/// Implementation of OMSI 2 path detection service.
/// </summary>
public class OMSIPathDetector : IOMSIPathDetector
{
    private readonly ILogger<OMSIPathDetector> _logger;

    // Required OMSI 2 subdirectories for validation
    private static readonly string[] RequiredDirectories = new[]
    {
        "Vehicles",
        "Maps",
        "Sceneryobjects"
    };

    // OMSI 2 Steam App ID
    private const string OMSI2_STEAM_APPID = "252530";

    public OMSIPathDetector(ILogger<OMSIPathDetector> logger)
    {
        _logger = logger;
    }

    public async Task<string?> DetectOMSI2PathAsync()
    {
        _logger.LogInformation("Starting automatic OMSI 2 path detection");

        // 1. Try registry first (most reliable)
        var registryPath = await SearchRegistryAsync();
        if (!string.IsNullOrEmpty(registryPath) && ValidateOMSI2Path(registryPath))
        {
            _logger.LogInformation("Found OMSI 2 via registry at: {Path}", registryPath);
            return registryPath;
        }

        // 2. Try common paths
        var potentialPaths = await GetPotentialPathsAsync();
        foreach (var path in potentialPaths)
        {
            if (ValidateOMSI2Path(path))
            {
                _logger.LogInformation("Found OMSI 2 at: {Path}", path);
                return path;
            }
        }

        // 3. Search Steam library folders
        var steamLibraryPaths = await SearchSteamLibrariesAsync();
        foreach (var libraryPath in steamLibraryPaths)
        {
            var omsiPath = Path.Combine(libraryPath, "steamapps", "common", "OMSI 2");
            if (ValidateOMSI2Path(omsiPath))
            {
                _logger.LogInformation("Found OMSI 2 in Steam library at: {Path}", omsiPath);
                return omsiPath;
            }
        }

        _logger.LogWarning("Could not automatically detect OMSI 2 installation");
        return null;
    }

    public bool ValidateOMSI2Path(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        if (!Directory.Exists(path))
            return false;

        // Check for required subdirectories
        foreach (var requiredDir in RequiredDirectories)
        {
            var fullPath = Path.Combine(path, requiredDir);
            if (!Directory.Exists(fullPath))
            {
                _logger.LogDebug("Missing required directory: {Dir} in {Path}", requiredDir, path);
                return false;
            }
        }

        // Check for OMSI executable
        var omsiExe = Path.Combine(path, "Omsi.exe");
        if (!File.Exists(omsiExe))
        {
            _logger.LogDebug("Omsi.exe not found in {Path}", path);
            return false;
        }

        return true;
    }

    public async Task<List<string>> GetPotentialPathsAsync()
    {
        var paths = new List<string>();

        // Common Steam installation locations
        var commonBasePaths = new[]
        {
            @"C:\Program Files (x86)\Steam",
            @"C:\Program Files\Steam",
            @"D:\Steam",
            @"D:\SteamLibrary",
            @"E:\Steam",
            @"E:\SteamLibrary",
            @"F:\Steam",
            @"F:\SteamLibrary"
        };

        await Task.Run(() =>
        {
            foreach (var basePath in commonBasePaths)
            {
                var omsiPath = Path.Combine(basePath, "steamapps", "common", "OMSI 2");
                if (Directory.Exists(omsiPath))
                {
                    paths.Add(omsiPath);
                }
            }

            // Add direct common paths
            var directPaths = new[]
            {
                @"C:\OMSI 2",
                @"C:\Games\OMSI 2",
                @"D:\OMSI 2",
                @"D:\Games\OMSI 2"
            };

            paths.AddRange(directPaths.Where(Directory.Exists));
        });

        _logger.LogDebug("Found {Count} potential OMSI 2 paths", paths.Count);
        return paths;
    }

    public async Task<string?> SearchRegistryAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                // Try Steam registry key for OMSI 2
                var steamKey = $@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App {OMSI2_STEAM_APPID}";
                
                // Try both 64-bit and 32-bit registry views
                var registryViews = new[]
                {
                    RegistryView.Registry64,
                    RegistryView.Registry32
                };

                foreach (var view in registryViews)
                {
                    using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                    using var key = baseKey.OpenSubKey(steamKey);
                    
                    if (key != null)
                    {
                        var installLocation = key.GetValue("InstallLocation") as string;
                        if (!string.IsNullOrEmpty(installLocation))
                        {
                            _logger.LogInformation("Found OMSI 2 in registry: {Path}", installLocation);
                            return installLocation;
                        }
                    }
                }

                // Try Steam's main registry key to find Steam installation
                using (var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
                using (var steamKey64 = baseKey.OpenSubKey(@"SOFTWARE\Wow6432Node\Valve\Steam"))
                {
                    if (steamKey64 != null)
                    {
                        var steamPath = steamKey64.GetValue("InstallPath") as string;
                        if (!string.IsNullOrEmpty(steamPath))
                        {
                            var omsiPath = Path.Combine(steamPath, "steamapps", "common", "OMSI 2");
                            if (ValidateOMSI2Path(omsiPath))
                            {
                                _logger.LogInformation("Found OMSI 2 via Steam path: {Path}", omsiPath);
                                return omsiPath;
                            }
                        }
                    }
                }

                _logger.LogDebug("No OMSI 2 installation found in registry");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error searching registry for OMSI 2");
                return null;
            }
        });
    }

    public ValidationResult ValidateWithDetails(string path)
    {
        var result = new ValidationResult
        {
            Path = path,
            IsValid = false,
            MissingComponents = new List<string>(),
            FoundComponents = new List<string>()
        };

        if (string.IsNullOrWhiteSpace(path))
        {
            result.ErrorMessage = "Path is empty or null";
            return result;
        }

        if (!Directory.Exists(path))
        {
            result.ErrorMessage = "Directory does not exist";
            return result;
        }

        // Check required directories
        foreach (var requiredDir in RequiredDirectories)
        {
            var fullPath = Path.Combine(path, requiredDir);
            if (Directory.Exists(fullPath))
            {
                result.FoundComponents.Add(requiredDir);
            }
            else
            {
                result.MissingComponents.Add(requiredDir);
            }
        }

        // Check for OMSI executable
        var omsiExe = Path.Combine(path, "Omsi.exe");
        if (File.Exists(omsiExe))
        {
            result.FoundComponents.Add("Omsi.exe");
        }
        else
        {
            result.MissingComponents.Add("Omsi.exe");
        }

        result.IsValid = result.MissingComponents.Count == 0;
        
        if (!result.IsValid)
        {
            result.ErrorMessage = $"Missing components: {string.Join(", ", result.MissingComponents)}";
        }

        return result;
    }

    /// <summary>
    /// Searches for Steam library folders by reading libraryfolders.vdf.
    /// </summary>
    private async Task<List<string>> SearchSteamLibrariesAsync()
    {
        var libraries = new List<string>();

        await Task.Run(() =>
        {
            try
            {
                // Find Steam installation
                string? steamPath = null;

                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Wow6432Node\Valve\Steam"))
                {
                    steamPath = key?.GetValue("InstallPath") as string;
                }

                if (string.IsNullOrEmpty(steamPath))
                    return;

                // Read libraryfolders.vdf
                var libraryFile = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(libraryFile))
                    return;

                var lines = File.ReadAllLines(libraryFile);
                foreach (var line in lines)
                {
                    // Look for path entries in VDF format
                    if (line.Contains("\"path\""))
                    {
                        var start = line.IndexOf("\"", line.IndexOf("\"path\"") + 6) + 1;
                        var end = line.LastIndexOf("\"");
                        if (start > 0 && end > start)
                        {
                            var libraryPath = line.Substring(start, end - start);
                            libraryPath = libraryPath.Replace("\\\\", "\\");
                            libraries.Add(libraryPath);
                        }
                    }
                }

                _logger.LogDebug("Found {Count} Steam library folders", libraries.Count);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error searching Steam libraries");
            }
        });

        return libraries;
    }
}
