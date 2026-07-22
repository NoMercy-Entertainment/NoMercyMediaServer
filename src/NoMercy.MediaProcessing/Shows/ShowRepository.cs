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
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.TvShows;
using NoMercy.MediaProcessing.Common;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.MediaProcessing.Shows;

public class ShowRepository(MediaContext context) : IShowRepository
{
    public async Task AddAsync(Tv tv)
    {
        await context
            .Tvs.Upsert(entity: tv)
            .On(match: v => new { v.Id })
            .WhenMatched(
                updater: (ts, ti) =>
                    new()
                    {
                        Id = ti.Id,
                        Backdrop = ti.Backdrop,
                        Duration = ti.Duration,
                        FirstAirDate = ti.FirstAirDate,
                        Homepage = ti.Homepage,
                        ImdbId = ti.ImdbId,
                        InProduction = ti.InProduction,
                        LastEpisodeToAir = ti.LastEpisodeToAir,
                        NextEpisodeToAir = ti.NextEpisodeToAir,
                        NumberOfEpisodes = ti.NumberOfEpisodes,
                        NumberOfSeasons = ti.NumberOfSeasons,
                        OriginCountry = ti.OriginCountry,
                        OriginalLanguage = ti.OriginalLanguage,
                        Overview = ti.Overview,
                        Popularity = ti.Popularity,
                        Poster = ti.Poster,
                        SpokenLanguages = ti.SpokenLanguages,
                        Status = ti.Status,
                        Tagline = ti.Tagline,
                        Title = ti.Title,
                        TitleSort = ti.TitleSort,
                        Trailer = ti.Trailer,
                        TvdbId = ti.TvdbId,
                        Type = ti.Type,
                        VoteAverage = ti.VoteAverage,
                        VoteCount = ti.VoteCount,
                        Folder = ti.Folder,
                        LibraryId = ti.LibraryId,
                        MediaType = ti.MediaType,
                    }
            )
            .RunAsync();

        await context
            .Tvs.Where(predicate: t => t.Id == tv.Id)
            .ExecuteUpdateAsync(setPropertyCalls: s => s.SetProperty(propertyExpression: t => t.CreatedAt, valueExpression: t => tv.CreatedAt));

        await context.SaveChangesAsync();

        // Link any existing recommendation/similar rows that reference this show as their target
        await context
            .Recommendations.Where(predicate: r => r.MediaId == tv.Id && r.TvToId == null)
            .ExecuteUpdateAsync(setPropertyCalls: s => s.SetProperty(propertyExpression: r => r.TvToId, valueExpression: tv.Id));

        await context
            .Similar.Where(predicate: r => r.MediaId == tv.Id && r.TvToId == null)
            .ExecuteUpdateAsync(setPropertyCalls: s => s.SetProperty(propertyExpression: r => r.TvToId, valueExpression: tv.Id));
    }

    public async Task Remove(int id)
    {
        // SQLite schema uses DeleteBehavior.Restrict globally. Disable FK
        // enforcement on this pinned connection so the show and all its
        // dependents are removed atomically, mirroring
        // Data.Repositories.TvShowRepository.DeleteAsync.
        bool ownsConnection =
            context.Database.GetDbConnection().State != System.Data.ConnectionState.Open;
        if (ownsConnection)
            await context.Database.OpenConnectionAsync();

        try
        {
            await context.Database.ExecuteSqlRawAsync(sql: "PRAGMA foreign_keys = OFF");
            try
            {
                await context.Tvs.Where(predicate: tv => tv.Id == id).ExecuteDeleteAsync();
            }
            finally
            {
                await context.Database.ExecuteSqlRawAsync(sql: "PRAGMA foreign_keys = ON");
            }
        }
        finally
        {
            if (ownsConnection)
                await context.Database.CloseConnectionAsync();
        }
    }

    public Task LinkToLibrary(Library library, Tv tv)
    {
        return context
            .LibraryTv.Upsert(entity: new(libraryId: library.Id, tvId: tv.Id))
            .On(match: v => new { v.LibraryId, v.TvId })
            .WhenMatched(updater: (lts, lti) => new() { LibraryId = lti.LibraryId, TvId = lti.TvId })
            .RunAsync();
    }

    public Task StoreAlternativeTitles(IEnumerable<AlternativeTitle> alternativeTitles)
    {
        return context
            .AlternativeTitles.UpsertRange(entities: alternativeTitles.ToArray())
            .On(match: a => new { a.Title, a.TvId })
            .WhenMatched(
                updater: (ats, ati) =>
                    new()
                    {
                        Title = ati.Title,
                        Iso31661 = ati.Iso31661,
                        TvId = ati.TvId,
                    }
            )
            .RunAsync();
    }

    public Task StoreTranslations(IEnumerable<Translation> translations)
    {
        return context
            .Translations.UpsertRange(entities: translations.ToArray())
            .On(match: t => new
            {
                t.Iso31661,
                t.Iso6391,
                t.TvId,
            })
            .WhenMatched(
                updater: (ts, ti) =>
                    new()
                    {
                        Iso31661 = ti.Iso31661,
                        Iso6391 = ti.Iso6391,
                        Name = ti.Name,
                        EnglishName = ti.EnglishName,
                        Title = ti.Title,
                        Overview = ti.Overview,
                        Homepage = ti.Homepage,
                        Biography = ti.Biography,
                        TvId = ti.TvId,
                        SeasonId = ti.SeasonId,
                        EpisodeId = ti.EpisodeId,
                        MovieId = ti.MovieId,
                        CollectionId = ti.CollectionId,
                        PersonId = ti.PersonId,
                    }
            )
            .RunAsync();
    }

    public IEnumerable<CertificationTv> GetCertificationTvs(
        TmdbTvShowAppends tv,
        IEnumerable<CertificationCriteria> certificationCriteria
    )
    {
        return context
            .Certifications.AsEnumerable()
            .Where(predicate: c =>
                certificationCriteria.Any(predicate: cc =>
                    cc.Iso31661 == c.Iso31661 && cc.Certification == c.Rating
                )
            )
            .Select(selector: c => new CertificationTv { CertificationId = c.Id, TvId = tv.Id });
    }

    public Task StoreContentRatings(IEnumerable<CertificationTv> certifications)
    {
        return context
            .CertificationTv.UpsertRange(entities: certifications.ToArray())
            .On(match: v => new { v.CertificationId, v.TvId })
            .WhenMatched(updater: (ts, ti) => new() { CertificationId = ti.CertificationId, TvId = ti.TvId })
            .RunAsync();
    }

    public Task StoreSimilar(IEnumerable<Similar> similar)
    {
        return context
            .Similar.UpsertRange(entities: similar.ToArray())
            .On(match: v => new { v.MediaId, v.TvFromId })
            .WhenMatched(
                updater: (ts, ti) =>
                    new()
                    {
                        TvToId = ti.TvToId,
                        TvFromId = ti.TvFromId,
                        Overview = ti.Overview,
                        Title = ti.Title,
                        TitleSort = ti.TitleSort,
                        Backdrop = ti.Backdrop,
                        Poster = ti.Poster,
                        MediaId = ti.MediaId,
                    }
            )
            .RunAsync();
    }

    public Task StoreRecommendations(IEnumerable<Recommendation> recommendations)
    {
        return context
            .Recommendations.UpsertRange(entities: recommendations.ToArray())
            .On(match: v => new { v.MediaId, v.TvFromId })
            .WhenMatched(
                updater: (ts, ti) =>
                    new()
                    {
                        TvToId = ti.TvToId,
                        TvFromId = ti.TvFromId,
                        Overview = ti.Overview,
                        Title = ti.Title,
                        TitleSort = ti.TitleSort,
                        Backdrop = ti.Backdrop,
                        Poster = ti.Poster,
                        MediaId = ti.MediaId,
                    }
            )
            .RunAsync();
    }

    public Task StoreVideos(IEnumerable<Media> videos)
    {
        return context
            .Medias.UpsertRange(entities: videos.ToArray())
            .On(match: v => new { v.Src, v.TvId })
            .WhenMatched(
                updater: (ts, ti) =>
                    new()
                    {
                        Src = ti.Src,
                        Iso6391 = ti.Iso6391,
                        Type = ti.Type,
                        TvId = ti.TvId,
                        Name = ti.Name,
                        Site = ti.Site,
                        Size = ti.Size,
                    }
            )
            .RunAsync();
    }

    public Task StoreImages(IEnumerable<Image> images)
    {
        return context
            .Images.UpsertRange(entities: images.ToArray())
            .On(match: v => new { v.FilePath, v.TvId })
            .WhenMatched(
                updater: (ts, ti) =>
                    new()
                    {
                        AspectRatio = ti.AspectRatio,
                        FilePath = ti.FilePath,
                        Height = ti.Height,
                        Iso6391 = ti.Iso6391,
                        Site = ti.Site,
                        VoteAverage = ti.VoteAverage,
                        VoteCount = ti.VoteCount,
                        Width = ti.Width,
                        Type = ti.Type,
                        TvId = ti.TvId,
                    }
            )
            .RunAsync();
    }

    public Task StoreKeywords(IEnumerable<Keyword> keywords)
    {
        return context
            .Keywords.UpsertRange(entities: keywords.ToArray())
            .On(match: v => new { v.Id })
            .WhenMatched(updater: (ts, ti) => new() { Id = ti.Id, Name = ti.Name })
            .RunAsync();
    }

    public Task LinkKeywordsToTv(IEnumerable<KeywordTv> keywordTvs)
    {
        return context
            .KeywordTv.UpsertRange(entities: keywordTvs.ToArray())
            .On(match: v => new { v.KeywordId, v.TvId })
            .WhenMatched(updater: (ts, ti) => new() { KeywordId = ti.KeywordId, TvId = ti.TvId })
            .RunAsync();
    }

    public Task StoreGenres(IEnumerable<GenreTv> genreTvs)
    {
        return context
            .GenreTv.UpsertRange(entities: genreTvs.ToArray())
            .On(match: v => new { v.GenreId, v.TvId })
            .WhenMatched(updater: (ts, ti) => new() { GenreId = ti.GenreId, TvId = ti.TvId })
            .RunAsync();
    }

    public async Task StoreNetworks(IEnumerable<Network> networks)
    {
        await context
            .Networks.UpsertRange(entities: networks)
            .On(match: n => n.Id)
            .WhenMatched(updater: n =>
                new()
                {
                    Name = n.Name,
                    Logo = n.Logo,
                    OriginCountry = n.OriginCountry,
                    Description = n.Description,
                    Headquarters = n.Headquarters,
                    Homepage = n.Homepage,
                }
            )
            .RunAsync();
    }

    public async Task StoreNetworkTvs(IEnumerable<NetworkTv> networkTvs)
    {
        await context
            .NetworkTv.UpsertRange(entities: networkTvs)
            .On(match: nt => new { nt.NetworkId, nt.TvId })
            .WhenMatched(updater: nt => new() { NetworkId = nt.NetworkId, TvId = nt.TvId })
            .RunAsync();
    }

    public Task StoreCompanies(List<Company> companies)
    {
        return context
            .Companies.UpsertRange(entities: companies)
            .On(match: v => new { v.Id })
            .WhenMatched(
                updater: (ts, ti) =>
                    new()
                    {
                        Id = ti.Id,
                        Name = ti.Name,
                        Description = ti.Description,
                        Headquarters = ti.Headquarters,
                        Homepage = ti.Homepage,
                        Logo = ti.Logo,
                        OriginCountry = ti.OriginCountry,
                        ParentCompany = ti.ParentCompany,
                    }
            )
            .RunAsync();
    }

    public Task StoreCompanyTvs(List<CompanyTv> companyTvs)
    {
        return context
            .CompanyTv.UpsertRange(entities: companyTvs.ToArray())
            .On(match: v => new { v.CompanyId, v.TvId })
            .WhenMatched(updater: (ts, ti) => new() { CompanyId = ti.CompanyId, TvId = ti.TvId })
            .RunAsync();
    }

    public Task StoreWatchProviders(List<WatchProvider> watchProviders)
    {
        return context
            .WatchProviders.UpsertRange(entities: watchProviders)
            .On(match: v => new { v.Id })
            .WhenMatched(
                updater: (ts, ti) =>
                    new()
                    {
                        Id = ti.Id,
                        Name = ti.Name,
                        Logo = ti.Logo,
                        DisplayPriority = ti.DisplayPriority,
                    }
            )
            .RunAsync();
    }

    public Task StoreWatchProviderMedias(List<WatchProviderMedia> watchProviderMedias)
    {
        return context
            .WatchProviderMedia.UpsertRange(entities: watchProviderMedias.ToArray())
            .On(match: v => new
            {
                v.WatchProviderId,
                v.CountryCode,
                v.ProviderType,
                v.MovieId,
                v.TvId,
            })
            .WhenMatched(
                updater: (ts, ti) =>
                    new()
                    {
                        WatchProviderId = ti.WatchProviderId,
                        TvId = ti.TvId,
                        ProviderType = ti.ProviderType,
                        CountryCode = ti.CountryCode,
                    }
            )
            .RunAsync();
    }
}
