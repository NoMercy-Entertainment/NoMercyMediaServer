namespace NoMercy.Queue.Core.Interfaces;

public interface IShouldQueue
{
    string QueueName { get; }
    int Priority { get; }
    Task Handle();
}
