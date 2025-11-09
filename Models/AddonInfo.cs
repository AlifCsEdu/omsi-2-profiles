using System;

namespace OMSIProfileManager.Models;

/// <summary>
/// Represents metadata about a vehicle or map addon in OMSI 2.
/// </summary>
public sealed record AddonInfo
{
    /// <summary>
    /// Folder name of the addon (e.g., "MAN_Lion_NL").
    /// </summary>
    public required string FolderName { get; init; }

    /// <summary>
    /// Type of addon (Vehicle or Map).
    /// </summary>
    public required AddonType Type { get; init; }

    /// <summary>
    /// Whether the addon is currently enabled (not ending with .disabled).
    /// </summary>
    public required bool IsEnabled { get; init; }

    /// <summary>
    /// Full path to the addon folder.
    /// </summary>
    public required string FullPath { get; init; }

    /// <summary>
    /// When the folder was last modified (for sorting by recent use).
    /// </summary>
    public DateTime? LastModified { get; init; }

    /// <summary>
    /// Display name extracted from addon metadata (if available).
    /// Falls back to folder name if metadata unavailable.
    /// </summary>
    public string DisplayName => FolderName;
}

/// <summary>
/// Type of OMSI 2 addon.
/// </summary>
public enum AddonType
{
    Vehicle,
    Map
}
