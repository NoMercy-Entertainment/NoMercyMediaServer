using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Providers.TMDB.Models.People;
using TmdbGender=NoMercy.Providers.TMDB.Models.People.TmdbGender;

namespace NoMercy.Api.Controllers.V1.Media.DTO;
public record PersonResponseItemDto
{
    [JsonProperty("id")] public long Id { get; set; }
    [JsonProperty("adult")] public bool Adult { get; set; }
    [JsonProperty("also_known_as")] public string[]? AlsoKnownAs { get; set; }
    [JsonProperty("biography")] public string? Biography { get; set; }
    [JsonProperty("birthday")] public DateTime? Birthday { get; set; }
    [JsonProperty("deathday")] public DateTime? DeathDay { get; set; }
    [JsonProperty("gender")] public string Gender { get; set; } = TmdbGender.Unknown.ToString();
    [JsonProperty("homepage")] public string? Homepage { get; set; }
    [JsonProperty("imdb_id")] public string? ImdbId { get; set; }
    [JsonProperty("known_for_department")] public string? KnownForDepartment { get; set; }
    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    [JsonProperty("place_of_birth")] public string? PlaceOfBirth { get; set; }
    [JsonProperty("popularity")] public double Popularity { get; set; }
    [JsonProperty("profile")] public string? Profile { get; set; }
    [JsonProperty("titleSort")] public string TitleSort { get; set; } = string.Empty;
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("created_at")] public DateTime CreatedAt { get; set; }
    [JsonProperty("updated_at")] public DateTime UpdatedAt { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; } = null!;

    [JsonProperty("combined_credits")] public Credits CombinedCredits { get; set; } = new();

    [JsonProperty("external_ids")] public Database.Models.TmdbPersonExternalIds? ExternalIds { get; set; }
    [JsonProperty("translations")] public TranslationsDto TranslationsDto { get; set; } = new();
    [JsonProperty("known_for")] public KnownFor[] KnownFor { get; set; } = [];
    [JsonProperty("images")] public Images Images { get; set; } = new();

    public PersonResponseItemDto(Person person)
    {
        string? biography = person.Translations
            .FirstOrDefault()?.Biography;

        Id = person.Id;
        Name = person.Name;
        Biography = !string.IsNullOrEmpty(biography)
            ? biography
            : person.Biography;
        Adult = person.Adult;
        AlsoKnownAs = person.AlsoKnownAs is null
            ? []
            : JsonConvert.DeserializeObject<string[]>(person.AlsoKnownAs);
        Birthday = person.BirthDay;
        DeathDay = person.DeathDay;
        Homepage = person.Homepage;
        ImdbId = person.ImdbId;
        KnownForDepartment = person.KnownForDepartment;
        PlaceOfBirth = person.PlaceOfBirth;
        Popularity = person.Popularity;
        Profile = person.Profile;
        ColorPalette = person.ColorPalette;
        CreatedAt = person.CreatedAt;
        UpdatedAt = person.UpdatedAt;
        ExternalIds = person.ExternalIds;
        Gender = person.Gender;
        Link = new($"/person/{Id}", UriKind.Relative);

        Images = new()
        {
            Profiles = person.Images
                .Select(image => new ImageDto(image))
                .ToArray()
        };

        CombinedCredits = new()
        {
            Cast = person.Casts
                .Select(cast => new KnownFor(cast))
                .OrderByDescending(knownFor => knownFor.Year)
                .ToArray(),

            Crew = person.Crews
                .Select(crew => new KnownFor(crew))
                .OrderByDescending(knownFor => knownFor.Year)
                .ToArray()
        };

        KnownFor = person.Casts
            .Select(crew => new KnownFor(crew))
            .Concat(person.Crews
                .Select(crew => new KnownFor(crew)))
            .OrderByDescending(knownFor => knownFor.Popularity)
            .ToArray();
    }

    public PersonResponseItemDto(TmdbPersonAppends tmdbPersonAppends, string? country)
    {
        string? biography = tmdbPersonAppends.Translations.Translations
            .FirstOrDefault(translation => translation.Iso31661 == country)?.TmdbPersonTranslationData.Overview;

        using MediaContext context = new();

        Person? person = context.People
            .Where(p => p.Id == tmdbPersonAppends.Id)
            .Include(p => p.Casts)
            .ThenInclude(c => c.Movie)
            .ThenInclude(movie => movie!.VideoFiles)
            .Include(p => p.Casts)
            .ThenInclude(c => c.Tv)
            .ThenInclude(tv => tv!.Episodes)
            .ThenInclude(episode => episode.VideoFiles)
            .Include(p => p.Crews)
            .ThenInclude(c => c.Movie)
            .ThenInclude(movie => movie!.VideoFiles)
            .Include(p => p.Crews)
            .ThenInclude(c => c.Tv)
            .ThenInclude(tv => tv!.Episodes)
            .ThenInclude(episode => episode.VideoFiles)
            .FirstOrDefault();

        Id = tmdbPersonAppends.Id;
        Name = tmdbPersonAppends.Name;
        Biography = !string.IsNullOrEmpty(biography)
            ? biography
            : tmdbPersonAppends.Biography;
        Adult = tmdbPersonAppends.Adult;
        AlsoKnownAs = tmdbPersonAppends.AlsoKnownAs;
        Birthday = tmdbPersonAppends.BirthDay;
        DeathDay = tmdbPersonAppends.DeathDay;
        Homepage = tmdbPersonAppends.Homepage?.ToString();
        ImdbId = tmdbPersonAppends.ImdbId;
        KnownForDepartment = tmdbPersonAppends.KnownForDepartment;
        PlaceOfBirth = tmdbPersonAppends.PlaceOfBirth;
        Popularity = tmdbPersonAppends.Popularity;
        Profile = tmdbPersonAppends.ProfilePath;
        ColorPalette = new();
        ExternalIds = tmdbPersonAppends.ExternalIds;
        Gender = Enum.Parse<TmdbGender>(tmdbPersonAppends.TmdbGender.ToString(), true).ToString();
        Link = new($"/person/{Id}", UriKind.Relative);

        Images = new()
        {
            Profiles = tmdbPersonAppends.Images.Profiles
                .Select(image => new ImageDto(image))
                .ToArray()
        };

        CombinedCredits = new()
        {
            Cast = tmdbPersonAppends.CombinedCredits.Cast
                .Select(cast => new KnownFor(cast, person))
                .OrderByDescending(knownFor => knownFor.Year)
                .ToArray(),

            Crew = tmdbPersonAppends.CombinedCredits.Crew
                .Select(crew => new KnownFor(crew, person))
                .OrderByDescending(knownFor => knownFor.Year)
                .ToArray()
        };

        KnownFor[] cast = tmdbPersonAppends.CombinedCredits.Cast
            .Select(cast => new KnownFor(cast, person))
            .DistinctBy(knownFor => knownFor.Id)
            .ToArray();

        KnownFor[] crew = tmdbPersonAppends.CombinedCredits.Crew
            .Select(crew => new KnownFor(crew, person))
            .DistinctBy(knownFor => knownFor.Id)
            .ToArray();

        KnownFor = cast.Concat(crew)
            .OrderByDescending(knownFor => knownFor.VoteCount)
            .ToArray();
    }
}
