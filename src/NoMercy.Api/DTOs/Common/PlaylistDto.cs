using Newtonsoft.Json;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.Users;

namespace NoMercy.Api.DTOs.Common;

public class PlaylistDto
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonProperty("name")] public string Name { get; set; }
    [JsonProperty("description")] public string? Description { get; set; }
    [JsonProperty("cover")] public string? Cover { get; set; }
    [JsonProperty("filename")] public string? Filename { get; set; }
    [JsonProperty("duration")] public string? Duration { get; set; }

    [JsonProperty("user_id")] public Guid UserId { get; set; }
    [JsonProperty("user")] public User User { get; set; }

    [JsonProperty("playlist_track")] public ICollection<PlaylistTrack> Tracks { get; set; }
    
    public PlaylistDto(Playlist playlist)
    {
        Id = playlist.Id;
        Name = playlist.Name;
        Description = playlist.Description;
        Cover = playlist.Cover;
        Cover = Cover is not null ? new Uri($"/images/music{Cover}", UriKind.Relative).ToString() : null;
        Filename = playlist.Filename;
        Duration = playlist.Duration;
        UserId = playlist.UserId;
        User = playlist.User;
        Tracks = playlist.Tracks;
    }

}