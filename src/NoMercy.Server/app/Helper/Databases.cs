using NoMercy.Database;

namespace NoMercy.Server.app.Helper;

public static class Databases
{
    internal static QueueContext QueueContext { get; set; } = new();
    internal static MediaContext MediaContext { get; set; } = new();
}
