using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;
using NoMercy.MediaProcessing.Common;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.Other;
using NoMercy.Providers.TMDB.Models.TV;

namespace NoMercy.MediaProcessing.Shows;

public class ShowRepository(MediaContext context) : IShowRepository
{
    public async Task AddAsync(Tv tv)
    {
        await context.Tvs.Upsert(tv)
            .On(v => new { v.Id })
            .WhenMatched((ts, ti) => new()
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
            })
            .RunAsync();
        
        await context.Tvs.Where(t => t.Id == tv.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.CreatedAt, t => tv.CreatedAt));

        await context.SaveChangesAsync();
    }

    public Task LinkToLibrary(Library library, Tv tv)
    {
        return context.LibraryTv.Upsert(new(library.Id, tv.Id))
            .On(v => new { v.LibraryId, v.TvId })
            .WhenMatched((lts, lti) => new()
            {
                LibraryId = lti.LibraryId,
                TvId = lti.TvId
            })
            .RunAsync();
    }

    public Task StoreAlternativeTitles(IEnumerable<AlternativeTitle> alternativeTitles)
    {
        return context.AlternativeTitles.UpsertRange(alternativeTitles.ToArray())
            .On(a => new { a.Title, a.TvId })
            .WhenMatched((ats, ati) => new()
            {
                Title = ati.Title,
                Iso31661 = ati.Iso31661,
                TvId = ati.TvId
            })
            .RunAsync();
    }

    public Task StoreTranslations(IEnumerable<Translation> translations)
    {
        return context.Translations.UpsertRange(translations.ToArray())
            .On(t => new { t.Iso31661, t.Iso6391, t.TvId })
            .WhenMatched((ts, ti) => new()
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
                PersonId = ti.PersonId
            })
            .RunAsync();
    }

    public IEnumerable<CertificationTv> GetCertificationTvs(TmdbTvShowAppends tv,
        IEnumerable<CertificationCriteria> certificationCriteria)
    {
        return context.Certifications
            .AsEnumerable()
            .Where(c => certificationCriteria
                .Any(cc => cc.Iso31661 == c.Iso31661 && cc.Certification == c.Rating))
            .Select(c => new CertificationTv
            {
                CertificationId = c.Id,
                TvId = tv.Id
            });
    }

    public Task StoreContentRatings(IEnumerable<CertificationTv> certifications)
    {
        return context.CertificationTv.UpsertRange(certifications.ToArray())
            .On(v => new { v.CertificationId, v.TvId })
            .WhenMatched((ts, ti) => new()
            {
                CertificationId = ti.CertificationId,
                TvId = ti.TvId
            })
            .RunAsync();
    }

    public Task StoreSimilar(IEnumerable<Similar> similar)
    {
        return context.Similar.UpsertRange(similar.ToArray())
            .On(v => new { v.MediaId, v.TvFromId })
            .WhenMatched((ts, ti) => new()
            {
                TvToId = ti.TvToId,
                TvFromId = ti.TvFromId,
                Overview = ti.Overview,
                Title = ti.Title,
                TitleSort = ti.TitleSort,
                Backdrop = ti.Backdrop,
                Poster = ti.Poster,
                MediaId = ti.MediaId
            })
            .RunAsync();
    }

    public Task StoreRecommendations(IEnumerable<Recommendation> recommendations)
    {
        return context.Recommendations.UpsertRange(recommendations.ToArray())
            .On(v => new { v.MediaId, v.TvFromId })
            .WhenMatched((ts, ti) => new()
            {
                TvToId = ti.TvToId,
                TvFromId = ti.TvFromId,
                Overview = ti.Overview,
                Title = ti.Title,
                TitleSort = ti.TitleSort,
                Backdrop = ti.Backdrop,
                Poster = ti.Poster,
                MediaId = ti.MediaId
            })
            .RunAsync();
    }

    public Task StoreVideos(IEnumerable<Media> videos)
    {
        return context.Medias.UpsertRange(videos.ToArray())
            .On(v => new { v.Src, v.TvId })
            .WhenMatched((ts, ti) => new()
            {
                Src = ti.Src,
                Iso6391 = ti.Iso6391,
                Type = ti.Type,
                TvId = ti.TvId,
                Name = ti.Name,
                Site = ti.Site,
                Size = ti.Size
            })
            .RunAsync();
    }

    public Task StoreImages(IEnumerable<Image> images)
    {
        return context.Images.UpsertRange(images.ToArray())
            .On(v => new { v.FilePath, v.TvId })
            .WhenMatched((ts, ti) => new()
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
                TvId = ti.TvId
            })
            .RunAsync();
    }

    public Task StoreKeywords(IEnumerable<Keyword> keywords)
    {
        return context.Keywords.UpsertRange(keywords.ToArray())
            .On(v => new { v.Id })
            .WhenMatched((ts, ti) => new()
            {
                Id = ti.Id,
                Name = ti.Name
            })
            .RunAsync();
    }

    public Task LinkKeywordsToTv(IEnumerable<KeywordTv> keywordTvs)
    {
        return context.KeywordTv.UpsertRange(keywordTvs.ToArray())
            .On(v => new { v.KeywordId, v.TvId })
            .WhenMatched((ts, ti) => new()
            {
                KeywordId = ti.KeywordId,
                TvId = ti.TvId
            })
            .RunAsync();
    }

    public Task StoreGenres(IEnumerable<GenreTv> genreTvs)
    {
        return context.GenreTv.UpsertRange(genreTvs.ToArray())
            .On(v => new { v.GenreId, v.TvId })
            .WhenMatched((ts, ti) => new()
            {
                GenreId = ti.GenreId,
                TvId = ti.TvId
            })
            .RunAsync();
    }
    
    public async Task StoreNetworks(IEnumerable<Network> networks)
    {
        await context.Networks.UpsertRange(networks)
            .On(n => n.Id)
            .WhenMatched(n => new()
            {
                Name = n.Name,
                Logo = n.Logo,
                OriginCountry = n.OriginCountry,
                Description = n.Description,
                Headquarters = n.Headquarters,
                Homepage = n.Homepage
            })
            .RunAsync();
    }

    public async Task StoreNetworkTvs(IEnumerable<NetworkTv> networkTvs)
    {
        await context.NetworkTv.UpsertRange(networkTvs)
            .On(nt => new { nt.NetworkId, nt.TvId })
            .WhenMatched(nt => new()
            {
                NetworkId = nt.NetworkId,
                TvId = nt.TvId
            })
            .RunAsync();
    }

    public Task StoreCompanies(List<Company> companies)
    {
        return context.Companies.UpsertRange(companies)
            .On(v => new { v.Id })
            .WhenMatched((ts, ti) => new()
            {
                Id = ti.Id,
                Name = ti.Name,
                Description = ti.Description,
                Headquarters = ti.Headquarters,
                Homepage = ti.Homepage,
                Logo = ti.Logo,
                OriginCountry = ti.OriginCountry,
                ParentCompany = ti.ParentCompany
            })
            .RunAsync();
    }

    public Task StoreCompanyTvs(List<CompanyTv> companyTvs)
    {
        return context.CompanyTv.UpsertRange(companyTvs.ToArray())
            .On(v => new { v.CompanyId, v.TvId })
            .WhenMatched((ts, ti) => new()
            {
                CompanyId = ti.CompanyId,
                TvId = ti.TvId
            })
            .RunAsync();
    }

    public string GetMediaType(TmdbTvShowAppends show)
    {
        bool isAnime = KitsuIo.IsAnime(show.Name, show.FirstAirDate.ParseYear()).Result;

        return isAnime ? "anime" : "tv";
    }
    
    public Task StoreWatchProviders(List<WatchProvider> watchProviders)
    {
        return context.WatchProviders.UpsertRange(watchProviders)
            .On(v => new { v.Id })
            .WhenMatched((ts, ti) => new()
            {
                Id = ti.Id,
                Name = ti.Name,
                Logo = ti.Logo,
                DisplayPriority = ti.DisplayPriority
            })
            .RunAsync();
    }

    public Task StoreWatchProviderMedias(List<WatchProviderMedia> watchProviderMedias)
    {
        return context.WatchProviderMedia.UpsertRange(watchProviderMedias.ToArray())
            .On(v => new { v.WatchProviderId, v.CountryCode, v.ProviderType, v.MovieId, v.TvId })
            .WhenMatched((ts, ti) => new()
            {
                WatchProviderId = ti.WatchProviderId,
                TvId = ti.TvId,
                ProviderType = ti.ProviderType,
                CountryCode = ti.CountryCode,
            })
            .RunAsync();
    }
}