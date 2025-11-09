using OMSIProfileManager.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace OMSIProfileManager.Services;

/// <summary>
/// Service for managing OMSI 2 profiles with full CRUD operations.
/// Handles creation, reading, updating, and deletion of profiles stored in JSON format.
/// All operations are thread-safe and include validation.
/// </summary>
public interface IProfileManager
{
    /// <summary>
    /// Loads all profiles from disk storage.
    /// </summary>
    /// <returns>A list of all saved profiles. Returns empty list if no profiles exist.</returns>
    /// <exception cref="System.IO.IOException">Thrown when file cannot be read.</exception>
    Task<List<Profile>> LoadProfilesAsync();

    /// <summary>
    /// Saves all profiles to disk storage atomically.
    /// Uses atomic write operation (write to temp file, then move) to prevent corruption.
    /// </summary>
    /// <param name="profiles">The complete list of profiles to save.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when profiles is null.</exception>
    /// <exception cref="System.IO.IOException">Thrown when file cannot be written.</exception>
    Task SaveProfilesAsync(List<Profile> profiles);

    /// <summary>
    /// Creates a new profile with the specified settings.
    /// Automatically generates a unique ID and sets creation timestamp.
    /// Validates that profile name is unique (case-insensitive).
    /// </summary>
    /// <param name="name">The profile name. Must be unique and not empty.</param>
    /// <param name="description">Optional description of the profile.</param>
    /// <param name="vehicles">List of vehicle folder names to include in this profile.</param>
    /// <param name="maps">List of map folder names to include in this profile.</param>
    /// <returns>The newly created profile with generated ID.</returns>
    /// <exception cref="System.ArgumentException">Thrown when name is invalid or already exists.</exception>
    Task<Profile> CreateProfileAsync(string name, string? description, List<string> vehicles, List<string> maps);

    /// <summary>
    /// Updates an existing profile with new values.
    /// Automatically updates the LastModified timestamp.
    /// Validates that new name doesn't conflict with other profiles.
    /// </summary>
    /// <param name="profile">The profile with updated values. Must have valid ID.</param>
    /// <exception cref="System.ArgumentNullException">Thrown when profile is null.</exception>
    /// <exception cref="System.ArgumentException">Thrown when profile ID doesn't exist or name conflicts.</exception>
    Task UpdateProfileAsync(Profile profile);

    /// <summary>
    /// Permanently deletes a profile by its unique identifier.
    /// This operation cannot be undone.
    /// </summary>
    /// <param name="profileId">The unique identifier of the profile to delete.</param>
    /// <exception cref="System.ArgumentException">Thrown when profile ID is not found.</exception>
    Task DeleteProfileAsync(string profileId);

    /// <summary>
    /// Retrieves a single profile by its unique identifier.
    /// </summary>
    /// <param name="profileId">The unique identifier of the profile to retrieve.</param>
    /// <returns>The profile if found, null otherwise.</returns>
    Task<Profile?> GetProfileByIdAsync(string profileId);

    /// <summary>
    /// Creates a duplicate of an existing profile with a new name.
    /// All addon selections are copied. A new unique ID is generated.
    /// </summary>
    /// <param name="profileId">The ID of the profile to duplicate.</param>
    /// <param name="newName">The name for the duplicated profile. Must be unique.</param>
    /// <returns>The newly created duplicate profile.</returns>
    /// <exception cref="System.ArgumentException">Thrown when source profile not found or new name conflicts.</exception>
    Task<Profile> DuplicateProfileAsync(string profileId, string newName);
}
