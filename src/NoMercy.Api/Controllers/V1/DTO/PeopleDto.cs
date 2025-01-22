using Newtonsoft.Json;
using NoMercy.Api.Controllers.V1.Media.DTO;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.Providers.TMDB.Models.Shared;
using TmdbGender=NoMercy.Providers.TMDB.Models.People.TmdbGender;

namespace NoMercy.Api.Controllers.V1.DTO;
public record PeopleDto
{
    [JsonProperty("character")] public string? Character { get; set; }
    [JsonProperty("job")] public string? Job { get; set; }
    [JsonProperty("profile")] public string? ProfilePath { get; set; }
    [JsonProperty("gender")] public string Gender { get; set; }
    [JsonProperty("id")] public long Id { get; set; }
    [JsonProperty("known_for_department")] public string? KnownForDepartment { get; set; }
    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("popularity")] public double Popularity { get; set; }
    [JsonProperty("deathday")] public DateTime? DeathDay { get; set; }
    [JsonProperty("translations")] public TranslationDto[] Translations { get; set; }
    [JsonProperty("order")] public int? Order { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; }

    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }

    public PeopleDto(Cast cast)
    {
        Id = cast.Person.Id;
        Character = cast.Role.Character;
        ProfilePath = cast.Person.Profile;
        KnownForDepartment = cast.Person.KnownForDepartment;
        Name = cast.Person.Name;
        ColorPalette = cast.Person.ColorPalette;
        DeathDay = cast.Person.DeathDay;
        Gender = cast.Person.Gender;
        Order = cast.Role.Order;
        Link = new($"/person/{cast.Person.Id}", UriKind.Relative);
        Translations = [];
    }

    public PeopleDto(TmdbCast tmdbCast)
    {
        Id = tmdbCast.Id;
        Character = tmdbCast.Character;
        ProfilePath = tmdbCast.ProfilePath;
        KnownForDepartment = tmdbCast.KnownForDepartment;
        Name = tmdbCast.Name ?? string.Empty;
        ColorPalette = new();
        Gender = Enum.Parse<TmdbGender>(tmdbCast.Gender.ToString(), true).ToString();
        Order = tmdbCast.Order;
        Link = new($"/person/{tmdbCast.Id}", UriKind.Relative);
        Translations = [];
    }

    public PeopleDto(Crew crew)
    {
        Id = crew.Person.Id;
        Job = crew.Job.Task;
        ProfilePath = crew.Person.Profile;
        KnownForDepartment = crew.Person.KnownForDepartment;
        Name = crew.Person.Name;
        ColorPalette = crew.Person.ColorPalette;
        DeathDay = crew.Person.DeathDay;
        Gender = crew.Person.Gender;
        Order = crew.Job.Order;
        Link = new($"/person/{crew.Person.Id}", UriKind.Relative);
        Translations = [];
    }

    public PeopleDto(TmdbCrew tmdbCrew)
    {
        Id = tmdbCrew.Id;
        Job = tmdbCrew.Job;
        ProfilePath = tmdbCrew.ProfilePath;
        KnownForDepartment = tmdbCrew.KnownForDepartment;
        Name = tmdbCrew.Name;
        ColorPalette = new();
        Gender = Enum.Parse<TmdbGender>(tmdbCrew.Gender.ToString(), true).ToString();
        Order = tmdbCrew.Order;
        Link = new($"/person/{tmdbCrew.Id}", UriKind.Relative);
        Translations = [];
    }

    public PeopleDto(TmdbCreatedBy crew)
    {
        Id = crew.Id;
        Job = "Creator";
        ProfilePath = crew.ProfilePath;
        Name = crew.Name;
        ColorPalette = new();
        Gender = Enum.Parse<TmdbGender>(crew.Gender.ToString(), true).ToString();
        Link = new($"/person/{crew.Id}", UriKind.Relative);
        Translations = [];
    }

    public PeopleDto(Creator creator)
    {
        Id = creator.Person.Id;
        Job = "Creator";
        ProfilePath = creator.Person.Profile;
        KnownForDepartment = creator.Person.KnownForDepartment;
        Name = creator.Person.Name;
        ColorPalette = creator.Person.ColorPalette;
        DeathDay = creator.Person.DeathDay;
        Gender = creator.Person.Gender;
        Link = new($"/person/{creator.Person.Id}", UriKind.Relative);
        Translations = [];
    }
}
