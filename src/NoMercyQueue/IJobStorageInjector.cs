using Microsoft.Extensions.DependencyInjection;

namespace NoMercyQueue;

/// <summary>
/// Implemented by queue jobs that need services resolved from the DI
/// container before <c>Handle()</c> is called. The queue worker opens a
/// fresh <see cref="IServiceScope"/> per job execution, calls this method
/// to let the job pull whatever it needs, and disposes the scope when the
/// job finishes (success or failure).
/// </summary>
public interface IJobStorageInjector
{
    void InjectStorageServices(IServiceProvider serviceProvider);
}
