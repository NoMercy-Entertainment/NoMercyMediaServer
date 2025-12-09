using System.Diagnostics;

namespace NoMercy.NmSystem.Dto;

public sealed class ExecutorHandle
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string? JobId { get; init; }
    public string Executable { get; init; } = string.Empty;
    public string Arguments { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    // Runtime-only
    public Process? Process { get; set; }
    public CancellationTokenSource Cancellation { get; init; } = new();
    public Task<ExecResult>? RunningTask { get; set; }
    public ExecutorState State { get; set; } = ExecutorState.Created;
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    // Minimal helper
    public int? Pid
    {
        get
        {
            try
            {
                // Accessing Id can throw if the Process has been disposed or not associated
                return Process?.Id;
            }
            catch (InvalidOperationException)
            {
                return null;
            }
        }
    }
}