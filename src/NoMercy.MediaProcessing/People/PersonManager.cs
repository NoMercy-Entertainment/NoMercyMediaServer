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

using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.People;
using NoMercy.MediaProcessing.Common;
using NoMercy.MediaProcessing.Jobs;
using NoMercy.MediaProcessing.Jobs.MediaJobs;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.Providers.TMDB.Client;
using NoMercy.Providers.TMDB.Models.Episode;
using NoMercy.Providers.TMDB.Models.Movies;
using NoMercy.Providers.TMDB.Models.People;
using NoMercy.Providers.TMDB.Models.Season;
using NoMercy.Providers.TMDB.Models.Shared;
using NoMercy.Providers.TMDB.Models.TV;
using TmdbGender = NoMercy.Database.Models.People.TmdbGender;

namespace NoMercy.MediaProcessing.People;

public class PersonManager(
    IPersonRepository personRepository,
    JobDispatcher jobDispatcher,
    ILogger<PersonManager> logger
) : BaseManager, IPersonManager
{
    public async Task Store(TmdbTvShowAppends show)
    {
        (
            List<int> peopleIds,
            List<Cast> casts,
            List<Crew> crews,
            List<Role> roles,
            List<Job> jobs
        ) = CollectPeople(show: show);

        List<TmdbPersonAppends> peopleAppends = await FetchPeopleByIds(ids: peopleIds);

        IEnumerable<Person> people = peopleAppends.Select(selector: person => new Person
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
            TitleSort = person.Name,
        });

        await personRepository.Store(people: people);
        logger.LogInformation(message: "Show {Name}: People stored", args: show.Name);

        await personRepository.StoreRoles(roles: roles);
        logger.LogDebug(message: "Show {Name}: Roles stored", args: show.Name);

        await personRepository.StoreJobs(job: jobs);
        logger.LogDebug(message: "Show {Name}: Jobs stored", args: show.Name);

        List<int> ids = personRepository.GetIds();

        await personRepository.StoreAggregateCreditsAsync(
            cast: casts.Where(predicate: c => ids.Contains(item: c.PersonId)),
            crew: crews.Where(predicate: c => ids.Contains(item: c.PersonId)),
            type: Type.TvShow
        );
        logger.LogDebug(message: "Show {Name}: Aggregate credits stored", args: show.Name);

        foreach (Person person in people)
            jobDispatcher.DispatchColorPaletteJob(entityType: "person", entityId: person.Id.ToString());

        jobDispatcher.DispatchJob<PersonExtrasJob, TmdbPersonAppends>(data: peopleAppends, name: show.Name);
    }

    public async Task Store(TmdbSeasonAppends season)
    {
        (
            List<int> peopleIds,
            List<Cast> casts,
            List<Crew> crews,
            List<Role> roles,
            List<Job> jobs
        ) = CollectPeople(season: season);

        List<TmdbPersonAppends> peopleAppends = await FetchPeopleByIds(ids: peopleIds);

        IEnumerable<Person> people = peopleAppends.Select(selector: person => new Person
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
            TitleSort = person.Name,
        });

        await personRepository.Store(people: people);
        logger.LogInformation(
            message: "Show {Name}; Season {SeasonNumber}: People stored", args: [season.Name, season.SeasonNumber]
        );

        await personRepository.StoreRoles(roles: roles);
        logger.LogDebug(
            message: "Show {Name}; Season {SeasonNumber}: Roles stored", args: [season.Name, season.SeasonNumber]
        );

        await personRepository.StoreJobs(job: jobs);
        logger.LogDebug(
            message: "Show {Name}; Season {SeasonNumber}: Jobs stored", args: [season.Name, season.SeasonNumber]
        );

        List<int> ids = personRepository.GetIds();

        await personRepository.StoreCast(cast: casts.Where(predicate: c => ids.Contains(item: c.PersonId)), type: Type.Season);
        logger.LogDebug(
            message: "Show {Name}; Season {SeasonNumber}: Cast stored", args: [season.Name, season.SeasonNumber]
        );

        await personRepository.StoreCrew(crew: crews.Where(predicate: c => ids.Contains(item: c.PersonId)), type: Type.Season);
        logger.LogDebug(
            message: "Show {Name}; Season {SeasonNumber}: Crew stored", args: [season.Name, season.SeasonNumber]
        );
    }

    public async Task Store(TmdbEpisodeAppends episode)
    {
        (
            List<int> peopleIds,
            List<Cast> casts,
            List<Crew> crews,
            List<Role> roles,
            List<Job> jobs
        ) = CollectPeople(episode: episode);

        List<TmdbPersonAppends> peopleAppends = await FetchPeopleByIds(ids: peopleIds);

        IEnumerable<Person> people = peopleAppends.Select(selector: person => new Person
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
            TitleSort = person.Name,
        });

        await personRepository.Store(people: people);
        logger.LogInformation(
            message: "Show {Name}: Season {SeasonNumber} Episode {EpisodeNumber}: People stored", args: [episode.Name, episode.SeasonNumber, episode.EpisodeNumber]
        );

        await personRepository.StoreRoles(roles: roles);
        logger.LogDebug(
            message: "Show {Name}: Season {SeasonNumber} Episode {EpisodeNumber}: Roles stored", args: [episode.Name, episode.SeasonNumber, episode.EpisodeNumber]
        );

        await personRepository.StoreJobs(job: jobs);
        logger.LogDebug(
            message: "Show {Name}: Season {SeasonNumber} Episode {EpisodeNumber}: Jobs stored", args: [episode.Name, episode.SeasonNumber, episode.EpisodeNumber]
        );

        List<int> ids = personRepository.GetIds();

        await personRepository.StoreCast(cast: casts.Where(predicate: c => ids.Contains(item: c.PersonId)), type: Type.Episode);
        logger.LogDebug(
            message: "Show {Name}: Season {SeasonNumber} Episode {EpisodeNumber}: Cast stored", args: [episode.Name, episode.SeasonNumber, episode.EpisodeNumber]
        );

        await personRepository.StoreCrew(crew: crews.Where(predicate: c => ids.Contains(item: c.PersonId)), type: Type.Episode);
        logger.LogDebug(
            message: "Show {Name}: Season {SeasonNumber} Episode {EpisodeNumber}: Crew stored", args: [episode.Name, episode.SeasonNumber, episode.EpisodeNumber]
        );
    }

    public async Task Store(TmdbMovieAppends movie)
    {
        (
            List<int> peopleIds,
            List<Cast> casts,
            List<Crew> crews,
            List<Role> roles,
            List<Job> jobs
        ) = CollectPeople(movie: movie);

        List<TmdbPersonAppends> peopleAppends = await FetchPeopleByIds(ids: peopleIds);

        IEnumerable<Person> people = peopleAppends.Select(selector: person => new Person
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
            TitleSort = person.Name,
        });

        await personRepository.Store(people: people);
        logger.LogInformation(message: "Movie: {Title}: People stored", args: movie.Title);

        await personRepository.StoreRoles(roles: roles);
        logger.LogDebug(message: "Movie: {Title}: Roles stored", args: movie.Title);

        await personRepository.StoreJobs(job: jobs);
        logger.LogDebug(message: "Movie: {Title}: Jobs stored", args: movie.Title);

        List<int> ids = personRepository.GetIds();

        await personRepository.StoreCast(cast: casts.Where(predicate: c => ids.Contains(item: c.PersonId)), type: Type.Movie);
        logger.LogDebug(message: "Movie: {Title}: Cast stored", args: movie.Title);

        await personRepository.StoreCrew(crew: crews.Where(predicate: c => ids.Contains(item: c.PersonId)), type: Type.Movie);
        logger.LogDebug(message: "Movie: {Title}: Crew stored", args: movie.Title);

        foreach (Person person in people)
            jobDispatcher.DispatchColorPaletteJob(entityType: "person", entityId: person.Id.ToString());

        jobDispatcher.DispatchJob<PersonExtrasJob, TmdbPersonAppends>(data: peopleAppends, name: movie.Title);
    }

    public Task Update(string showName, TmdbTvShowAppends show)
    {
        // Re-importing the show's people/credits is an idempotent upsert,
        // so re-running Store refreshes them in place.
        return Store(show: show);
    }

    public async Task Remove(string showName, TmdbTvShowAppends show)
    {
        // Remove this show's cast/crew associations. Shared Person rows are
        // left intact as they may still be referenced by other titles.
        await personRepository.RemoveAggregateCreditsAsync(tvId: show.Id);
        logger.LogDebug(message: "Show {ShowName}: People credits removed", args: showName);
    }

    public async Task UpdatePersonAsync(int personId)
    {
        using TmdbPersonClient personClient = new(id: personId);
        TmdbPersonAppends? person = await personClient.WithAppends(appendices:
        [
            "external_ids",
            "images",
            "translations",
        ]);

        if (person?.Name is null)
        {
            logger.LogWarning(message: "Person {PersonId} not found during refresh", args: personId);
            return;
        }

        await personRepository.Store(people: [ToPersonEntity(person: person)]);
        await StoreTranslations(person: person);
        await StoreImages(person: person);

        logger.LogDebug(message: "Person {Name}: refreshed from TMDB changes", args: person.Name);
    }

    private static Person ToPersonEntity(TmdbPersonAppends person)
    {
        return new()
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
            TitleSort = person.Name,
        };
    }

    internal async Task StoreTranslations(TmdbPersonAppends person)
    {
        IEnumerable<Translation> translations = person
            .Translations.Translations.Where(predicate: translation =>
                translation.TmdbPersonTranslationData.Overview != ""
            )
            .Select(selector: translation => new Translation
            {
                Iso31661 = translation.Iso31661,
                Iso6391 = translation.Iso6391,
                Name = translation.Name == "" ? null : translation.Name,
                EnglishName = translation.EnglishName,
                Biography = translation.TmdbPersonTranslationData.Overview,
                PersonId = person.Id,
            });

        await personRepository.StoreTranslationsAsync(translations: translations);
    }

    internal async Task StoreImages(TmdbPersonAppends person)
    {
        IEnumerable<Image> posters = person
            .Images.Profiles.Select(selector: image => new Image
            {
                AspectRatio = image.AspectRatio,
                Height = image.Height,
                Iso6391 = image.Iso6391,
                FilePath = image.FilePath.OrEmpty(),
                Width = image.Width,
                VoteAverage = image.VoteAverage,
                VoteCount = image.VoteCount,
                PersonId = person.Id,
                Type = "poster",
                Site = "https://image.tmdb.org/t/p/",
            })
            .ToList();

        await personRepository.StoreImagesAsync(images: posters);
    }

    private (
        List<int> peopleIds,
        List<Cast> casts,
        List<Crew> crews,
        List<Role> roles,
        List<Job> jobs
    ) CollectPeople(TmdbTvShowAppends show)
    {
        List<int> peopleIds = [];
        List<Role> roles = [];
        List<Job> jobs = [];
        List<Cast> casts = [];
        List<Crew> crews = [];

        foreach (TmdbTmdbAggregatedCast aggregateCast in show.AggregateCredits.Cast)
        {
            peopleIds.Add(item: aggregateCast.Id);

            roles.AddRange(
                collection: aggregateCast.Roles.Select(selector: creditRole => new Role
                {
                    CreditId = creditRole.CreditId,
                    Character = creditRole.Character,
                    Order = creditRole.Order,
                    EpisodeCount = creditRole.EpisodeCount,
                })
            );

            casts.AddRange(
                collection: aggregateCast.Roles.Select(selector: creditRole => new Cast
                {
                    CreditId = creditRole.CreditId,
                    PersonId = aggregateCast.Id,
                    TvId = show.Id,
                })
            );
        }

        foreach (TmdbTmdbAggregatedCrew aggregateCrew in show.AggregateCredits.Crew)
        {
            peopleIds.Add(item: aggregateCrew.Id);

            jobs.AddRange(
                collection: aggregateCrew.Jobs.Select(selector: crewJob => new Job
                {
                    CreditId = crewJob.CreditId.OrEmpty(),
                    Task = crewJob.Job,
                    Order = crewJob.Order,
                    EpisodeCount = crewJob.EpisodeCount,
                })
            );

            crews.AddRange(
                collection: aggregateCrew.Jobs.Select(selector: crewJob => new Crew
                {
                    CreditId = crewJob.CreditId,
                    PersonId = aggregateCrew.Id,
                    TvId = show.Id,
                })
            );
        }

        return (peopleIds, casts, crews, roles, jobs);
    }

    private (
        List<int> peopleIds,
        List<Cast> casts,
        List<Crew> crews,
        List<Role> roles,
        List<Job> jobs
    ) CollectPeople(TmdbSeasonAppends season)
    {
        List<int> peopleIds = [];
        List<Role> roles = [];
        List<Job> jobs = [];
        List<Cast> casts = [];
        List<Crew> crews = [];

        foreach (TmdbTmdbAggregatedCast aggregateCast in season.AggregateCredits.Cast)
        {
            peopleIds.Add(item: aggregateCast.Id);

            roles.AddRange(
                collection: aggregateCast.Roles.Select(selector: r => new Role
                {
                    CreditId = r.CreditId,
                    Character = r.Character,
                    Order = r.Order,
                    EpisodeCount = r.EpisodeCount,
                })
            );

            casts.AddRange(
                collection: aggregateCast.Roles.Select(selector: creditRole => new Cast
                {
                    CreditId = creditRole.CreditId,
                    PersonId = aggregateCast.Id,
                    SeasonId = season.Id,
                })
            );
        }

        foreach (TmdbTmdbAggregatedCrew aggregateCrew in season.AggregateCredits.Crew)
        {
            peopleIds.Add(item: aggregateCrew.Id);

            jobs.AddRange(
                collection: aggregateCrew.Jobs.Select(selector: j => new Job
                {
                    CreditId = j.CreditId.OrEmpty(),
                    Task = j.Job,
                    Order = j.Order,
                    EpisodeCount = j.EpisodeCount,
                })
            );

            crews.AddRange(
                collection: aggregateCrew.Jobs.Select(selector: crewJob => new Crew
                {
                    CreditId = crewJob.CreditId,
                    PersonId = aggregateCrew.Id,
                    SeasonId = season.Id,
                })
            );
        }

        return (peopleIds, casts, crews, roles, jobs);
    }

    private (
        List<int> peopleIds,
        List<Cast> casts,
        List<Crew> crews,
        List<Role> roles,
        List<Job> jobs
    ) CollectPeople(TmdbEpisodeAppends episode)
    {
        List<int> peopleIds = [];
        List<Role> roles = [];
        List<Job> jobs = [];
        List<Cast> casts = [];
        List<Crew> crews = [];

        foreach (TmdbCast tmdbCast in episode.Cast)
        {
            peopleIds.Add(item: tmdbCast.Id);

            roles.Add(
                item: new()
                {
                    CreditId = tmdbCast.CreditId,
                    Character = tmdbCast.Character,
                    Order = tmdbCast.Order,
                }
            );

            casts.Add(
                item: new()
                {
                    CreditId = tmdbCast.CreditId,
                    PersonId = tmdbCast.Id,
                    EpisodeId = episode.Id,
                }
            );
        }

        foreach (TmdbCrew tmdbCrew in episode.Crew)
        {
            peopleIds.Add(item: tmdbCrew.Id);

            jobs.Add(
                item: new()
                {
                    CreditId = tmdbCrew.CreditId.OrEmpty(),
                    Task = tmdbCrew.Job,
                    Order = tmdbCrew.Order,
                }
            );

            crews.Add(
                item: new()
                {
                    CreditId = tmdbCrew.CreditId,
                    PersonId = tmdbCrew.Id,
                    EpisodeId = episode.Id,
                }
            );
        }

        return (peopleIds, casts, crews, roles, jobs);
    }

    private (
        List<int> peopleIds,
        List<Cast> casts,
        List<Crew> crews,
        List<Role> roles,
        List<Job> jobs
    ) CollectPeople(TmdbMovieAppends movie)
    {
        List<int> peopleIds = [];
        List<Role> roles = [];
        List<Job> jobs = [];
        List<Cast> casts = [];
        List<Crew> crews = [];

        foreach (TmdbCast aggregateCast in movie.Credits.Cast)
        {
            peopleIds.Add(item: aggregateCast.Id);

            roles.Add(
                item: new()
                {
                    CreditId = aggregateCast.CreditId,
                    Character = aggregateCast.Character,
                    Order = aggregateCast.Order,
                }
            );

            casts.Add(
                item: new()
                {
                    CreditId = aggregateCast.CreditId,
                    PersonId = aggregateCast.Id,
                    MovieId = movie.Id,
                }
            );
        }

        foreach (TmdbCrew tmdbCrew in movie.Credits.Crew)
        {
            peopleIds.Add(item: tmdbCrew.Id);

            jobs.Add(
                item: new()
                {
                    CreditId = tmdbCrew.CreditId.OrEmpty(),
                    Task = tmdbCrew.Job,
                    Order = tmdbCrew.Order,
                }
            );

            crews.Add(
                item: new()
                {
                    CreditId = tmdbCrew.CreditId,
                    PersonId = tmdbCrew.Id,
                    MovieId = movie.Id,
                }
            );
        }

        return (peopleIds, casts, crews, roles, jobs);
    }

    /** Note: The data returned here is a reduced set to improve performance. */
    private async Task<List<TmdbPersonAppends>> FetchPeopleByIds(List<int> ids)
    {
        try
        {
            ConcurrentBag<TmdbPersonAppends> personAppends = [];

            await Parallel.ForEachAsync(
                source: ids,
                parallelOptions: SystemParallelism.Options,
                body: async (id, _) =>
                {
                    try
                    {
                        using TmdbPersonClient personClient = new(id: id);
                        TmdbPersonAppends? personTask = await personClient.WithAppends(appendices:
                        [
                            "external_ids",
                            "images",
                            "translations",
                        ]);

                        if (personTask?.Name is null)
                        {
                            logger.LogWarning(message: "Person {Id} not found", args: id);
                            return;
                        }

                        personAppends.Add(item: personTask);
                    }
                    catch (Exception e)
                    {
                        logger.LogError(message: e.Message);
                    }
                }
            );

            return personAppends.Where(predicate: f => f is { Name: not null }).OrderBy(keySelector: f => f!.Name).ToList();
        }
        catch (Exception e)
        {
            logger.LogError(message: e.Message);
        }

        return [];
    }
}
