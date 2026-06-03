using NoMercy.Database.Models.Libraries;

namespace NoMercy.MediaProcessing.Inbox;

public sealed class RouteOutcome
{
    public string Mode { get; set; } = string.Empty;
    public InboxDestination? Destination { get; set; }
    public InboxItem Item { get; set; } = null!;
}
