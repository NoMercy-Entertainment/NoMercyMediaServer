using NoMercy.Database.Models.Users;

namespace NoMercy.Api.Services.Music;

public class MusicLikeEventDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = null!;
    public bool Liked { get; set; }
    public User? User { get; set; }
}