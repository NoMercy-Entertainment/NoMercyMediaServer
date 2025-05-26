using Microsoft.EntityFrameworkCore;
using NoMercy.Api.Controllers.V1.Media;
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Helpers;
using NoMercy.NmSystem;
using NoMercy.Data.Repositories;

namespace NoMercy.Api.Services;

public class HomeService
{
    private readonly MediaContext _mediaContext;
    private readonly LibraryRepository _libraryRepository;

    public HomeService(
        LibraryRepository libraryRepository, MediaContext mediaContext)
    {
        _libraryRepository = libraryRepository;
        _mediaContext = mediaContext;
    }

    public async Task<Render> GetHomePageContent(Guid userId, string language, string country, PageRequestDto request)
    {
        IEnumerable<UserData> continueWatching = Queries
            .GetContinueWatching(_mediaContext, userId, language, country);
    
        List<GenreRowDto<GenreRowItemDto>> genres = [];
    
        List<int> movieIds = [];
        List<int> tvIds = [];
    
        HashSet<Genre> genreItems = Queries
            .GetHome(_mediaContext, userId, language, 300);
    
        foreach (Genre genre in genreItems)
        {
            IEnumerable<HomeSourceDto> movies = genre
                .GenreMovies.Select(movie => new HomeSourceDto(movie.MovieId, "movie"));
    
            IEnumerable<HomeSourceDto> tvs = genre
                .GenreTvShows.Select(tv => new HomeSourceDto(tv.TvId, "tv"));
    
            string name = genre.Translations.FirstOrDefault()?.Name ?? genre.Name;
            GenreRowDto<GenreRowItemDto> genreRowDto = new()
            {
                Title = name,
                MoreLink = new($"/genre/{genre.Id}", UriKind.Relative),
                Id = genre.Id.ToString(),
                Source = movies
                    .Concat(tvs)
                    .Randomize()
                    .Take(28)
            };
    
            tvIds.AddRange(genreRowDto.Source
                .Where(source => source.MediaType == "tv")
                .Select(source => source.Id));
    
            movieIds.AddRange(genreRowDto.Source
                .Where(source => source.MediaType == "movie")
                .Select(source => source.Id));
    
            genres.Add(genreRowDto);
        }
    
        List<Tv> tvData = [];
        await foreach (Tv tv in Queries.GetHomeTvs(_mediaContext, tvIds, language))
        {
            tvData.Add(tv);
        }
    
        List<Movie> movieData = [];
        await foreach (Movie movie in Queries.GetHomeMovies(_mediaContext, movieIds, language))
        {
            movieData.Add(movie);
        }
    
        foreach (GenreRowDto<GenreRowItemDto> genre in genres)
            genre.Items = genre.Source
                .Select(source =>
                {
                    switch (source.MediaType)
                    {
                        case "tv":
                        {
                            Tv? tv = tvData.FirstOrDefault(tv => tv.Id == source.Id);
                            return tv?.Id == null
                                ? null
                                : new GenreRowItemDto(tv, language);
                        }
                        case "movie":
                        {
                            Movie? movie = movieData.FirstOrDefault(movie => movie.Id == source.Id);
                            return movie?.Id == null
                                ? null
                                : new GenreRowItemDto(movie, language);
                        }
                        default:
                        {
                            return null;
                        }
                    }
                })
                .Where(genreRow => genreRow != null);
    
        genres = genres.Where(genre => genre.Items.Any()).ToList();
    
        GenreRowItemDto? homeCardItem = genres.Where(g => g.Title != String.Empty)
            .Randomize().FirstOrDefault()
            ?.Items.Randomize().FirstOrDefault() ?? genres.Where(g => g.Title != String.Empty)
            .Randomize().FirstOrDefault()
            ?.Items.Randomize().FirstOrDefault() ?? genres.Where(g => g.Title != String.Empty)
            .Randomize().FirstOrDefault()
            ?.Items.Randomize().FirstOrDefault() ?? genres.Where(g => g.Title != String.Empty)
            .Randomize().FirstOrDefault()
            ?.Items.Randomize().FirstOrDefault();
        
        IQueryable<Library> libraries = _libraryRepository.GetLibraries(userId);
        List<GenreRowDto<dynamic>> list = [];
    
        foreach (Library library in libraries)
        {
            IEnumerable<Movie> movies = _libraryRepository.GetLibraryMovies(userId, library.Id, language, 10, 0, m => m.CreatedAt, "desc");
            IEnumerable<Tv> shows = _libraryRepository.GetLibraryShows(userId, library.Id, language, 10, 0, m => m.CreatedAt, "desc");
    
            list.Add(new()
            {
                Title = library.Title,
                MoreLink = new($"/libraries/{library.Id}", UriKind.Relative),
                Items = movies.Select(movie => new GenreRowItemDto(movie, country))
                    .Concat(shows.Select(tv => new GenreRowItemDto(tv, country)))
            });
        }
    
        return new()
        {
            Data =
            [
                new ComponentBuilder<GenreRowItemDto>()
                    .WithComponent("NMHomeCard")
                    .WithUpdate("pageLoad", "/home/card")
                    .WithProps(props => props
                        .WithNextId("continue")
                        .WithPreviousId("")
                        .WithData(homeCardItem))
                    .Build(),
    
                new ComponentBuilder<ContinueWatchingItemDto>()
                    .WithComponent("NMCarousel")
                    .WithUpdate("pageLoad", "/home/continue")
                    .WithProps(props => props
                        .WithNextId(genres.ElementAtOrDefault(0)?.Id ?? "continue")
                        .WithPreviousId("continue")
                        .WithTitle("Continue watching".Localize())
                        .WithMoreLink(null)
                        .WithItems(GetContinueWatchingItems(continueWatching, country)))
                    .Build(),
    
                ..list.Select(genre => new ComponentBuilder<GenreRowItemDto>()
                    .WithComponent("NMCarousel")
                    .WithProps(props => props
                        .WithId(genre.Id)
                        .WithNextId(list.ElementAtOrDefault(list.IndexOf(genre) + 1)?.Id ?? "continue")
                        .WithPreviousId(list.ElementAtOrDefault(list.IndexOf(genre) - 1)?.Id ?? "continue")
                        .WithTitle("Latest in " + genre.Title)
                        .WithMoreLink(genre.MoreLink)
                        .WithItems(
                            genre.Items.Select(item =>
                                new ComponentBuilder<GenreRowItemDto>()
                                    .WithComponent("NMCard")
                                    .WithProps(cardProps => cardProps
                                        .WithData(item ?? new GenreRowItemDto())
                                        .WithWatch())
                                    .Build())))
                    .Build()),
                
                ..genres.Select(genre => new ComponentBuilder<GenreRowItemDto>()
                    .WithComponent("NMCarousel")
                    .WithProps(props => props
                        .WithId(genre.Id)
                        .WithNextId(genres.ElementAtOrDefault(genres.IndexOf(genre) + 1)?.Id ?? "continue")
                        .WithPreviousId(genres.ElementAtOrDefault(genres.IndexOf(genre) - 1)?.Id ?? "continue")
                        .WithTitle(genre.Title)
                        .WithMoreLink(genre.MoreLink)
                        .WithItems(
                            genre.Items.Select(item =>
                                new ComponentBuilder<GenreRowItemDto>()
                                    .WithComponent("NMCard")
                                    .WithProps(cardProps => cardProps
                                        .WithData(item ?? new GenreRowItemDto())
                                        .WithWatch())
                                    .Build())))
                    .Build()),
            ]
        };
    }

    public async Task<Render> GetContinueWatchingContent(Guid userId, string language, string country)
    {
        IEnumerable<UserData> continueWatching = Queries
            .GetContinueWatching(_mediaContext, userId, language, country);

        List<GenreRowDto<GenreRowItemDto>> genres = [];

        List<int> movieIds = [];
        List<int> tvIds = [];

        HashSet<Genre> genreItems = Queries
            .GetHome(_mediaContext, userId, language, 300);

        foreach (Genre genre in genreItems)
        {
            IEnumerable<HomeSourceDto> movies = genre
                .GenreMovies.Select(movie => new HomeSourceDto(movie.MovieId, "movie"));

            IEnumerable<HomeSourceDto> tvs = genre
                .GenreTvShows.Select(tv => new HomeSourceDto(tv.TvId, "tv"));

            string name = genre.Translations.FirstOrDefault()?.Name ?? genre.Name;
            GenreRowDto<GenreRowItemDto> genreRowDto = new()
            {
                Title = name,
                MoreLink = new($"/genre/{genre.Id}", UriKind.Relative),
                Id = genre.Id.ToString(),
                Source = movies
                    .Concat(tvs)
                    .Randomize()
                    .Take(28)
            };

            tvIds.AddRange(genreRowDto.Source
                .Where(source => source.MediaType == "tv")
                .Select(source => source.Id));

            movieIds.AddRange(genreRowDto.Source
                .Where(source => source.MediaType == "movie")
                .Select(source => source.Id));

            genres.Add(genreRowDto);
        }

        List<Tv> tvData = [];
        await foreach (Tv tv in Queries.GetHomeTvs(_mediaContext, tvIds, language))
        {
            tvData.Add(tv);
        }

        List<Movie> movieData = [];
        await foreach (Movie movie in Queries.GetHomeMovies(_mediaContext, movieIds, language))
        {
            movieData.Add(movie);
        }

        foreach (GenreRowDto<GenreRowItemDto> genre in genres)
            genre.Items = genre.Source
                .Select(source =>
                {
                    switch (source.MediaType)
                    {
                        case "tv":
                        {
                            Tv? tv = tvData.FirstOrDefault(tv => tv.Id == source.Id);
                            return tv?.Id == null
                                ? null
                                : new GenreRowItemDto(tv, language);
                        }
                        case "movie":
                        {
                            Movie? movie = movieData.FirstOrDefault(movie => movie.Id == source.Id);
                            return movie?.Id == null
                                ? null
                                : new GenreRowItemDto(movie, language);
                        }
                        default:
                        {
                            return null;
                        }
                    }
                })
                .Where(genreRow => genreRow != null);

        genres = genres.Where(genre => genre.Items.Any()).ToList();

        GenreRowItemDto? homeCardItem = genres.Where(g => g.Title != String.Empty)
            .Randomize().FirstOrDefault()
            ?.Items.Randomize().FirstOrDefault() ?? genres.Where(g => g.Title != String.Empty)
            .Randomize().FirstOrDefault()
            ?.Items.Randomize().FirstOrDefault() ?? genres.Where(g => g.Title != String.Empty)
            .Randomize().FirstOrDefault()
            ?.Items.Randomize().FirstOrDefault() ?? genres.Where(g => g.Title != String.Empty)
            .Randomize().FirstOrDefault()
            ?.Items.Randomize().FirstOrDefault();

        IQueryable<Library> libraries = _libraryRepository.GetLibraries(userId);
        List<GenreRowDto<dynamic>> list = [];

        foreach (Library library in libraries)
        {
            IEnumerable<Movie> movies = _libraryRepository.GetLibraryMovies(userId, library.Id, language, 32, 0, m => m.CreatedAt, "desc");
            IEnumerable<Tv> shows = _libraryRepository.GetLibraryShows(userId, library.Id, language, 32, 0, m => m.CreatedAt, "desc");

            list.Add(new()
            {
                Title = library.Title,
                MoreLink = new($"/libraries/{library.Id}", UriKind.Relative),
                Items = movies.Select(movie => new GenreRowItemDto(movie, country))
                    .Concat(shows.Select(tv => new GenreRowItemDto(tv, country)))
            });
        }
        
        return new()
        {
            Data =
            [
                new ComponentBuilder<GenreRowItemDto>()
                    .WithComponent("NMHomeCard")
                    .WithUpdate("pageLoad", "/home/card")
                    .WithProps(props => props
                        .WithNextId("continue")
                        .WithPreviousId("")
                        .WithData(homeCardItem))
                    .Build(),

                new ComponentBuilder<ContinueWatchingItemDto>()
                    .WithComponent("NMCarousel")
                    .WithUpdate("pageLoad", "/home/continue")
                    .WithProps(props => props
                        .WithNextId(genres.ElementAtOrDefault(0)?.Id ?? "continue")
                        .WithPreviousId("continue")
                        .WithTitle("Continue watching".Localize())
                        .WithMoreLink(null)
                        .WithItems(GetContinueWatchingItems(continueWatching, country)))
                    .Build(),
                
                ..list.Select(genre => new ComponentBuilder<GenreRowItemDto>()
                    .WithComponent("NMCarousel")
                    .WithProps(props => props
                        .WithId(genre.Id)
                        .WithNextId(list.ElementAtOrDefault(list.IndexOf(genre) + 1)?.Id ?? "continue")
                        .WithPreviousId(list.ElementAtOrDefault(list.IndexOf(genre) - 1)?.Id ?? "continue")
                        .WithTitle("Latest in " + genre.Title)
                        .WithMoreLink(genre.MoreLink)
                        .WithItems(
                            genre.Items.Select(item =>
                                new ComponentBuilder<GenreRowItemDto>()
                                    .WithComponent("NMCard")
                                    .WithProps(cardProps => cardProps
                                        .WithData(item ?? new GenreRowItemDto())
                                        .WithWatch())
                                    .Build())))
                    .Build()),
                
                ..genres.Select(genre => new ComponentBuilder<GenreRowItemDto>()
                    .WithComponent("NMCarousel")
                    .WithProps(props => props
                        .WithId(genre.Id)
                        .WithNextId(genres.ElementAtOrDefault(genres.IndexOf(genre) + 1)?.Id ?? "continue")
                        .WithPreviousId(genres.ElementAtOrDefault(genres.IndexOf(genre) - 1)?.Id ?? "continue")
                        .WithTitle(genre.Title)
                        .WithMoreLink(genre.MoreLink)
                        .WithItems(
                            genre.Items.Select(item =>
                                new ComponentBuilder<GenreRowItemDto>()
                                    .WithComponent("NMCard")
                                    .WithProps(cardProps => cardProps
                                        .WithData(item ?? new GenreRowItemDto())
                                        .WithWatch())
                                    .Build())))
                    .Build())
            ]
        };
    }

    public async Task<Render> GetHomeCard(Guid userId, string language, Ulid replaceId)
    {
        Tv? tv = await _mediaContext.Tvs
            .AsNoTracking()
            .Where(tv =>
                tv.Library.LibraryUsers.Any(u => u.UserId.Equals(userId))
                && tv.Episodes.Any(episode => episode.SeasonNumber > 0 && episode.VideoFiles.Count != 0))
            .Include(tv => tv.Translations.Where(translation => translation.Iso6391 == language))
            .Include(tv => tv.Images.Where(image => image.Type == "logo" && image.Iso6391 == "en"))
            .Include(tv => tv.Media.Where(media => media.Site == "YouTube"))
            .Include(tv => tv.KeywordTvs).ThenInclude(keywordTv => keywordTv.Keyword)
            .Include(tv => tv.Episodes.Where(episode => episode.SeasonNumber > 0 && episode.VideoFiles.Count != 0))
            .ThenInclude(episode => episode.VideoFiles)
            .Include(tv => tv.CertificationTvs).ThenInclude(certificationTv => certificationTv.Certification)
            .OrderBy(tv => EF.Functions.Random())
            .FirstOrDefaultAsync();

        Movie? movie = await _mediaContext.Movies
            .AsNoTracking()
            .Where(movie => movie.Library.LibraryUsers.Any(u => u.UserId.Equals(userId))
                            && movie.VideoFiles.Count != 0)
            .Include(movie => movie.Translations.Where(translation => translation.Iso6391 == language))
            .Include(movie => movie.Images.Where(image => image.Type == "logo" && image.Iso6391 == "en"))
            .Include(movie => movie.Media.Where(media => media.Site == "YouTube"))
            .Include(movie => movie.KeywordMovies).ThenInclude(keywordMovie => keywordMovie.Keyword)
            .Include(movie => movie.CertificationMovies)
            .ThenInclude(certificationMovie => certificationMovie.Certification)
            .OrderBy(movie => EF.Functions.Random())
            .FirstOrDefaultAsync();

        List<GenreRowItemDto> genres = [];
        if (tv != null)
            genres.Add(new(tv, language));

        if (movie != null)
            genres.Add(new(movie, language));

        GenreRowItemDto? homeCardItem = genres.Where(g => !string.IsNullOrWhiteSpace(g.Title))
            .Randomize().FirstOrDefault();

        return new()
        {
            Data =
            [
                new ComponentBuilder<GenreRowItemDto>()
                    .WithComponent("NMHomeCard")
                    .WithUpdate("pageLoad", "/home/card")
                    .WithReplacing(replaceId)
                    .WithProps(props => props.WithData(homeCardItem))
                    .Build(),
            ]
        };
    }

    public async Task<ScreensaverDto> GetScreensaverContent(Guid userId)
    {
        HashSet<Image> data = await Queries.GetScreensaverImages(_mediaContext, userId);

        IEnumerable<Image> logos = data.Where(image => image.Type == "logo");

        IEnumerable<ScreensaverDataDto> tvCollection = data
            .Where(image => image is { TvId: not null, Type: "backdrop" })
            .DistinctBy(image => image.TvId)
            .Select(image => new ScreensaverDataDto(image, logos, "tv"));

        IEnumerable<ScreensaverDataDto> movieCollection = data
            .Where(image => image is { MovieId: not null, Type: "backdrop" })
            .DistinctBy(image => image.MovieId)
            .Select(image => new ScreensaverDataDto(image, logos, "movie"));

        return new()
        {
            Data = tvCollection
                .Concat(movieCollection)
                .Where(image => image.Meta?.Logo != null)
                .Randomize()
        };
    }

    public async Task<Render> GetHomeTvContent(Guid userId, string language, string country)
    {
        IEnumerable<UserData> continueWatching = Queries.GetContinueWatching(_mediaContext, userId, language, country);
        List<GenreRowDto<GenreRowItemDto>> genres = [];

        HashSet<Genre> genreItems = Queries.GetHome(_mediaContext, userId, language, 300);
        List<int> movieIds = [];
        List<int> tvIds = [];

        foreach (Genre genre in genreItems)
        {
            IEnumerable<HomeSourceDto> movies =
                genre.GenreMovies.Select(movie => new HomeSourceDto(movie.MovieId, "movie"));
            IEnumerable<HomeSourceDto> tvs = genre.GenreTvShows.Select(tv => new HomeSourceDto(tv.TvId, "tv"));

            string name = genre.Translations.FirstOrDefault()?.Name ?? genre.Name;
            GenreRowDto<GenreRowItemDto> genreRowDto = new GenreRowDto<GenreRowItemDto>
            {
                Title = name,
                MoreLink = new($"/genre/{genre.Id}", UriKind.Relative),
                Id = genre.Id.ToString(),
                Source = movies.Concat(tvs).Randomize().Take(28)
            };

            tvIds.AddRange(genreRowDto.Source.Where(s => s.MediaType == "tv").Select(s => s.Id));
            movieIds.AddRange(genreRowDto.Source.Where(s => s.MediaType == "movie").Select(s => s.Id));

            genres.Add(genreRowDto);
        }

        List<Tv> tvData = [];
        List<Movie> movieData = [];

        await foreach (Tv tv in Queries.GetHomeTvs(_mediaContext, tvIds, language))
            tvData.Add(tv);

        await foreach (Movie movie in Queries.GetHomeMovies(_mediaContext, movieIds, language))
            movieData.Add(movie);

        // Process genres and items
        foreach (GenreRowDto<GenreRowItemDto> genre in genres)
        {
            genre.Items = genre.Source
                .Select(source => source.MediaType switch
                {
                    "tv" => tvData.FirstOrDefault(t => t.Id == source.Id)?.Id == null
                        ? null
                        : new GenreRowItemDto(tvData.First(t => t.Id == source.Id), language),
                    "movie" => movieData.FirstOrDefault(m => m.Id == source.Id)?.Id == null
                        ? null
                        : new GenreRowItemDto(movieData.First(m => m.Id == source.Id), language),
                    _ => null
                })
                .Where(item => item != null);
        }

        genres = genres.Where(genre => genre.Items.Any()).ToList();
        GenreRowItemDto? homeCardItem = genres.Where(g => g.Title != string.Empty)
            .Randomize().FirstOrDefault()?.Items.Randomize().FirstOrDefault();

        IQueryable<Library> libraries = _libraryRepository.GetLibraries(userId);
        List<GenreRowDto<dynamic>> list = [];

        foreach (Library library in libraries)
        {
            IEnumerable<Movie> movies = _libraryRepository.GetLibraryMovies(userId, library.Id, language, 32, 0, m => m.CreatedAt, "desc");
            IEnumerable<Tv> shows = _libraryRepository.GetLibraryShows(userId, library.Id, language, 32, 0, m => m.CreatedAt, "desc");

            list.Add(new()
            {
                Title = library.Title,
                MoreLink = new($"/libraries/{library.Id}", UriKind.Relative),
                Items = movies.Select(movie => new GenreRowItemDto(movie, country))
                    .Concat(shows.Select(tv => new GenreRowItemDto(tv, country)))
            });
        }
        
        return new()
        {
            Data =
            [
                new ComponentBuilder<GenreRowItemDto>()
                    .WithComponent("NMHomeCard")
                    .WithUpdate("pageLoad", "/home/card")
                    .WithProps(props => props.WithData(homeCardItem))
                    .Build(),

                new ComponentBuilder<ContinueWatchingItemDto>()
                    .WithComponent("NMCarousel")
                    .WithUpdate("pageLoad", "/home/continue")
                    .WithProps(props => props
                        .WithNextId(genres.FirstOrDefault()?.Id ?? "continue")
                        .WithPreviousId("continue")
                        .WithTitle("Continue watching".Localize())
                        .WithMoreLink(null)
                        .WithItems(GetContinueWatchingItems(continueWatching, country)))
                    .Build(),

                ..list.Select(genre => new ComponentBuilder<GenreRowItemDto>()
                    .WithComponent("NMCarousel")
                    .WithProps(props => props
                        .WithId(genre.Id)
                        .WithNextId(list.ElementAtOrDefault(list.IndexOf(genre) + 1)?.Id ?? "continue")
                        .WithPreviousId(list.ElementAtOrDefault(list.IndexOf(genre) - 1)?.Id ?? "continue")
                        .WithTitle("Latest in " + genre.Title)
                        .WithMoreLink(genre.MoreLink)
                        .WithItems(
                            genre.Items.Select(item =>
                                new ComponentBuilder<GenreRowItemDto>()
                                    .WithComponent("NMCard")
                                    .WithProps(cardProps => cardProps
                                        .WithData(item ?? new GenreRowItemDto())
                                        .WithWatch())
                                    .Build())))
                    .Build()),
                
                ..genres.Select(genre => new ComponentBuilder<GenreRowItemDto>()
                    .WithComponent("NMCarousel")
                    .WithProps(props => props
                        .WithId(genre.Id)
                        .WithNextId(genres.ElementAtOrDefault(genres.IndexOf(genre) + 1)?.Id ?? "continue")
                        .WithPreviousId(genres.ElementAtOrDefault(genres.IndexOf(genre) - 1)?.Id ?? "continue")
                        .WithTitle(genre.Title)
                        .WithMoreLink(genre.MoreLink)
                        .WithItems(
                            genre.Items.Select(item =>
                                new ComponentBuilder<GenreRowItemDto>()
                                    .WithComponent("NMCard")
                                    .WithProps(cardProps => cardProps
                                        .WithData(item ?? new GenreRowItemDto())
                                        .WithWatch())
                                    .Build())))
                    .Build())
            ]
        };
    }

    public Task<Render> GetHomeContinueContent(Guid userId, string language, string country, Ulid replaceId)
    {
        IEnumerable<UserData> continueWatching = Queries.GetContinueWatching(_mediaContext, userId, language, country);

        return Task.FromResult(new Render
        {
            Data =
            [
                new ComponentBuilder<ContinueWatchingItemDto>()
                    .WithComponent("NMCarousel")
                    .WithUpdate("pageLoad", "/home/continue")
                    .WithProps(props => props
                        .WithNextId(28)
                        .WithPreviousId("continue")
                        .WithTitle("Continue watching".Localize())
                        .WithMoreLink(null)
                        .WithItems(GetContinueWatchingItems(continueWatching, country)))
                    .Build()
            ]
        });
    }

    private static IEnumerable<ComponentDto<ContinueWatchingItemDto>> GetContinueWatchingItems(
        IEnumerable<UserData> continueWatching,
        string country)
    {
        return continueWatching
            .Select(item => new ComponentDto<ContinueWatchingItemDto>
            {
                Component = "NMCard",
                Props =
                {
                    Data = new(item, country),
                    Watch = true,
                    ContextMenuItems =
                    [
                        new()
                        {
                            { "label", "Remove from watchlist".Localize() },
                            { "icon", "mooooom-trash" },
                            { "method", "DELETE" },
                            { "confirm", "Are you sure you want to remove this from continue watching?".Localize() },
                            {
                                "args", new Dictionary<string, object>
                                {
                                    { "url", new Uri($"/userdata/continue", UriKind.Relative) }
                                }
                            }
                        }
                    ]
                }
            })
            .DistinctBy(item => item.Props.Data?.Link);
    }
}