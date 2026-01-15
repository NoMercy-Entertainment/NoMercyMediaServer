namespace NoMercy.Queue;

public interface IShouldQueue
{
    Task Handle();
}