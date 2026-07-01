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
        ) = CollectPeople(show);

        List<TmdbPersonAppends> peopleAppends = await FetchPeopleByIds(peopleIds);

        IEnumerable<Person> people = peopleAppends.Select(person => new Person
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

        await personRepository.Store(people);
        logger.LogInformation("Show {Name}: People stored", show.Name);

        await personRepository.StoreRoles(roles);
        logger.LogDebug("Show {Name}: Roles stored", show.Name);

        await personRepository.StoreJobs(jobs);
        logger.LogDebug("Show {Name}: Jobs stored", show.Name);

        List<int> ids = personRepository.GetIds();

        await personRepository.StoreAggregateCreditsAsync(
            casts.Where(c => ids.Contains(c.PersonId)),
            crews.Where(c => ids.Contains(c.PersonId)),
            Type.TvShow
        );
        logger.LogDebug("Show {Name}: Aggregate credits stored", show.Name);

        foreach (Person person in people)
            jobDispatcher.DispatchColorPaletteJob("person", person.Id.ToString());

        jobDispatcher.DispatchJob<PersonExtrasJob, TmdbPersonAppends>(peopleAppends, show.Name);
    }

    public async Task Store(TmdbSeasonAppends season)
    {
        (
            List<int> peopleIds,
            List<Cast> casts,
            List<Crew> crews,
            List<Role> roles,
            List<Job> jobs
        ) = CollectPeople(season);

        List<TmdbPersonAppends> peopleAppends = await FetchPeopleByIds(peopleIds);

        IEnumerable<Person> people = peopleAppends.Select(person => new Person
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

        await personRepository.Store(people);
        logger.LogInformation(
            "Show {Name}; Season {SeasonNumber}: People stored",
            season.Name,
            season.SeasonNumber
        );

        await personRepository.StoreRoles(roles);
        logger.LogDebug(
            "Show {Name}; Season {SeasonNumber}: Roles stored",
            season.Name,
            season.SeasonNumber
        );

        await personRepository.StoreJobs(jobs);
        logger.LogDebug(
            "Show {Name}; Season {SeasonNumber}: Jobs stored",
            season.Name,
            season.SeasonNumber
        );

        List<int> ids = personRepository.GetIds();

        await personRepository.StoreCast(casts.Where(c => ids.Contains(c.PersonId)), Type.Season);
        logger.LogDebug(
            "Show {Name}; Season {SeasonNumber}: Cast stored",
            season.Name,
            season.SeasonNumber
        );

        await personRepository.StoreCrew(crews.Where(c => ids.Contains(c.PersonId)), Type.Season);
        logger.LogDebug(
            "Show {Name}; Season {SeasonNumber}: Crew stored",
            season.Name,
            season.SeasonNumber
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
        ) = CollectPeople(episode);

        List<TmdbPersonAppends> peopleAppends = await FetchPeopleByIds(peopleIds);

        IEnumerable<Person> people = peopleAppends.Select(person => new Person
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

        await personRepository.Store(people);
        logger.LogInformation(
            "Show {Name}: Season {SeasonNumber} Episode {EpisodeNumber}: People stored",
            episode.Name,
            episode.SeasonNumber,
            episode.EpisodeNumber
        );

        await personRepository.StoreRoles(roles);
        logger.LogDebug(
            "Show {Name}: Season {SeasonNumber} Episode {EpisodeNumber}: Roles stored",
            episode.Name,
            episode.SeasonNumber,
            episode.EpisodeNumber
        );

        await personRepository.StoreJobs(jobs);
        logger.LogDebug(
            "Show {Name}: Season {SeasonNumber} Episode {EpisodeNumber}: Jobs stored",
            episode.Name,
            episode.SeasonNumber,
            episode.EpisodeNumber
        );

        List<int> ids = personRepository.GetIds();

        await personRepository.StoreCast(casts.Where(c => ids.Contains(c.PersonId)), Type.Episode);
        logger.LogDebug(
            "Show {Name}: Season {SeasonNumber} Episode {EpisodeNumber}: Cast stored",
            episode.Name,
            episode.SeasonNumber,
            episode.EpisodeNumber
        );

        await personRepository.StoreCrew(crews.Where(c => ids.Contains(c.PersonId)), Type.Episode);
        logger.LogDebug(
            "Show {Name}: Season {SeasonNumber} Episode {EpisodeNumber}: Crew stored",
            episode.Name,
            episode.SeasonNumber,
            episode.EpisodeNumber
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
        ) = CollectPeople(movie);

        List<TmdbPersonAppends> peopleAppends = await FetchPeopleByIds(peopleIds);

        IEnumerable<Person> people = peopleAppends.Select(person => new Person
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

        await personRepository.Store(people);
        logger.LogInformation("Movie: {Title}: People stored", movie.Title);

        await personRepository.StoreRoles(roles);
        logger.LogDebug("Movie: {Title}: Roles stored", movie.Title);

        await personRepository.StoreJobs(jobs);
        logger.LogDebug("Movie: {Title}: Jobs stored", movie.Title);

        List<int> ids = personRepository.GetIds();

        await personRepository.StoreCast(casts.Where(c => ids.Contains(c.PersonId)), Type.Movie);
        logger.LogDebug("Movie: {Title}: Cast stored", movie.Title);

        await personRepository.StoreCrew(crews.Where(c => ids.Contains(c.PersonId)), Type.Movie);
        logger.LogDebug("Movie: {Title}: Crew stored", movie.Title);

        foreach (Person person in people)
            jobDispatcher.DispatchColorPaletteJob("person", person.Id.ToString());

        jobDispatcher.DispatchJob<PersonExtrasJob, TmdbPersonAppends>(peopleAppends, movie.Title);
    }

    public Task Update(string showName, TmdbTvShowAppends show)
    {
        // Re-importing the show's people/credits is an idempotent upsert,
        // so re-running Store refreshes them in place.
        return Store(show);
    }

    public async Task Remove(string showName, TmdbTvShowAppends show)
    {
        // Remove this show's cast/crew associations. Shared Person rows are
        // left intact as they may still be referenced by other titles.
        await personRepository.RemoveAggregateCreditsAsync(show.Id);
        logger.LogDebug("Show {ShowName}: People credits removed", showName);
    }

    public async Task UpdatePersonAsync(int personId)
    {
        using TmdbPersonClient personClient = new(personId);
        TmdbPersonAppends? person = await personClient.WithAppends([
            "external_ids",
            "images",
            "translations",
        ]);

        if (person?.Name is null)
        {
            logger.LogWarning("Person {PersonId} not found during refresh", personId);
            return;
        }

        await personRepository.Store([ToPersonEntity(person)]);
        await StoreTranslations(person);
        await StoreImages(person);

        logger.LogDebug("Person {Name}: refreshed from TMDB changes", person.Name);
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
            .Translations.Translations.Where(translation =>
                translation.TmdbPersonTranslationData.Overview != ""
            )
            .Select(translation => new Translation
            {
                Iso31661 = translation.Iso31661,
                Iso6391 = translation.Iso6391,
                Name = translation.Name == "" ? null : translation.Name,
                EnglishName = translation.EnglishName,
                Biography = translation.TmdbPersonTranslationData.Overview,
                PersonId = person.Id,
            });

        await personRepository.StoreTranslationsAsync(translations);
    }

    internal async Task StoreImages(TmdbPersonAppends person)
    {
        IEnumerable<Image> posters = person
            .Images.Profiles.Select(image => new Image
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

        await personRepository.StoreImagesAsync(posters);
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
            peopleIds.Add(aggregateCast.Id);

            roles.AddRange(
                aggregateCast.Roles.Select(creditRole => new Role
                {
                    CreditId = creditRole.CreditId,
                    Character = creditRole.Character,
                    Order = creditRole.Order,
                    EpisodeCount = creditRole.EpisodeCount,
                })
            );

            casts.AddRange(
                aggregateCast.Roles.Select(creditRole => new Cast
                {
                    CreditId = creditRole.CreditId,
                    PersonId = aggregateCast.Id,
                    TvId = show.Id,
                })
            );
        }

        foreach (TmdbTmdbAggregatedCrew aggregateCrew in show.AggregateCredits.Crew)
        {
            peopleIds.Add(aggregateCrew.Id);

            jobs.AddRange(
                aggregateCrew.Jobs.Select(crewJob => new Job
                {
                    CreditId = crewJob.CreditId.OrEmpty(),
                    Task = crewJob.Job,
                    Order = crewJob.Order,
                    EpisodeCount = crewJob.EpisodeCount,
                })
            );

            crews.AddRange(
                aggregateCrew.Jobs.Select(crewJob => new Crew
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
            peopleIds.Add(aggregateCast.Id);

            roles.AddRange(
                aggregateCast.Roles.Select(r => new Role
                {
                    CreditId = r.CreditId,
                    Character = r.Character,
                    Order = r.Order,
                    EpisodeCount = r.EpisodeCount,
                })
            );

            casts.AddRange(
                aggregateCast.Roles.Select(creditRole => new Cast
                {
                    CreditId = creditRole.CreditId,
                    PersonId = aggregateCast.Id,
                    SeasonId = season.Id,
                })
            );
        }

        foreach (TmdbTmdbAggregatedCrew aggregateCrew in season.AggregateCredits.Crew)
        {
            peopleIds.Add(aggregateCrew.Id);

            jobs.AddRange(
                aggregateCrew.Jobs.Select(j => new Job
                {
                    CreditId = j.CreditId.OrEmpty(),
                    Task = j.Job,
                    Order = j.Order,
                    EpisodeCount = j.EpisodeCount,
                })
            );

            crews.AddRange(
                aggregateCrew.Jobs.Select(crewJob => new Crew
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
            peopleIds.Add(tmdbCast.Id);

            roles.Add(
                new()
                {
                    CreditId = tmdbCast.CreditId,
                    Character = tmdbCast.Character,
                    Order = tmdbCast.Order,
                }
            );

            casts.Add(
                new()
                {
                    CreditId = tmdbCast.CreditId,
                    PersonId = tmdbCast.Id,
                    EpisodeId = episode.Id,
                }
            );
        }

        foreach (TmdbCrew tmdbCrew in episode.Crew)
        {
            peopleIds.Add(tmdbCrew.Id);

            jobs.Add(
                new()
                {
                    CreditId = tmdbCrew.CreditId.OrEmpty(),
                    Task = tmdbCrew.Job,
                    Order = tmdbCrew.Order,
                }
            );

            crews.Add(
                new()
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
            peopleIds.Add(aggregateCast.Id);

            roles.Add(
                new()
                {
                    CreditId = aggregateCast.CreditId,
                    Character = aggregateCast.Character,
                    Order = aggregateCast.Order,
                }
            );

            casts.Add(
                new()
                {
                    CreditId = aggregateCast.CreditId,
                    PersonId = aggregateCast.Id,
                    MovieId = movie.Id,
                }
            );
        }

        foreach (TmdbCrew tmdbCrew in movie.Credits.Crew)
        {
            peopleIds.Add(tmdbCrew.Id);

            jobs.Add(
                new()
                {
                    CreditId = tmdbCrew.CreditId.OrEmpty(),
                    Task = tmdbCrew.Job,
                    Order = tmdbCrew.Order,
                }
            );

            crews.Add(
                new()
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
                ids,
                SystemParallelism.Options,
                async (id, _) =>
                {
                    try
                    {
                        using TmdbPersonClient personClient = new(id);
                        TmdbPersonAppends? personTask = await personClient.WithAppends([
                            "external_ids",
                            "images",
                            "translations",
                        ]);

                        if (personTask?.Name is null)
                        {
                            logger.LogWarning("Person {Id} not found", id);
                            return;
                        }

                        personAppends.Add(personTask);
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e.Message);
                    }
                }
            );

            return personAppends.Where(f => f is { Name: not null }).OrderBy(f => f!.Name).ToList();
        }
        catch (Exception e)
        {
            logger.LogError(e.Message);
        }

        return [];
    }
}
