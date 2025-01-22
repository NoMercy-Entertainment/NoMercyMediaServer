using Newtonsoft.Json;
using NoMercy.Api.Controllers.V1.DTO;
using NoMercy.Database;
using NoMercy.Database.Models;
using NoMercy.NmSystem;
using NoMercy.NmSystem.Extensions;

namespace NoMercy.Api.Controllers.V1.Media.DTO;
public record SpecialResponseItemDto
{
    [JsonProperty("id")] public Ulid Id { get; set; }
    [JsonProperty("title")] public string Title { get; set; }
    [JsonProperty("overview")] public string? Overview { get; set; }
    [JsonProperty("backdrop")] public string? Backdrop { get; set; }
    [JsonProperty("poster")] public string? Poster { get; set; }
    [JsonProperty("titleSort")] public string? TitleSort { get; set; }
    [JsonProperty("type")] public string Type { get; set; }
    [JsonProperty("media_type")] public string MediaType { get; set; }
    [JsonProperty("color_palette")] public IColorPalettes? ColorPalette { get; set; }
    [JsonProperty("collection")] public IEnumerable<SpecialItemDto>? Special { get; set; }
    [JsonProperty("number_of_items")] public int? NumberOfItems { get; set; }
    [JsonProperty("have_items")] public int? HaveItems { get; set; }
    [JsonProperty("favorite")] public bool Favorite { get; set; }
    [JsonProperty("genres")] public IEnumerable<GenreDto> Genres { get; set; }
    [JsonProperty("total_duration")] public int TotalDuration { get; set; }
    [JsonProperty("link")] public Uri Link { get; set; }

    [JsonProperty("cast")] public IEnumerable<PeopleDto> Cast { get; set; }
    [JsonProperty("crew")] public IEnumerable<PeopleDto> Crew { get; set; }
    [JsonProperty("backdrops")] public IEnumerable<ImageDto> Backdrops { get; set; }
    [JsonProperty("posters")] public IEnumerable<ImageDto> Posters { get; set; }

    [JsonProperty("content_ratings")] public IEnumerable<Certification?> ContentRatings { get; set; }

    public SpecialResponseItemDto(Special special, List<SpecialItemsDto> items)
    {
        List<SpecialItemDto> specialItems = [];
        foreach (SpecialItem specialItem in special.Items)
            if (specialItem.MovieId is not null)
            {
                SpecialItemsDto? newItem = items.Find(i => i.Id == specialItem.MovieId);
                if (newItem is null) continue;

                SpecialItemDto item = new(newItem);
                specialItems.Add(item);
            }
            else
            {
                SpecialItemsDto? newItem = items.FirstOrDefault(i => i.EpisodeIds.Contains(specialItem.EpisodeId ?? 0));
                if (newItem is null) continue;

                SpecialItemDto item = new(newItem);
                specialItems.Add(item);
            }

        IEnumerable<PeopleDto> cast = items
            .SelectMany(tv => tv.Cast)
            .DistinctBy(people => people.Id)
            .ToList();

        IEnumerable<PeopleDto> crew = items
            .SelectMany(item => item.Crew)
            .DistinctBy(people => people.Id)
            .ToList();

        IEnumerable<ImageDto> posters = items
            .SelectMany(item => item.Posters)
            .ToList();

        IEnumerable<ImageDto> backdrops = items
            .SelectMany(item => item.Backdrops)
            .ToList();

        IEnumerable<GenreDto> genres = items
            .SelectMany(item => item.Genres)
            .DistinctBy(genre => genre.Id)
            .ToList();

        foreach (SpecialItemsDto item in items)
        {
            item.Posters = [];
            item.Backdrops = [];
            item.Cast = [];
            item.Crew = [];
            item.Genres = [];
        }

        Id = special.Id;
        Title = special.Title;
        Overview = special.Overview;
        Backdrop = special.Backdrop?.Replace("https://storage.nomercy.tv/laravel", "");
        Poster = special.Poster;
        TitleSort = special.Title.TitleSort();
        Type = "specials";
        MediaType = "specials";
        Link = new($"/specials/{Id}", UriKind.Relative);
        ColorPalette = special.ColorPalette;
        Backdrops = backdrops;
        Posters = posters;
        Cast = cast;
        Crew = crew;
        Genres = genres;

        Favorite = special.SpecialUser.Count != 0;

        NumberOfItems = special.Items.Count;

        int movies = special.Items.Count(item => item.MovieId is not null && item.Movie?.VideoFiles.Count != 0);
        int episodes = special.Items
            .Where(item => item.EpisodeId is not null)
            .Count(item => item.Episode?.VideoFiles?.Count != 0);

        HaveItems = movies + episodes;

        TotalDuration = items.Sum(item => item.TotalDuration);

        ContentRatings = items
            .Select(specialItem => specialItem.Rating)
            .DistinctBy(rating => rating.Iso31661);

        Special = specialItems.DistinctBy(si => si.Id);
    }

    public SpecialResponseItemDto(Special special)
    {
        Id = special.Id;
        Title = special.Title;
        Overview = special.Overview;
        Backdrop = special.Backdrop?.Replace("https://storage.nomercy.tv/laravel", "");
        Poster = special.Poster;
        TitleSort = special.Title.TitleSort();
        Type = "specials";
        MediaType = "specials";
        Link = new($"/specials/{Id}", UriKind.Relative);
        ColorPalette = special.ColorPalette;
        Favorite = special.SpecialUser?.Count != 0;
        NumberOfItems = special.Items.Count;

        int movies = special.Items.Count(item => item.MovieId is not null && (bool)item.Movie?.VideoFiles.Any());
        int episodes = special.Items.Count(item => item.EpisodeId is not null && (bool)item.Episode?.VideoFiles.Any());

        Cast = [];
        Crew = [];
        Backdrops = [];
        Posters = [];
        Genres = [];

        HaveItems = movies + episodes;

        TotalDuration = special.Items.Sum(item => item.Movie?.Runtime ?? 0);

        ContentRatings = special.Items
            .Select(specialItem => specialItem.Movie?.CertificationMovies
                .Select(certification => certification.Certification)
                .FirstOrDefault())
            .DistinctBy(rating => rating?.Iso31661);
    }
}
