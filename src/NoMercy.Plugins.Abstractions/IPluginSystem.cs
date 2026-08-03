// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2024 NoMercy Entertainment

namespace NoMercy.Plugins.Abstractions;

/// <summary>
/// Everything a plugin can reach that it does not own.
///
/// One way in, rather than an interface per subsystem. A bespoke interface for
/// each capability means every new one is a change to the plugin contract, a
/// rebuild for every plugin, and a version of the host that older plugins cannot
/// talk to. Naming the capability instead means a host can grow one without the
/// contract moving at all, and a plugin can ask whether something exists before
/// depending on it.
///
/// The pattern is always the same and always indirect: a plugin describes what
/// it wants, the host decides whether it may, and whoever owns the thing carries
/// it out. A plugin never receives the player, the cast session or the library —
/// it receives an answer. That is what lets the owner change, or be a different
/// device entirely, without any plugin noticing.
/// </summary>
public interface IPluginSystem
{
    /// <summary>
    /// Whether this host offers a capability at all. Distinct from being allowed
    /// to use it: a plugin should be able to hide a feature the host cannot do
    /// rather than offer it and fail.
    /// </summary>
    bool Offers(string capability);

    /// <summary>
    /// Ask a capability to do something.
    ///
    /// Refused rather than thrown when the grant is missing, because a plugin
    /// being told no is an ordinary outcome it should handle, not an error.
    /// </summary>
    Task<PluginSystemResult> InvokeAsync(
        string capability,
        string command,
        IReadOnlyDictionary<string, object?>? arguments = null,
        CancellationToken ct = default);

    /// <summary>Ask a capability about its current state.</summary>
    Task<PluginSystemResult> QueryAsync(
        string capability,
        string question,
        CancellationToken ct = default);
}

/// <summary>
/// The capabilities a host can offer. A name here is a promise about meaning,
/// not about which subsystem answers it.
/// </summary>
public static class PluginCapability
{
    /// <summary>Playback: what is playing, and driving it.</summary>
    public const string Player = "player";

    /// <summary>Where playback is going, and moving it.</summary>
    public const string Cast = "cast";

    /// <summary>Fetching media for offline use.</summary>
    public const string Downloads = "downloads";

    /// <summary>Telling a viewer something.</summary>
    public const string Notifications = "notifications";

    /// <summary>Scanning, matching and organising.</summary>
    public const string Library = "library";

    /// <summary>Work that runs on a schedule.</summary>
    public const string Tasks = "tasks";

    public static readonly string[] All = [Player, Cast, Downloads, Notifications, Library, Tasks];

    public static bool IsKnown(string? capability)
    {
        return capability is not null && Array.IndexOf(All, capability) >= 0;
    }

    /// <summary>
    /// The grant that gates a capability.
    ///
    /// Derived rather than listed, so a new capability cannot be added and left
    /// ungated by someone who forgot the second list.
    /// </summary>
    public static string GrantFor(string capability)
    {
        return $"capability.{capability}";
    }
}

/// <summary>
/// What came back.
/// </summary>
public class PluginSystemResult
{
    public required bool Ok { get; init; }

    /// <summary>Set when the plugin was not allowed, rather than when it failed.</summary>
    public bool Refused { get; init; }

    /// <summary>Set when the host does not offer this capability at all.</summary>
    public bool Unsupported { get; init; }

    public string? Reason { get; init; }

    public IReadOnlyDictionary<string, object?> Data { get; init; } =
        new Dictionary<string, object?>();

    public static PluginSystemResult Done(IReadOnlyDictionary<string, object?>? data = null)
    {
        return new() { Ok = true, Data = data ?? new Dictionary<string, object?>() };
    }

    public static PluginSystemResult NotAllowed(string capability)
    {
        return new()
        {
            Ok = false,
            Refused = true,
            Reason = $"no grant for '{PluginCapability.GrantFor(capability)}'"
        };
    }

    public static PluginSystemResult NotOffered(string capability)
    {
        return new()
        {
            Ok = false,
            Unsupported = true,
            Reason = $"this host offers no '{capability}'"
        };
    }

    public static PluginSystemResult Failed(string reason)
    {
        return new() { Ok = false, Reason = reason };
    }
}
