using NoMercy.Database.Models;
using NoMercy.MediaProcessing.Common;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.MediaProcessing.Jobs.PaletteJobs;
using NoMercy.NmSystem;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Episode;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.People;
using NoMercy.Providers.TMDB.Models.Season;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;
using Serilog.Events;
using TmdbGender = NoMercy.Database.Models.TmdbGender;

namespace NoMercy.MediaProcessing.People;

public class PersonManager(
    IPersonRepository personRepository,
    JobDispatcher jobDispatcher
) : BaseManager, IPersonManager
{
    public async Task Store(TmdbTvShowAppends show)
    {
        (List<int> peopleIds, List<Cast> casts, List<Crew> crews, List<Role> roles, List<Job> jobs) = CollectPeople(show);

        List<TmdbPersonAppends> peopleAppends = await FetchPeopleByIds(peopleIds);

        IEnumerable<Person> people = peopleAppends
            .Select(person => new Person
            {
                Id = person.Id,
                Adult = person.Adult,
                AlsoKnownAs = person.AlsoKnownAs.Length > 0 ? person.AlsoKnownAs.ToJson() : null,
                Biography = person.Biography,
                BirthDay = person.BirthDay,
                DeathDay = person.DeathDay,
                TmdbGender = (TmdbGender)person.TmdbGender,
                _externalIds = person.ExternalIds.ToJson(),
                Homepage = person.Homepage?.ToString(),
                ImdbId = person.ImdbId,
                KnownForDepartment = person.KnownForDepartment,
                Name = person.Name,
                PlaceOfBirth = person.PlaceOfBirth,
                Popularity = person.Popularity,
                Profile = person.ProfilePath,
                TitleSort = person.Name
            });

        await personRepository.Store(people);
        Logger.MovieDb($"Show {show.Name}: People stored");

        await personRepository.StoreRoles(roles);
        Logger.MovieDb($"Show {show.Name}: Roles stored", LogEventLevel.Debug);

        await personRepository.StoreJobs(jobs);
        Logger.MovieDb($"Show {show.Name}: Jobs stored", LogEventLevel.Debug);

        List<int> ids = personRepository.GetIds();
        
        await personRepository.StoreCast(casts.Where(c => ids.Contains(c.PersonId)), Type.TvShow);
        Logger.MovieDb($"Show {show.Name}: Cast stored", LogEventLevel.Debug);
        
        await personRepository.StoreCrew(crews.Where(c => ids.Contains(c.PersonId)), Type.TvShow);
        Logger.MovieDb($"Show {show.Name}: Crew stored", LogEventLevel.Debug);

        jobDispatcher.DispatchJob<AddPersonExtraDataJob, TmdbPersonAppends>(peopleAppends, show.Name);
    }
    
    public async Task Store(TmdbSeasonAppends season)
    {
        (List<int> peopleIds, List<Cast> casts, List<Crew> crews, List<Role> roles, List<Job> jobs) = CollectPeople(season);

        List<TmdbPersonAppends> peopleAppends = await FetchPeopleByIds(peopleIds);

        IEnumerable<Person> people = peopleAppends
            .Select(person => new Person
            {
                Id = person.Id,
                Adult = person.Adult,
                AlsoKnownAs = person.AlsoKnownAs.Length > 0 ? person.AlsoKnownAs.ToJson() : null,
                Biography = person.Biography,
                BirthDay = person.BirthDay,
                DeathDay = person.DeathDay,
                TmdbGender = (TmdbGender)person.TmdbGender,
                _externalIds = person.ExternalIds.ToJson(),
                Homepage = person.Homepage?.ToString(),
                ImdbId = person.ImdbId,
                KnownForDepartment = person.KnownForDepartment,
                Name = person.Name,
                PlaceOfBirth = person.PlaceOfBirth,
                Popularity = person.Popularity,
                Profile = person.ProfilePath,
                TitleSort = person.Name
            });

        await personRepository.Store(people);
        Logger.MovieDb($"Show {season.Name}; Season {season.SeasonNumber}: People stored");

        await personRepository.StoreRoles(roles);
        Logger.MovieDb($"Show {season.Name}; Season {season.SeasonNumber}: Roles stored", LogEventLevel.Debug);

        await personRepository.StoreJobs(jobs);
        Logger.MovieDb($"Show {season.Name}; Season {season.SeasonNumber}: Jobs stored", LogEventLevel.Debug);

        List<int> ids = personRepository.GetIds();
        
        await personRepository.StoreCast(casts.Where(c => ids.Contains(c.PersonId)), Type.Season);
        Logger.MovieDb($"Show {season.Name}; Season {season.SeasonNumber}: Cast stored", LogEventLevel.Debug);
        
        await personRepository.StoreCrew(crews.Where(c => ids.Contains(c.PersonId)), Type.Season);
        Logger.MovieDb($"Show {season.Name}; Season {season.SeasonNumber}: Crew stored", LogEventLevel.Debug);
    }

    public async Task Store(TmdbEpisodeAppends episode)
    {
        (List<int> peopleIds, List<Cast> casts, List<Crew> crews, List<Role> roles, List<Job> jobs) = CollectPeople(episode);

        List<TmdbPersonAppends> peopleAppends = await FetchPeopleByIds(peopleIds);

        IEnumerable<Person> people = peopleAppends
            .Select(person => new Person
            {
                Id = person.Id,
                Adult = person.Adult,
                AlsoKnownAs = person.AlsoKnownAs.Length > 0 ? person.AlsoKnownAs.ToJson() : null,
                Biography = person.Biography,
                BirthDay = person.BirthDay,
                DeathDay = person.DeathDay,
                TmdbGender = (TmdbGender)person.TmdbGender,
                _externalIds = person.ExternalIds.ToJson(),
                Homepage = person.Homepage?.ToString(),
                ImdbId = person.ImdbId,
                KnownForDepartment = person.KnownForDepartment,
                Name = person.Name,
                PlaceOfBirth = person.PlaceOfBirth,
                Popularity = person.Popularity,
                Profile = person.ProfilePath,
                TitleSort = person.Name
            });

        await personRepository.Store(people);
        Logger.MovieDb($"Show {episode.Name}: Season {episode.SeasonNumber} Episode {episode.EpisodeNumber}: People stored");

        await personRepository.StoreRoles(roles);
        Logger.MovieDb($"Show {episode.Name}: Season {episode.SeasonNumber} Episode {episode.EpisodeNumber}: Roles stored", LogEventLevel.Debug);

        await personRepository.StoreJobs(jobs);
        Logger.MovieDb($"Show {episode.Name}: Season {episode.SeasonNumber} Episode {episode.EpisodeNumber}: Jobs stored", LogEventLevel.Debug);

        List<int> ids = personRepository.GetIds();
        
        await personRepository.StoreCast(casts.Where(c => ids.Contains(c.PersonId)), Type.Episode);
        Logger.MovieDb($"Show {episode.Name}: Season {episode.SeasonNumber} Episode {episode.EpisodeNumber}: Cast stored", LogEventLevel.Debug);
        
        await personRepository.StoreCrew(crews.Where(c => ids.Contains(c.PersonId)), Type.Episode);
        Logger.MovieDb($"Show {episode.Name}: Season {episode.SeasonNumber} Episode {episode.EpisodeNumber}: Crew stored", LogEventLevel.Debug);
    }

    public async Task Store(TmdbMovieAppends movie)
    {
        (List<int> peopleIds, List<Cast> casts, List<Crew> crews, List<Role> roles, List<Job> jobs) = CollectPeople(movie);

        List<TmdbPersonAppends> peopleAppends = await FetchPeopleByIds(peopleIds);

        IEnumerable<Person> people = peopleAppends
            .Select(person => new Person
            {
                Id = person.Id,
                Adult = person.Adult,
                AlsoKnownAs = person.AlsoKnownAs.Length > 0 ? person.AlsoKnownAs.ToJson() : null,
                Biography = person.Biography,
                BirthDay = person.BirthDay,
                DeathDay = person.DeathDay,
                TmdbGender = (TmdbGender)person.TmdbGender,
                _externalIds = person.ExternalIds.ToJson(),
                Homepage = person.Homepage?.ToString(),
                ImdbId = person.ImdbId,
                KnownForDepartment = person.KnownForDepartment,
                Name = person.Name,
                PlaceOfBirth = person.PlaceOfBirth,
                Popularity = person.Popularity,
                Profile = person.ProfilePath,
                TitleSort = person.Name
            });

        await personRepository.Store(people);
        Logger.MovieDb($"Movie: {movie.Title}: People stored");

        await personRepository.StoreRoles(roles);
        Logger.MovieDb($"Movie: {movie.Title}: Roles stored", LogEventLevel.Debug);

        await personRepository.StoreJobs(jobs);
        Logger.MovieDb($"Movie: {movie.Title}: Jobs stored", LogEventLevel.Debug);

        List<int> ids = personRepository.GetIds();

        await personRepository.StoreCast(casts.Where(c => ids.Contains(c.PersonId)), Type.Movie);
        Logger.MovieDb($"Movie: {movie.Title}: Cast stored", LogEventLevel.Debug);
        
        await personRepository.StoreCrew(crews.Where(c => ids.Contains(c.PersonId)), Type.Movie);
        Logger.MovieDb($"Movie: {movie.Title}: Crew stored", LogEventLevel.Debug);

        jobDispatcher.DispatchJob<AddPersonExtraDataJob, TmdbPersonAppends>(peopleAppends, movie.Title);
    }
    
    public Task Update(string showName, TmdbTvShowAppends show)
    {
        throw new NotImplementedException();
    }

    public Task Remove(string showName, TmdbTvShowAppends show)
    {
        throw new NotImplementedException();
    }

    internal async Task StoreTranslations(TmdbPersonAppends person)
    {
        IEnumerable<Translation> translations = person.Translations.Translations
            .Where(translation => translation.TmdbPersonTranslationData.Overview != "")
            .Select(translation => new Translation
            {
                Iso31661 = translation.Iso31661,
                Iso6391 = translation.Iso6391,
                Name = translation.Name == "" ? null : translation.Name,
                EnglishName = translation.EnglishName,
                Biography = translation.TmdbPersonTranslationData.Overview,
                PersonId = person.Id
            });

        await personRepository.StoreTranslationsAsync(translations);
    }

    internal async Task StoreImages(TmdbPersonAppends person)
    {
        IEnumerable<Image> posters = person.Images.Profiles
            .Select(image => new Image
            {
                AspectRatio = image.AspectRatio,
                Height = image.Height,
                Iso6391 = image.Iso6391,
                FilePath = image.FilePath,
                Width = image.Width,
                VoteAverage = image.VoteAverage,
                VoteCount = image.VoteCount,
                PersonId = person.Id,
                Type = "poster",
                Site = "https://image.tmdb.org/t/p/"
            })
            .ToList();

        await personRepository.StoreImagesAsync(posters);

        IEnumerable<Image> posterJobItems = posters
            .Select(x => new Image { FilePath = x.FilePath })
            .ToArray();
        if (posterJobItems.Any())
            jobDispatcher.DispatchJob<ImagePaletteJob, Image>(person.Id, posterJobItems);
    }

    private (List<int> peopleIds, List<Cast> casts, List<Crew> crews,  List<Role> roles, List<Job> jobs) CollectPeople(TmdbTvShowAppends show)
    {
        List<int> peopleIds = [];
        List<Role> roles = [];
        List<Job> jobs = [];
        List<Cast> casts = [];
        List<Crew> crews = [];
        
        foreach (TmdbTmdbAggregatedCast aggregateCast in show.AggregateCredits.Cast)
        {
            peopleIds.Add(aggregateCast.Id);

            roles.AddRange(aggregateCast.Roles.Select(creditRole => new Role
            {
                CreditId = creditRole.CreditId,
                Character = creditRole.Character,
                Order = creditRole.Order,
                EpisodeCount = creditRole.EpisodeCount
            }));
            
            casts.AddRange(aggregateCast.Roles.Select(creditRole => new Cast
            {
                CreditId = creditRole.CreditId,
                PersonId = aggregateCast.Id,
                TvId = show.Id
            }));
        }

        foreach (TmdbTmdbAggregatedCrew aggregateCrew in show.AggregateCredits.Crew)
        {
            peopleIds.Add(aggregateCrew.Id);

            jobs.AddRange(aggregateCrew.Jobs.Select(crewJob => new Job
            {
                CreditId = crewJob.CreditId,
                Task = crewJob.Job,
                Order = crewJob.Order,
                EpisodeCount = crewJob.EpisodeCount
            }));
            
            crews.AddRange(aggregateCrew.Jobs.Select(crewJob => new Crew
            {
                CreditId = crewJob.CreditId,
                PersonId = aggregateCrew.Id,
                TvId = show.Id
            }));
        }

        return (peopleIds, casts, crews, roles, jobs);
    }
    
    private (List<int> peopleIds, List<Cast> casts, List<Crew> crews, List<Role> roles, List<Job> jobs) CollectPeople(TmdbSeasonAppends season)
    {
        List<int> peopleIds = [];
        List<Role> roles = [];
        List<Job> jobs = [];
        List<Cast> casts = [];
        List<Crew> crews = [];

        foreach (TmdbTmdbAggregatedCast aggregateCast in season.AggregateCredits.Cast)
        {
            peopleIds.Add(aggregateCast.Id);
            
            roles.AddRange(aggregateCast.Roles.Select(r => new Role
            {
                CreditId = r.CreditId,
                Character = r.Character,
                Order = r.Order,
                EpisodeCount = r.EpisodeCount
            }));
            
            casts.AddRange(aggregateCast.Roles.Select(creditRole => new Cast
            {
                CreditId = creditRole.CreditId,
                PersonId = aggregateCast.Id,
                SeasonId = season.Id
            }));
        }

        foreach (TmdbTmdbAggregatedCrew aggregateCrew in season.AggregateCredits.Crew)
        {
            peopleIds.Add(aggregateCrew.Id);
            
            jobs.AddRange(aggregateCrew.Jobs.Select(j => new Job
            {
                CreditId = j.CreditId,
                Task = j.Job,
                Order = j.Order,
                EpisodeCount = j.EpisodeCount
            }));
            
            crews.AddRange(aggregateCrew.Jobs.Select(crewJob => new Crew
            {
                CreditId = crewJob.CreditId,
                PersonId = aggregateCrew.Id,
                SeasonId = season.Id
            }));
        }

        return (peopleIds, casts, crews, roles, jobs);
    }
    
    private (List<int> peopleIds, List<Cast> casts, List<Crew> crews, List<Role> roles, List<Job> jobs) CollectPeople(TmdbEpisodeAppends episode)
    {
        List<int> peopleIds = [];
        List<Role> roles = [];
        List<Job> jobs = [];
        List<Cast> casts = [];
        List<Crew> crews = [];

        foreach (TmdbCast tmdbCast in episode.Cast)
        {
            peopleIds.Add(tmdbCast.Id);
            
            roles.Add(new()
            {
                CreditId = tmdbCast.CreditId,
                Character = tmdbCast.Character,
                Order = tmdbCast.Order
            });
            
            casts.Add(new()
            {
                CreditId = tmdbCast.CreditId,
                PersonId = tmdbCast.Id,
                EpisodeId = episode.Id,
            });
        }

        foreach (TmdbCrew tmdbCrew in episode.Crew)
        {
            peopleIds.Add(tmdbCrew.Id);
            
            jobs.Add(new()
            {
                CreditId = tmdbCrew.CreditId,
                Task = tmdbCrew.Job,
                Order = tmdbCrew.Order
            });
            
            crews.Add(new()
            {
                CreditId = tmdbCrew.CreditId,
                PersonId = tmdbCrew.Id,
                EpisodeId = episode.Id,
            });
        }

        return (peopleIds, casts, crews, roles, jobs);
    }
    
    private (List<int> peopleIds, List<Cast> casts, List<Crew> crews, List<Role> roles, List<Job> jobs) CollectPeople(TmdbMovieAppends movie)
    {
        List<int> peopleIds = [];
        List<Role> roles = [];
        List<Job> jobs = [];
        List<Cast> casts = [];
        List<Crew> crews = [];

        foreach (TmdbCast aggregateCast in movie.Credits.Cast)
        {
            peopleIds.Add(aggregateCast.Id);

            roles.Add(new()
            {
                CreditId = aggregateCast.CreditId,
                Character = aggregateCast.Character,
                Order = aggregateCast.Order
            });
            
            casts.Add(new()
            {
                CreditId = aggregateCast.CreditId,
                PersonId = aggregateCast.Id,
                MovieId = movie.Id
            });
        }

        foreach (TmdbCrew tmdbCrew in movie.Credits.Crew)
        {
            peopleIds.Add(tmdbCrew.Id);

            jobs.Add(new()
            {
                CreditId = tmdbCrew.CreditId,
                Task = tmdbCrew.Job,
                Order = tmdbCrew.Order
            });
            
            crews.Add(new()
            {
                CreditId = tmdbCrew.CreditId,
                PersonId = tmdbCrew.Id,
                MovieId = movie.Id
            });
        }

        return (peopleIds, casts, crews, roles, jobs);
    }

    /** Note: The data returned here is a reduced set to improve performance. */
    private async Task<List<TmdbPersonAppends>> FetchPeopleByIds(List<int> ids)
    {
        List<TmdbPersonAppends> personAppends = [];

        await Parallel.ForEachAsync(ids, async (id, _) =>
        {
            try
            {
                using TmdbPersonClient personClient = new(id);
                TmdbPersonAppends? personTask = await personClient.WithAppends([
                    "external_ids",
                    "images",
                    "translations"
                ]);

                if (personTask?.Name is null)
                {
                    Logger.MovieDb($"Person {id} not found", LogEventLevel.Warning);
                    return;
                }

                personAppends.Add(personTask);
            }
            catch (Exception e)
            {
                Logger.MovieDb(e.Message, LogEventLevel.Error);
            }
        });

        return personAppends?
            .OrderBy(f => f.Name)
            .ToList() ?? [];
    }
}