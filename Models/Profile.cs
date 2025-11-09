using System;
using System.Collections.Generic;

namespace OMSIProfileManager.Models;

/// <summary>
/// Represents an OMSI 2 addon profile with associated vehicles and maps.
/// </summary>
public sealed record Profile
{
    /// <summary>
    /// Unique identifier for the profile (e.g., "prof_20251109_001").
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Display name of the profile (max 50 characters).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Optional description (max 200 characters).
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// List of vehicle folder names to enable when this profile is loaded.
    /// </summary>
    public required List<string> Vehicles { get; init; }

    /// <summary>
    /// List of map folder names to enable when this profile is loaded.
    /// </summary>
    public required List<string> Maps { get; init; }

    /// <summary>
    /// When the profile was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// When the profile was last used/loaded.
    /// </summary>
    public DateTime? LastUsed { get; init; }

    /// <summary>
    /// Optional tags for filtering/categorization.
    /// </summary>
    public List<string>? Tags { get; init; }

    /// <summary>
    /// Whether the profile is active/enabled.
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Validates profile data constraints.
    /// </summary>
    public bool IsValid(out string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            errorMessage = "Profile name is required.";
            return false;
        }

        if (Name.Length > 50)
        {
            errorMessage = "Profile name must be 50 characters or less.";
            return false;
        }

        if (Description?.Length > 200)
        {
            errorMessage = "Description must be 200 characters or less.";
            return false;
        }

        if (Vehicles.Count == 0 && Maps.Count == 0)
        {
            errorMessage = "Profile must have at least one vehicle or map.";
            return false;
        }

        errorMessage = null;
        return true;
    }
}
