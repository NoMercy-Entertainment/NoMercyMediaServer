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
);

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
