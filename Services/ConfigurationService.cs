using Microsoft.Extensions.Logging;
using OMSIProfileManager.Models;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace OMSIProfileManager.Services;

/// <summary>
/// Implementation of configuration service with enhanced path detection.
/// </summary>
public class ConfigurationService : IConfigurationService
{
    private readonly ILogger<ConfigurationService> _logger;
    private readonly IOMSIPathDetector _pathDetector;
    private readonly string _configFilePath;
    private AppConfig? _cachedConfig;

    public ConfigurationService(ILogger<ConfigurationService> logger, IOMSIPathDetector pathDetector)
    {
        _logger = logger;
        _pathDetector = pathDetector;
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "OMSIProfileManager");
        Directory.CreateDirectory(appFolder);
        _configFilePath = Path.Combine(appFolder, "config.json");
    }

    public async Task<AppConfig> LoadConfigAsync()
    {
        try
        {
            if (!File.Exists(_configFilePath))
            {
                _logger.LogInformation("Config file not found, creating default configuration");
                var defaultConfig = new AppConfig
                {
                    Omsi2Path = await DetectOmsi2PathAsync(),
                    LastUpdated = DateTime.UtcNow,
                    AppVersion = "1.0.0"
                };
                await SaveConfigAsync(defaultConfig);
                return defaultConfig;
            }

            var json = await File.ReadAllTextAsync(_configFilePath);
            
            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("Config file is empty, creating default configuration");
                var defaultConfig = new AppConfig
                {
                    Omsi2Path = await DetectOmsi2PathAsync(),
                    LastUpdated = DateTime.UtcNow,
                    AppVersion = "1.0.0"
                };
                await SaveConfigAsync(defaultConfig);
                return defaultConfig;
            }

            var config = JsonSerializer.Deserialize<AppConfig>(json);
            
            if (config == null)
            {
                _logger.LogError("Failed to deserialize config file");
                throw new InvalidOperationException("Configuration data is corrupted");
            }

            _cachedConfig = config;
            _logger.LogInformation("Configuration loaded from {Path}", _configFilePath);
            
            // Validate the OMSI 2 path
            if (!string.IsNullOrEmpty(config.Omsi2Path))
            {
                var validation = _pathDetector.ValidatePath(config.Omsi2Path);
                if (!validation.IsValid)
                {
                    _logger.LogWarning("Configured OMSI 2 path is no longer valid: {Path}. Reason: {Reason}", 
                        config.Omsi2Path, validation.Message);
                }
            }

            return _cachedConfig;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Config file contains invalid JSON");
            throw new InvalidOperationException("Configuration file is corrupted", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load configuration");
            throw;
        }
    }

    public async Task SaveConfigAsync(AppConfig config)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        try
        {
            // Validate OMSI 2 path if provided
            if (!string.IsNullOrEmpty(config.Omsi2Path))
            {
                var validation = _pathDetector.ValidatePath(config.Omsi2Path);
                if (!validation.IsValid)
                {
                    _logger.LogWarning("Saving config with invalid OMSI 2 path: {Path}. Reason: {Reason}", 
                        config.Omsi2Path, validation.Message);
                }
            }

            var updatedConfig = config with { LastUpdated = DateTime.UtcNow };
            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(updatedConfig, options);
            
            // Write to temp file first, then move (atomic operation)
            var tempPath = _configFilePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json);
            File.Move(tempPath, _configFilePath, overwrite: true);
            
            _cachedConfig = updatedConfig;
            _logger.LogInformation("Configuration saved to {Path}", _configFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save configuration");
            throw;
        }
    }

    public AppConfig GetCurrentConfig()
    {
        return _cachedConfig ?? new AppConfig();
    }

    public async Task<string?> DetectOmsi2PathAsync()
    {
        _logger.LogInformation("Attempting to auto-detect OMSI 2 installation path");

        var result = await _pathDetector.DetectPathAsync();
        
        if (result.IsValid && result.DetectedPath != null)
        {
            _logger.LogInformation("Auto-detected OMSI 2 at {Path} (method: {Method})", 
                result.DetectedPath, result.DetectionMethod);
            return result.DetectedPath;
        }

        _logger.LogWarning("Could not auto-detect OMSI 2 installation: {Reason}", result.Message);
        return null;
    }

    public bool ValidateOmsi2Path(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var result = _pathDetector.ValidatePath(path);
        
        if (!result.IsValid)
        {
            _logger.LogDebug("OMSI 2 path validation failed for {Path}: {Reason}", path, result.Message);
        }

        return result.IsValid;
    }
}
