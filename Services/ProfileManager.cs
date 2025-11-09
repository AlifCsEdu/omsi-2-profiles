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
/// Implementation of profile manager service with enhanced validation and features.
/// </summary>
public class ProfileManager : IProfileManager
{
    private readonly ILogger<ProfileManager> _logger;
    private readonly string _profilesFilePath;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private readonly int _maxBackups = 10;

    public ProfileManager(ILogger<ProfileManager> logger)
    {
        _logger = logger;
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var appFolder = Path.Combine(appDataPath, "OMSIProfileManager");
        Directory.CreateDirectory(appFolder);
        _profilesFilePath = Path.Combine(appFolder, "profiles.json");
    }

    public async Task<List<Profile>> LoadProfilesAsync()
    {
        await _fileLock.WaitAsync();
        try
        {
            if (!File.Exists(_profilesFilePath))
            {
                _logger.LogInformation("Profiles file not found, creating empty collection");
                var emptyCollection = new ProfileCollection();
                await SaveProfileCollectionAsync(emptyCollection);
                return new List<Profile>();
            }

            var json = await File.ReadAllTextAsync(_profilesFilePath);
            
            if (string.IsNullOrWhiteSpace(json))
            {
                _logger.LogWarning("Profiles file is empty, initializing with empty collection");
                return new List<Profile>();
            }

            var collection = JsonSerializer.Deserialize<ProfileCollection>(json);
            
            if (collection == null)
            {
                _logger.LogError("Failed to deserialize profiles file, content may be corrupted");
                throw new InvalidOperationException("Profile data is corrupted");
            }

            _logger.LogInformation("Loaded {Count} profiles", collection.Profiles.Count);
            return collection.Profiles;
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Profile file contains invalid JSON, attempting recovery from backup");
            var recovered = await TryRecoverFromBackupAsync();
            if (recovered != null)
            {
                _logger.LogWarning("Successfully recovered {Count} profiles from backup", recovered.Count);
                return recovered;
            }
            throw new InvalidOperationException("Failed to load profiles and no valid backup found", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error loading profiles");
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SaveProfilesAsync(List<Profile> profiles)
    {
        await _fileLock.WaitAsync();
        try
        {
            // Validate all profiles before saving
            var invalidProfiles = profiles
                .Where(p => !p.IsValid(out _))
                .Select(p => p.Name)
                .ToList();

            if (invalidProfiles.Any())
            {
                throw new InvalidOperationException(
                    $"Cannot save profiles - the following profiles are invalid: {string.Join(", ", invalidProfiles)}");
            }

            var collection = new ProfileCollection
            {
                Profiles = profiles,
                LastUpdated = DateTime.UtcNow
            };
            
            await SaveProfileCollectionAsync(collection);
            _logger.LogInformation("Saved {Count} profiles", profiles.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save profiles");
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task<Profile> CreateProfileAsync(string name, string? description, List<string> vehicles, List<string> maps)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Profile name cannot be empty", nameof(name));
        }

        var profiles = await LoadProfilesAsync();

        // Check for duplicate names (case-insensitive)
        if (profiles.Any(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"A profile with the name '{name}' already exists");
        }

        var newProfile = new Profile
        {
            Id = GenerateProfileId(),
            Name = name,
            Description = description,
            Vehicles = vehicles ?? new List<string>(),
            Maps = maps ?? new List<string>(),
            CreatedAt = DateTime.UtcNow,
            Tags = new List<string>(),
            Enabled = true
        };

        if (!newProfile.IsValid(out var errorMessage))
        {
            throw new InvalidOperationException($"Cannot create profile: {errorMessage}");
        }

        profiles.Add(newProfile);
        await SaveProfilesAsync(profiles);
        _logger.LogInformation("Created new profile: {Name} (ID: {Id}, Vehicles: {VCount}, Maps: {MCount})", 
            name, newProfile.Id, vehicles.Count, maps.Count);
        return newProfile;
    }

    public async Task UpdateProfileAsync(Profile profile)
    {
        if (profile == null)
        {
            throw new ArgumentNullException(nameof(profile));
        }

        if (!profile.IsValid(out var errorMessage))
        {
            throw new InvalidOperationException($"Cannot update profile: {errorMessage}");
        }

        var profiles = await LoadProfilesAsync();
        var index = profiles.FindIndex(p => p.Id == profile.Id);

        if (index == -1)
        {
            throw new InvalidOperationException($"Profile with ID '{profile.Id}' not found");
        }

        // Check for duplicate names (excluding current profile)
        if (profiles.Any(p => p.Id != profile.Id && 
                             p.Name.Equals(profile.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"A profile with the name '{profile.Name}' already exists");
        }

        profiles[index] = profile;
        await SaveProfilesAsync(profiles);
        _logger.LogInformation("Updated profile: {Name} (ID: {Id})", profile.Name, profile.Id);
    }

    public async Task DeleteProfileAsync(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            throw new ArgumentException("Profile ID cannot be empty", nameof(profileId));
        }

        var profiles = await LoadProfilesAsync();
        var profileToRemove = profiles.FirstOrDefault(p => p.Id == profileId);

        if (profileToRemove == null)
        {
            throw new InvalidOperationException($"Profile with ID '{profileId}' not found");
        }

        profiles.Remove(profileToRemove);
        await SaveProfilesAsync(profiles);
        _logger.LogInformation("Deleted profile: {Name} (ID: {Id})", profileToRemove.Name, profileId);
    }

    public async Task<Profile?> GetProfileByIdAsync(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return null;
        }

        var profiles = await LoadProfilesAsync();
        return profiles.FirstOrDefault(p => p.Id == profileId);
    }

    public async Task<Profile> DuplicateProfileAsync(string profileId, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            throw new ArgumentException("New profile name cannot be empty", nameof(newName));
        }

        var existingProfile = await GetProfileByIdAsync(profileId);
        if (existingProfile == null)
        {
            throw new InvalidOperationException($"Profile with ID '{profileId}' not found");
        }

        var profiles = await LoadProfilesAsync();

        // Check for duplicate names
        if (profiles.Any(p => p.Name.Equals(newName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"A profile with the name '{newName}' already exists");
        }

        var duplicatedProfile = existingProfile with
        {
            Id = GenerateProfileId(),
            Name = newName,
            CreatedAt = DateTime.UtcNow,
            LastUsed = null
        };

        profiles.Add(duplicatedProfile);
        await SaveProfilesAsync(profiles);
        _logger.LogInformation("Duplicated profile: {OriginalName} -> {NewName} (ID: {Id})", 
            existingProfile.Name, newName, duplicatedProfile.Id);
        return duplicatedProfile;
    }

    private async Task SaveProfileCollectionAsync(ProfileCollection collection)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(collection, options);
        
        // Write to temporary file first, then replace (atomic operation)
        var tempPath = _profilesFilePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json);
        File.Move(tempPath, _profilesFilePath, overwrite: true);

        // Create backup
        await CreateBackupAsync(json);
    }

    private async Task CreateBackupAsync(string json)
    {
        try
        {
            var backupFolder = Path.Combine(Path.GetDirectoryName(_profilesFilePath)!, "backups");
            Directory.CreateDirectory(backupFolder);
            
            var backupPath = Path.Combine(backupFolder, $"profiles.json.{DateTime.UtcNow:yyyyMMddHHmmss}");
            await File.WriteAllTextAsync(backupPath, json);

            // Clean up old backups
            var backupFiles = Directory.GetFiles(backupFolder, "profiles.json.*")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .Skip(_maxBackups)
                .ToList();

            foreach (var oldBackup in backupFiles)
            {
                try
                {
                    oldBackup.Delete();
                    _logger.LogDebug("Deleted old backup: {FileName}", oldBackup.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete old backup: {FileName}", oldBackup.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create profile backup (non-critical)");
        }
    }

    private async Task<List<Profile>?> TryRecoverFromBackupAsync()
    {
        try
        {
            var backupFolder = Path.Combine(Path.GetDirectoryName(_profilesFilePath)!, "backups");
            if (!Directory.Exists(backupFolder))
                return null;

            var backupFiles = Directory.GetFiles(backupFolder, "profiles.json.*")
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.CreationTime)
                .ToList();

            foreach (var backupFile in backupFiles)
            {
                try
                {
                    _logger.LogInformation("Attempting to recover from backup: {FileName}", backupFile.Name);
                    var json = await File.ReadAllTextAsync(backupFile.FullName);
                    var collection = JsonSerializer.Deserialize<ProfileCollection>(json);
                    
                    if (collection?.Profiles != null)
                    {
                        // Restore the backup as the current file
                        File.Copy(backupFile.FullName, _profilesFilePath, overwrite: true);
                        return collection.Profiles;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Backup file {FileName} is also corrupted, trying next", backupFile.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to recover from backup");
        }

        return null;
    }

    private string GenerateProfileId()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");
        var randomSuffix = Guid.NewGuid().ToString("N")[..6];
        return $"prof_{timestamp}_{randomSuffix}";
    }
}
