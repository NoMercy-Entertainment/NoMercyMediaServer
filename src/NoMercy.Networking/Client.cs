using Microsoft.AspNetCore.SignalR;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Libraries;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;
using NoMercy.Database.Models.Users;

namespace NoMercy.Networking;

public class Client : Device
{
    public Guid Sub { get; set; }
    public int? Ping { get; set; }
    public ISingleClientProxy Socket { get; set; } = null!;
    public string Endpoint { get; set; } = string.Empty;
}