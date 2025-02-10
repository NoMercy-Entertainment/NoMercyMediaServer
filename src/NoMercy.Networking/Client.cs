using Microsoft.AspNetCore.SignalR;
using NoMercy.Database.Models;

namespace NoMercy.Networking;
public class Client : Device
{
    public Guid Sub { get; set; }
    public int? Ping { get; set; }
    public ISingleClientProxy Socket { get; set; } = null!;
    public string Endpoint { get; set; } = string.Empty;
}