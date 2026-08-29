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

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NoMercy.Database;

namespace NoMercy.Data.Repositories;

/// <summary>
/// Removing a title and everything the server hung off it.
///
/// <para>
/// Deleting a show used to turn foreign-key enforcement off and delete the
/// parent row alone. Restrict was refusing that delete because dependents
/// existed, which is the constraint doing its job; switching enforcement off did
/// not make the delete complete, it made it silent. One show left 723 rows
/// behind, every one naming a parent that no longer existed, and the library
/// went on counting a show that was gone.
/// </para>
///
/// <para>
/// So the dependents are deleted here, deepest first, in one transaction, with
/// enforcement left on. An incomplete delete fails loudly rather than succeeding
/// quietly.
/// </para>
///
/// <para>
/// Not derived from the model graph, deliberately. Casts, Crews, Images, Medias,
/// Translations and AlternativeTitles are shared across shows, movies and
/// people, and the graph loops back out through them: Roles reach Casts, and a
/// walk that followed every edge from one show would delete cast rows belonging
/// to other titles. Every statement below is therefore keyed on this title's own
/// ids. <c>MediaSubtreeDeleteCoverageTests</c> fails if a table gains a foreign
/// key into one of these aggregates and is not named here, which is the part a
/// reader cannot be expected to notice.
/// </para>
/// </summary>
public static class MediaSubtreeDelete
{
    /// <summary>A show, its seasons, its episodes and everything under them.</summary>
    public static async Task ShowAsync(MediaContext context, int id, CancellationToken ct = default)
    {
        await InTransactionAsync(
            context,
            async () =>
            {
                IQueryable<int> seasons = context
                    .Seasons.Where(season => season.TvId == id)
                    .Select(season => season.Id);

                IQueryable<int> episodes = context
                    .Episodes.Where(episode => episode.TvId == id)
                    .Select(episode => episode.Id);

                IQueryable<Ulid> videoFiles = context
                    .VideoFiles.Where(file =>
                        file.EpisodeId != null && episodes.Contains(file.EpisodeId.Value)
                    )
                    .Select(file => file.Id);

                IQueryable<int> guestStars = context
                    .GuestStars.Where(star => episodes.Contains(star.EpisodeId))
                    .Select(star => star.Id);

                IQueryable<int> roles = context
                    .Roles.Where(role =>
                        role.GuestStarId != null && guestStars.Contains(role.GuestStarId.Value)
                    )
                    .Select(role => role.Id);

                IQueryable<int> specialItems = context
                    .SpecialItems.Where(item =>
                        item.EpisodeId != null && episodes.Contains(item.EpisodeId.Value)
                    )
                    .Select(item => item.Id);

                IQueryable<int> casts = context
                    .Casts.Where(cast =>
                        cast.TvId == id
                        || (cast.SeasonId != null && seasons.Contains(cast.SeasonId.Value))
                        || (cast.EpisodeId != null && episodes.Contains(cast.EpisodeId.Value))
                        || (cast.RoleId != null && roles.Contains(cast.RoleId.Value))
                    )
                    .Select(cast => cast.Id);

                IQueryable<int> crews = context
                    .Crews.Where(crew =>
                        crew.TvId == id
                        || (crew.SeasonId != null && seasons.Contains(crew.SeasonId.Value))
                        || (crew.EpisodeId != null && episodes.Contains(crew.EpisodeId.Value))
                    )
                    .Select(crew => crew.Id);

                // Artwork hangs off the cast and crew rows as well as off the
                // title, so it goes before either of them.
                await context
                    .Images.Where(image =>
                        image.TvId == id
                        || (image.SeasonId != null && seasons.Contains(image.SeasonId.Value))
                        || (image.EpisodeId != null && episodes.Contains(image.EpisodeId.Value))
                        || (image.CastId != null && casts.Contains(image.CastId.Value))
                        || (image.CrewId != null && crews.Contains(image.CrewId.Value))
                    )
                    .ExecuteDeleteAsync(ct);

                await context
                    .UserData.Where(data =>
                        data.TvId == id
                        || videoFiles.Contains(data.VideoFileId)
                        // A shadow foreign key: the model declares the
                        // relationship without a property to read it from, and a
                        // row left pointing at a deleted special item is exactly
                        // the orphan this is here to prevent.
                        || specialItems.Contains(EF.Property<int?>(data, "SpecialItemId") ?? 0)
                    )
                    .ExecuteDeleteAsync(ct);

                await context
                    .Medias.Where(media =>
                        media.TvId == id
                        || (media.SeasonId != null && seasons.Contains(media.SeasonId.Value))
                        || (media.EpisodeId != null && episodes.Contains(media.EpisodeId.Value))
                        || (
                            media.VideoFileId != null
                            && videoFiles.Contains(media.VideoFileId.Value)
                        )
                    )
                    .ExecuteDeleteAsync(ct);

                await context.Casts.Where(cast => casts.Contains(cast.Id)).ExecuteDeleteAsync(ct);
                await context.Roles.Where(role => roles.Contains(role.Id)).ExecuteDeleteAsync(ct);
                await context
                    .GuestStars.Where(star => guestStars.Contains(star.Id))
                    .ExecuteDeleteAsync(ct);
                await context.Crews.Where(crew => crews.Contains(crew.Id)).ExecuteDeleteAsync(ct);

                await context
                    .VideoFiles.Where(file => videoFiles.Contains(file.Id))
                    .ExecuteDeleteAsync(ct);
                await context
                    .SpecialItems.Where(item => specialItems.Contains(item.Id))
                    .ExecuteDeleteAsync(ct);

                await context
                    .ContentSegments.Where(segment =>
                        segment.EpisodeId != null && episodes.Contains(segment.EpisodeId.Value)
                    )
                    .ExecuteDeleteAsync(ct);

                await context
                    .PlaylistItems.Where(item =>
                        item.TvId == id
                        || (item.EpisodeId != null && episodes.Contains(item.EpisodeId.Value))
                    )
                    .ExecuteDeleteAsync(ct);

                await context
                    .Translations.Where(translation =>
                        translation.TvId == id
                        || (
                            translation.SeasonId != null
                            && seasons.Contains(translation.SeasonId.Value)
                        )
                        || (
                            translation.EpisodeId != null
                            && episodes.Contains(translation.EpisodeId.Value)
                        )
                    )
                    .ExecuteDeleteAsync(ct);

                await context.Episodes.Where(episode => episode.TvId == id).ExecuteDeleteAsync(ct);
                await context.Seasons.Where(season => season.TvId == id).ExecuteDeleteAsync(ct);

                await context
                    .AlternativeTitles.Where(title => title.TvId == id)
                    .ExecuteDeleteAsync(ct);
                await context
                    .AnimeDemographicTv.Where(row => row.TvId == id)
                    .ExecuteDeleteAsync(ct);
                await context.AnimeSeasonTv.Where(row => row.TvId == id).ExecuteDeleteAsync(ct);
                await context.AnimeThemeTv.Where(row => row.TvId == id).ExecuteDeleteAsync(ct);
                await context.CertificationTv.Where(row => row.TvId == id).ExecuteDeleteAsync(ct);
                await context.CompanyTv.Where(row => row.TvId == id).ExecuteDeleteAsync(ct);
                await context.Creators.Where(creator => creator.TvId == id).ExecuteDeleteAsync(ct);
                await context.GenreTv.Where(row => row.TvId == id).ExecuteDeleteAsync(ct);
                await context.KeywordTv.Where(row => row.TvId == id).ExecuteDeleteAsync(ct);
                await context.LibraryTv.Where(row => row.TvId == id).ExecuteDeleteAsync(ct);
                await context.NetworkTv.Where(row => row.TvId == id).ExecuteDeleteAsync(ct);
                await context.TvUser.Where(row => row.TvId == id).ExecuteDeleteAsync(ct);
                await context
                    .WatchProviderMedia.Where(row => row.TvId == id)
                    .ExecuteDeleteAsync(ct);
                await context
                    .PlaybackPreferences.Where(preference => preference.TvId == id)
                    .ExecuteDeleteAsync(ct);

                await context
                    .Recommendations.Where(row => row.TvFromId == id || row.TvToId == id)
                    .ExecuteDeleteAsync(ct);
                await context
                    .Similar.Where(row => row.TvFromId == id || row.TvToId == id)
                    .ExecuteDeleteAsync(ct);

                await context.Tvs.Where(tv => tv.Id == id).ExecuteDeleteAsync(ct);
            },
            ct
        );
    }

    /// <summary>A movie and everything the server hung off it.</summary>
    public static async Task MovieAsync(
        MediaContext context,
        int id,
        CancellationToken ct = default
    )
    {
        await InTransactionAsync(
            context,
            async () =>
            {
                IQueryable<Ulid> videoFiles = context
                    .VideoFiles.Where(file => file.MovieId == id)
                    .Select(file => file.Id);

                IQueryable<int> specialItems = context
                    .SpecialItems.Where(item => item.MovieId == id)
                    .Select(item => item.Id);

                IQueryable<int> casts = context
                    .Casts.Where(cast => cast.MovieId == id)
                    .Select(cast => cast.Id);

                IQueryable<int> crews = context
                    .Crews.Where(crew => crew.MovieId == id)
                    .Select(crew => crew.Id);

                await context
                    .Images.Where(image =>
                        image.MovieId == id
                        || (image.CastId != null && casts.Contains(image.CastId.Value))
                        || (image.CrewId != null && crews.Contains(image.CrewId.Value))
                    )
                    .ExecuteDeleteAsync(ct);

                await context
                    .UserData.Where(data =>
                        data.MovieId == id
                        || videoFiles.Contains(data.VideoFileId)
                        // A shadow foreign key: the model declares the
                        // relationship without a property to read it from, and a
                        // row left pointing at a deleted special item is exactly
                        // the orphan this is here to prevent.
                        || specialItems.Contains(EF.Property<int?>(data, "SpecialItemId") ?? 0)
                    )
                    .ExecuteDeleteAsync(ct);

                await context
                    .Medias.Where(media =>
                        media.MovieId == id
                        || (
                            media.VideoFileId != null
                            && videoFiles.Contains(media.VideoFileId.Value)
                        )
                    )
                    .ExecuteDeleteAsync(ct);

                await context.Casts.Where(cast => casts.Contains(cast.Id)).ExecuteDeleteAsync(ct);
                await context.Crews.Where(crew => crews.Contains(crew.Id)).ExecuteDeleteAsync(ct);

                await context
                    .VideoFiles.Where(file => videoFiles.Contains(file.Id))
                    .ExecuteDeleteAsync(ct);
                await context
                    .SpecialItems.Where(item => specialItems.Contains(item.Id))
                    .ExecuteDeleteAsync(ct);

                await context
                    .ContentSegments.Where(segment => segment.MovieId == id)
                    .ExecuteDeleteAsync(ct);
                await context
                    .PlaylistItems.Where(item => item.MovieId == id)
                    .ExecuteDeleteAsync(ct);
                await context
                    .Translations.Where(translation => translation.MovieId == id)
                    .ExecuteDeleteAsync(ct);

                // A movie can carry seasons in this schema, and they are the
                // movie's own rather than a show's.
                await context
                    .Seasons.Where(season => EF.Property<int?>(season, "MovieId") == id)
                    .ExecuteDeleteAsync(ct);

                await context
                    .AlternativeTitles.Where(title => title.MovieId == id)
                    .ExecuteDeleteAsync(ct);
                await context
                    .AnimeDemographicMovie.Where(row => row.MovieId == id)
                    .ExecuteDeleteAsync(ct);
                await context
                    .AnimeSeasonMovie.Where(row => row.MovieId == id)
                    .ExecuteDeleteAsync(ct);
                await context
                    .AnimeThemeMovie.Where(row => row.MovieId == id)
                    .ExecuteDeleteAsync(ct);
                await context
                    .CertificationMovie.Where(row => row.MovieId == id)
                    .ExecuteDeleteAsync(ct);
                await context
                    .CollectionMovie.Where(row => row.MovieId == id)
                    .ExecuteDeleteAsync(ct);
                await context.CompanyMovie.Where(row => row.MovieId == id).ExecuteDeleteAsync(ct);
                await context.GenreMovie.Where(row => row.MovieId == id).ExecuteDeleteAsync(ct);
                await context.KeywordMovie.Where(row => row.MovieId == id).ExecuteDeleteAsync(ct);
                await context.LibraryMovie.Where(row => row.MovieId == id).ExecuteDeleteAsync(ct);
                await context.MovieUser.Where(row => row.MovieId == id).ExecuteDeleteAsync(ct);
                await context
                    .WatchProviderMedia.Where(row => row.MovieId == id)
                    .ExecuteDeleteAsync(ct);
                await context
                    .PlaybackPreferences.Where(preference => preference.MovieId == id)
                    .ExecuteDeleteAsync(ct);

                await context
                    .Recommendations.Where(row => row.MovieFromId == id || row.MovieToId == id)
                    .ExecuteDeleteAsync(ct);
                await context
                    .Similar.Where(row => row.MovieFromId == id || row.MovieToId == id)
                    .ExecuteDeleteAsync(ct);

                await context.Movies.Where(movie => movie.Id == id).ExecuteDeleteAsync(ct);
            },
            ct
        );
    }

    /// <summary>
    /// A collection and what belongs to it.
    ///
    /// <para>
    /// The movies in it are not its dependents: a collection row going away is
    /// not a reason to lose the films. Only the join rows and the collection's
    /// own metadata go.
    /// </para>
    /// </summary>
    public static async Task CollectionAsync(
        MediaContext context,
        int id,
        CancellationToken ct = default
    )
    {
        await InTransactionAsync(
            context,
            async () =>
            {
                await context
                    .Images.Where(image => image.CollectionId == id)
                    .ExecuteDeleteAsync(ct);
                await context
                    .UserData.Where(data => data.CollectionId == id)
                    .ExecuteDeleteAsync(ct);
                await context
                    .Translations.Where(translation => translation.CollectionId == id)
                    .ExecuteDeleteAsync(ct);
                await context
                    .PlaybackPreferences.Where(preference => preference.CollectionId == id)
                    .ExecuteDeleteAsync(ct);
                await context
                    .CollectionLibrary.Where(row => row.CollectionId == id)
                    .ExecuteDeleteAsync(ct);
                await context
                    .CollectionMovie.Where(row => row.CollectionId == id)
                    .ExecuteDeleteAsync(ct);
                await context
                    .CollectionUser.Where(row => row.CollectionId == id)
                    .ExecuteDeleteAsync(ct);

                await context
                    .Collections.Where(collection => collection.Id == id)
                    .ExecuteDeleteAsync(ct);
            },
            ct
        );
    }

    /// <summary>
    /// One transaction around the lot, with enforcement left on.
    ///
    /// <para>
    /// Half a delete is worse than none: the row the library counts would be
    /// gone while the rows it joins through remained. Either the whole subtree
    /// goes or nothing does.
    /// </para>
    /// </summary>
    private static async Task InTransactionAsync(
        MediaContext context,
        Func<Task> work,
        CancellationToken ct
    )
    {
        // A transaction the caller already opened stays the caller's to commit,
        // which is what lets a delete take part in a larger unit of work.
        if (context.Database.CurrentTransaction is not null)
        {
            await work();
            return;
        }

        await using IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(ct);

        await work();

        await transaction.CommitAsync(ct);
    }
}
