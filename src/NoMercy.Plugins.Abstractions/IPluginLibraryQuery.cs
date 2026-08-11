// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

namespace NoMercy.Plugins.Abstractions;

/// <summary>
/// Reading what the library already holds.
/// <para>
/// A plugin whose job depends on the library — what is missing, what is already
/// there, where it lives — had no sanctioned way to ask. The mechanical answer
/// was to share <c>NoMercy.Database</c> with plugins so they could resolve the
/// EF context, and that answer is wrong: it makes the EF model public plugin
/// ABI, so every migration becomes a potential plugin break. Self-hosted users
/// do not get broken, and that includes by their plugins.
/// </para>
/// <para>
/// So the contract returns types owned by this assembly. The EF model stays
/// free to change, and this interface is the thing that has to stay still.
/// Read-only by construction, which is why it needs no capability: nothing here
/// can alter a user's library. Writing is
/// <see cref="PluginGrantKind.LibraryWrite"/> and a different contract.
/// </para>
/// </summary>
public interface IPluginLibraryQuery
{
    /// <summary>Every library the server has, of every type.</summary>
    Task<IReadOnlyList<PluginLibrary>> GetLibrariesAsync(CancellationToken ct = default);

    /// <summary>Shows, optionally narrowed to one library.</summary>
    Task<IReadOnlyList<PluginLibraryShow>> GetShowsAsync(
        string? libraryId = null,
        CancellationToken ct = default
    );

    /// <summary>Movies, optionally narrowed to one library.</summary>
    Task<IReadOnlyList<PluginLibraryMovie>> GetMoviesAsync(
        string? libraryId = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Every episode of a show, including the ones with no file. A plugin
    /// computing what is missing needs the gaps, so an episode with no file is
    /// returned with <see cref="PluginLibraryEpisode.HasFile"/> false rather
    /// than omitted.
    /// </summary>
    Task<IReadOnlyList<PluginLibraryEpisode>> GetEpisodesAsync(
        int showId,
        CancellationToken ct = default
    );

    /// <summary>The files held against one show.</summary>
    Task<IReadOnlyList<PluginLibraryFile>> GetShowFilesAsync(
        int showId,
        CancellationToken ct = default
    );
}

/// <param name="Id">The library's id, as used by <see cref="PluginGrantKind.LibraryWrite"/>.</param>
/// <param name="Type">movie, tv, anime or music.</param>
public record PluginLibrary(string Id, string Title, string Type);

/// <param name="Id">The provider's show id, which is what the rest of this contract keys on.</param>
/// <param name="Folder">The show's folder relative to its library root, or null when it has none.</param>
public record PluginLibraryShow(
    int Id,
    string Title,
    int? Year,
    string LibraryId,
    string? Folder,
    int EpisodeCount,
    int HaveEpisodeCount
)
{
    /// <summary>
    /// Whether the show is still going out.
    /// <para>
    /// Not a positional parameter, deliberately. Everything above was in the contract
    /// before this was, and adding a parameter to a record's primary constructor changes
    /// that constructor's signature — which a host built against the new contract and a
    /// plugin compiled against the old one would then disagree about. An init property
    /// with a default is additive: old callers keep compiling, old readers keep working,
    /// and a host that cannot answer leaves it
    /// <see cref="PluginShowStatus.Unknown"/>.
    /// </para>
    /// <para>
    /// The library knows this and a plugin cannot work it out. Deriving it from air dates
    /// — "something aired lately, so it must still be running" — reads a series cancelled
    /// last month as current and a show on a nine-month hiatus as finished. Both are
    /// wrong in the direction that costs somebody bandwidth.
    /// </para>
    /// </summary>
    public PluginShowStatus Status { get; init; } = PluginShowStatus.Unknown;
}

/// <summary>
/// Where a show is in its life, as the library understands it.
/// <para>
/// A closed set owned by this assembly rather than the metadata provider's own wording,
/// for the same reason every other type here is: the provider is free to rename
/// "Returning Series" tomorrow, and when it does that is a change in the host's mapping
/// and not a silent behaviour change in every installed plugin.
/// </para>
/// </summary>
public enum PluginShowStatus
{
    /// <summary>
    /// The library has no status for it, or one this contract does not recognise.
    /// <para>
    /// Distinct from <see cref="Ended"/> on purpose. A plugin deciding what to do about a
    /// show should treat "I do not know" as "carry on": leaving a running series alone
    /// because its metadata is thin is a failure nobody can see, and the opposite mistake
    /// costs one wasted search.
    /// </para>
    /// </summary>
    Unknown = 0,

    /// <summary>Announced, nothing shot yet.</summary>
    Planned = 1,

    /// <summary>Being made, and has not started going out.</summary>
    InProduction = 2,

    /// <summary>One episode exists and nothing has been ordered beyond it.</summary>
    Pilot = 3,

    /// <summary>Going out, or between seasons with more to come.</summary>
    Returning = 4,

    /// <summary>Finished. Everything that will ever exist of it exists.</summary>
    Ended = 5,

    /// <summary>Stopped before it finished. Same consequence as <see cref="Ended"/>, different reason, and an owner wants to be told which.</summary>
    Canceled = 6,
}

public record PluginLibraryMovie(
    int Id,
    string Title,
    int? Year,
    string LibraryId,
    string? Folder,
    bool HasFile
);

/// <param name="HasFile">Whether the library holds a file for this episode. False is the gap a plugin is looking for.</param>
public record PluginLibraryEpisode(
    int ShowId,
    int SeasonNumber,
    int EpisodeNumber,
    string? Title,
    DateTime? AirDate,
    bool HasFile
);

/// <param name="Path">Full path, as the server records it.</param>
/// <param name="Quality">The server's own quality label for the file, or empty when it has none.</param>
public record PluginLibraryFile(
    int ShowId,
    int? SeasonNumber,
    int? EpisodeNumber,
    string Path,
    string Quality
);
