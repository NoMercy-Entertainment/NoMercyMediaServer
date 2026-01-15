using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace NoMercy.Database.Models;

[Index(nameof(Name))]
[PrimaryKey(nameof(Id))]
public class MusicGenre
{
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    [JsonProperty("id")]
    public Guid Id { get; set; }

    [JsonProperty("name")] public string Name { get; set; } = string.Empty;
    
    public ICollection<AlbumMusicGenre> AlbumMusicGenres { get; set; } = [];
    public ICollection<ArtistMusicGenre> ArtistMusicGenres { get; set; } = [];
    public ICollection<MusicGenreTrack> MusicGenreTracks { get; set; } = [];
    public ICollection<MusicGenreReleaseGroup> MusicGenreReleaseGroups { get; set; } = [];

    public MusicGenre()
    {
        //
    }

    // public MusicGenre(Providers.MusicBrainz.Models.MusicBrainzGenre musicBrainzGenre)
    // {
    //     Id = musicBrainzGenre.Id;
    //     Name = musicBrainzGenre.Name;
    // }
}