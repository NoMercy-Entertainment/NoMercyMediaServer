namespace NoMercy.EncoderV2.Core.Contracts;

public interface ITaskContract
{
    Task Run(CancellationTokenSource cts);
}