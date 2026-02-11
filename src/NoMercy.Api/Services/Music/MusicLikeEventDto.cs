using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;

namespace NoMercy.Api.Services.Music;

public class MusicLikeEventDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = null!;
    public bool Liked { get; set; }
    public User? User { get; set; }
}