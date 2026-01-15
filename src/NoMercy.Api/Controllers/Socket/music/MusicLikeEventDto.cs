using NoMercy.Database.Models;

namespace NoMercy.Api.Controllers.Socket.music;

public class MusicLikeEventDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = null!;
    public bool Liked { get; set; }
    public User? User { get; set; }
}