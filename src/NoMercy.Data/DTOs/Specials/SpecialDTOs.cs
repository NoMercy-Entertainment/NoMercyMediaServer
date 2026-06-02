namespace NoMercy.Data.DTOs.Specials;

public class SpecialCardDto
{
    public Ulid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TitleSort { get; set; } = string.Empty;
    public string? Overview { get; set; }
    public string? Poster { get; set; }
    public string? Backdrop { get; set; }
    public string? Logo { get; set; }
    public string? ColorPalette { get; set; }
    public DateTime CreatedAt { get; set; }
    public int NumberOfItems { get; set; }
    public int HaveMovies { get; set; }
    public int HaveEpisodes { get; set; }
    public string? CertificationRating { get; set; }
    public string? CertificationCountry { get; set; }
}

public class SpecialDetailDto
{
    public Ulid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TitleSort { get; set; } = string.Empty;
    public string? Overview { get; set; }
    public string? Backdrop { get; set; }
    public string? Logo { get; set; }
    public string? Poster { get; set; }
    public string ColorPalette { get; set; } = string.Empty;
    public bool Favorite { get; set; }
    public int NumberOfItems { get; set; }
    public int HaveMovies { get; set; }
    public int HaveEpisodes { get; set; }
    public List<SpecialItemRefDto> Items { get; set; } = [];
}

public class SpecialItemRefDto
{
    public int? MovieId { get; set; }
    public int? EpisodeId { get; set; }
    public int TvId { get; set; }
}

public class SpecialMovieProjection
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Overview { get; set; }
    public string? Backdrop { get; set; }
    public string? Poster { get; set; }
    public string ColorPalette { get; set; } = string.Empty;
    public DateTime? ReleaseDate { get; set; }
    public int? Runtime { get; set; }
    public double? VoteAverage { get; set; }
    public string? Video { get; set; }
    public string? Logo { get; set; }
    public int VideoFileCount { get; set; }
    public string? CertificationRating { get; set; }
    public string? CertificationCountry { get; set; }
    public List<SpecialGenreProjection> Genres { get; set; } = [];
    public List<SpecialImageProjection> Backdrops { get; set; } = [];
    public List<SpecialImageProjection> Posters { get; set; } = [];
    public List<SpecialCastProjection> Cast { get; set; } = [];
    public List<SpecialCrewProjection> Crew { get; set; } = [];
}

public class SpecialTvProjection
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Overview { get; set; }
    public string? Backdrop { get; set; }
    public string? Poster { get; set; }
    public string ColorPalette { get; set; } = string.Empty;
    public DateTime? FirstAirDate { get; set; }
    public int? Duration { get; set; }
    public double? VoteAverage { get; set; }
    public string? Trailer { get; set; }
    public string? Logo { get; set; }
    public int NumberOfEpisodes { get; set; }
    public int HaveEpisodes { get; set; }
    public int[] EpisodeIds { get; set; } = [];
    public List<string?> EpisodeDurations { get; set; } = [];
    public string? CertificationRating { get; set; }
    public string? CertificationCountry { get; set; }
    public List<SpecialGenreProjection> Genres { get; set; } = [];
    public List<SpecialImageProjection> Backdrops { get; set; } = [];
    public List<SpecialImageProjection> Posters { get; set; } = [];
    public List<SpecialCastProjection> Cast { get; set; } = [];
    public List<SpecialCrewProjection> Crew { get; set; } = [];
}

public class SpecialGenreProjection
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class SpecialImageProjection
{
    public int Id { get; set; }
    public string? Site { get; set; }
    public string? FilePath { get; set; }
    public int Width { get; set; }
    public string? Type { get; set; }
    public int Height { get; set; }
    public string? Iso6391 { get; set; }
    public double VoteAverage { get; set; }
    public int VoteCount { get; set; }
    public string? ColorPalette { get; set; }
}

public class SpecialCastProjection
{
    public int PersonId { get; set; }
    public string PersonName { get; set; } = string.Empty;
    public string? PersonProfile { get; set; }
    public string? PersonKnownForDepartment { get; set; }
    public string? PersonColorPalette { get; set; }
    public DateTime? PersonDeathDay { get; set; }
    public string PersonGender { get; set; } = string.Empty;
    public string? Character { get; set; }
    public int? Order { get; set; }
}

public class SpecialCrewProjection
{
    public int PersonId { get; set; }
    public string PersonName { get; set; } = string.Empty;
    public string? PersonProfile { get; set; }
    public string? PersonKnownForDepartment { get; set; }
    public string? PersonColorPalette { get; set; }
    public DateTime? PersonDeathDay { get; set; }
    public string PersonGender { get; set; } = string.Empty;
    public string? Task { get; set; }
    public int? Order { get; set; }
}
