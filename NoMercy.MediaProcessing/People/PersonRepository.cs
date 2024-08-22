using FlexLabs.EntityFrameworkCore.Upsert;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.MediaProcessing.People
{
    public class PersonRepository(MediaContext context): IPersonRepository
    {
        public Task StoreAsync(IEnumerable<Person> people)
        {
            return context.People.UpsertRange(people)
                .On(p => new { p.Id })
                .WhenMatched((ps, pi) => new Person
                {
                    Id = pi.Id,
                    Adult = pi.Adult,
                    AlsoKnownAs = pi.AlsoKnownAs,
                    Biography = pi.Biography,
                    BirthDay = pi.BirthDay,
                    DeathDay = pi.DeathDay,
                    _externalIds = pi._externalIds,
                    TmdbGender = pi.TmdbGender,
                    Homepage = pi.Homepage,
                    ImdbId = pi.ImdbId,
                    KnownForDepartment = pi.KnownForDepartment,
                    Name = pi.Name,
                    PlaceOfBirth = pi.PlaceOfBirth,
                    Popularity = pi.Popularity,
                    Profile = pi.Profile,
                    TitleSort = pi.Name
                })
                .RunAsync();
        }

        public Task StoreTranslationsAsync(IEnumerable<Translation> translations)
        {
            return context.Translations
                .UpsertRange(translations.Where(translation => translation.Title != null || translation.Overview != ""))
                .On(t => new { t.Iso31661, t.Iso6391, t.PersonId })
                .WhenMatched((ts, ti) => new Translation
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

        public Task StoreImagesAsync(IEnumerable<Image> images)
        {
            return context.Images.UpsertRange(images)
                .On(v => new { v.FilePath, v.PersonId })
                .WhenMatched((ts, ti) => new Image
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
                    PersonId = ti.PersonId
                })
                .RunAsync();
        }

        public Task StoreCastAsync(IEnumerable<Cast> cast, Type type)
        {
            UpsertCommandBuilder<Cast> query = type switch
            {
                Type.Movie => context.Casts.UpsertRange(cast).On(c => new { c.CreditId, c.MovieId, c.RoleId }),
                Type.TvShow => context.Casts.UpsertRange(cast).On(c => new { c.CreditId, c.TvId, c.RoleId }),
                Type.Season => context.Casts.UpsertRange(cast).On(c => new { c.CreditId, c.SeasonId, c.RoleId }),
                Type.Episode => context.Casts.UpsertRange(cast).On(c => new { c.CreditId, c.EpisodeId, c.RoleId }),
                _ => throw new ArgumentOutOfRangeException()
            };

            return query.WhenMatched((cs, ci) => new Cast
                {
                    CreditId = ci.CreditId,
                    MovieId = ci.MovieId,
                    TvId = ci.TvId,
                    SeasonId = ci.SeasonId,
                    EpisodeId = ci.EpisodeId,
                    PersonId = ci.PersonId,
                    RoleId = ci.RoleId
                })
                .RunAsync();
        }

        public Task StoreCrewAsync(IEnumerable<Crew> crew, Type type)
        {
            UpsertCommandBuilder<Crew> query = type switch
            {
                Type.Movie => context.Crews.UpsertRange(crew).On(c => new { c.CreditId, c.MovieId, c.JobId }),
                Type.TvShow => context.Crews.UpsertRange(crew).On(c => new { c.CreditId, c.TvId, c.JobId }),
                Type.Season => context.Crews.UpsertRange(crew).On(c => new { c.CreditId, c.SeasonId, c.JobId }),
                Type.Episode => context.Crews.UpsertRange(crew).On(c => new { c.CreditId, c.EpisodeId, c.JobId }),
                _ => throw new ArgumentOutOfRangeException()
            };

            return query.WhenMatched((cs, ci) => new Crew
                {
                    CreditId = ci.CreditId,
                    MovieId = ci.MovieId,
                    TvId = ci.TvId,
                    SeasonId = ci.SeasonId,
                    EpisodeId = ci.EpisodeId,
                    PersonId = ci.PersonId,
                    JobId = ci.JobId
                })
                .RunAsync();
        }

        public Task StoreCreatorAsync(Creator creator)
        {
            return context.Creators.Upsert(creator)
                .On(c => new { c.TvId, c.PersonId })
                .WhenMatched((cs, ci) => new Creator
                {
                    TvId = ci.TvId,
                    PersonId = ci.PersonId
                })
                .RunAsync();
        }

        public Task StoreGuestStarsAsync(IEnumerable<GuestStar> guestStars)
        {
            return context.GuestStars
                .UpsertRange(guestStars)
                .On(c => new { c.CreditId, c.EpisodeId })
                .WhenMatched((cs, ci) => new GuestStar
                {
                    Id = ci.Id,
                    CreditId = ci.CreditId,
                    PersonId = ci.PersonId,
                    EpisodeId = ci.EpisodeId
                })
                .RunAsync();
        }

        public Task StoreRolesAsync(IEnumerable<Role> roles)
        {
            return  context.Roles
                .UpsertRange(roles)
                .On(p => new { p.CreditId })
                .WhenMatched((rs, ri) => new Role
                {
                    EpisodeCount = ri.EpisodeCount,
                    Character = ri.Character,
                    Order = ri.Order,
                    CreditId = ri.CreditId
                })
                .RunAsync();
        }

        public Task StoreJobsAsync(IEnumerable<Job> jobs)
        {
            return context.Jobs.UpsertRange(jobs)
                .On(p => new { p.CreditId })
                .WhenMatched((js, ji) => new Job
                {
                    Task = ji.Task,
                    CreditId = ji.CreditId,
                    EpisodeCount = ji.EpisodeCount,
                    Order = ji.Order
                })
                .RunAsync();
        }

        public Task StoreAggregateCreditsAsync()
        {
            throw new NotImplementedException();
        }

        public Task StoreAggregateCastAsync()
        {
            throw new NotImplementedException();
        }

        public Task StoreAggregateCrewAsync()
        {
            throw new NotImplementedException();
        }
    }
}