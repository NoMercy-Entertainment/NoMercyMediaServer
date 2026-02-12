namespace NoMercy.Queue.Core.Interfaces;

public interface IJobDispatcher
{
    void Dispatch(IShouldQueue job);
    void Dispatch(IShouldQueue job, string onQueue, int priority);
}
