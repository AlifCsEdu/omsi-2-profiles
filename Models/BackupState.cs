using System;
using System.Collections.Generic;

namespace OMSIProfileManager.Models;

/// <summary>
/// Represents a snapshot of the current OMSI 2 addon state for backup purposes.
/// </summary>
public sealed record BackupState
{
    /// <summary>
    /// Unique identifier for this backup.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// When this backup was created.
    /// </summary>
    public required DateTime CreatedAt { get; init; }

    /// <summary>
    /// List of vehicle folder names that were enabled at the time of backup.
    /// </summary>
    public required List<string> EnabledVehicles { get; init; }

    /// <summary>
    /// List of map folder names that were enabled at the time of backup.
    /// </summary>
    public required List<string> EnabledMaps { get; init; }

    /// <summary>
    /// Optional description of what triggered this backup.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// ID of the profile that was loaded (if this backup was created before profile load).
    /// </summary>
    public string? ProfileId { get; init; }

    /// <summary>
    /// Name of the profile that was loaded (stored for display purposes).
    /// </summary>
    public string? ProfileName { get; init; }

    /// <summary>
    /// Creates a display-friendly description of this backup.
    /// </summary>
    public string GetDisplayText()
    {
        var timeString = CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
        var addonCount = $"{EnabledVehicles.Count} vehicles, {EnabledMaps.Count} maps";
        
        if (!string.IsNullOrWhiteSpace(ProfileName))
        {
            return $"{timeString} - Before loading '{ProfileName}' ({addonCount})";
        }

        return $"{timeString} - {Description ?? "Manual backup"} ({addonCount})";
    }
}
