using System;
using System.Collections.Generic;

namespace OMSIProfileManager.Models;

/// <summary>
/// Container for all profiles stored in profiles.json.
/// </summary>
public sealed record ProfileCollection
{
    /// <summary>
    /// Version of the profile data format.
    /// </summary>
    public string Version { get; init; } = "1.0";

    /// <summary>
    /// When the profile collection was last updated.
    /// </summary>
    public DateTime LastUpdated { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// List of all profiles.
    /// </summary>
    public List<Profile> Profiles { get; init; } = new();
}
