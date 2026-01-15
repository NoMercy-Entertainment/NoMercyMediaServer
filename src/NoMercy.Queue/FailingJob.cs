// ReSharper disable All

using NoMercy.Queue;

namespace NoMercy.Server.system;

public class FailingJob : IShouldQueue
{
    private readonly string _param1;

    public FailingJob(string param1)
    {
        _param1 = param1;
    }

    public Task Handle()
    {
        throw new Exception($"This job always fails. the argument was {_param1}");
    }
}