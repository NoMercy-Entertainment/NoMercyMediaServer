using CoreIShouldQueue = NoMercy.Queue.Core.Interfaces.IShouldQueue;

namespace NoMercy.Queue;

/// <summary>
/// Re-exports the canonical IShouldQueue from NoMercy.Queue.Core.
/// Existing code that references NoMercy.Queue.IShouldQueue continues to compile.
/// </summary>
public interface IShouldQueue : CoreIShouldQueue
{
}
