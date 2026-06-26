namespace NoMercy.Setup.Boot;

public class DeferredTasks
{
    public bool ApiKeysLoaded { get; set; }
    public bool Authenticated { get; set; }
    public bool NetworkDiscovered { get; set; }
    public bool Registered { get; set; }
    public bool SeedsRun { get; set; }
    public bool AllCompleted { get; set; }
}
