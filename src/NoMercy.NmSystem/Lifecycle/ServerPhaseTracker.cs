using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace NoMercy.NmSystem.Lifecycle;

public sealed class ServerPhaseTracker : IServerPhaseTracker
{
    // Static accessor so static boot code (Setup.Start, Setup.Binaries) can advance
    // stages without threading DI through every helper. Wired by ServiceConfiguration
    // immediately after the DI container is built.
    public static IServerPhaseTracker? Current { get; private set; }

    public static void RegisterCurrent(IServerPhaseTracker tracker) => Current = tracker;

    private readonly object _lock = new();
    private readonly ILogger<ServerPhaseTracker> _logger;
    private readonly Dictionary<BootStage, TaskCompletionSource> _stageSignals = new();

    private BootStage _completed = BootStage.None;

    public ServerPhaseTracker(ILogger<ServerPhaseTracker>? logger = null)
    {
        _logger = logger ?? NullLogger<ServerPhaseTracker>.Instance;

        foreach (BootStage stage in Enum.GetValues<BootStage>())
        {
            if (stage == BootStage.None || stage == BootStage.All)
                continue;
            if (!IsSingleFlag(stage))
                continue;
            _stageSignals[stage] = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }
    }

    public BootStage CompletedStages
    {
        get
        {
            lock (_lock)
                return _completed;
        }
    }

    public bool IsComplete(BootStage stage)
    {
        lock (_lock)
            return (_completed & stage) == stage;
    }

    public event Action<BootStage>? StageCompleted;

    public void MarkComplete(BootStage stage)
    {
        if (!IsSingleFlag(stage))
            throw new ArgumentException(
                $"MarkComplete requires a single flag, got {stage}. Call MarkComplete per stage.",
                nameof(stage)
            );

        TaskCompletionSource? tcs;
        bool justAdvanced;

        lock (_lock)
        {
            justAdvanced = (_completed & stage) != stage;
            if (!justAdvanced)
                return;

            _completed |= stage;
            tcs = _stageSignals.GetValueOrDefault(stage);
        }

        _logger.LogInformation("Boot stage complete: {Stage}", stage);
        tcs?.TrySetResult();
        StageCompleted?.Invoke(stage);
    }

    public async Task WhenReachedAsync(BootStage stage, CancellationToken ct)
    {
        if (IsComplete(stage))
            return;

        List<Task> pending = [];
        lock (_lock)
        {
            foreach ((BootStage flag, TaskCompletionSource signal) in _stageSignals)
            {
                if ((stage & flag) == flag && (_completed & flag) != flag)
                    pending.Add(signal.Task);
            }
        }

        if (pending.Count == 0)
            return;

        await Task.WhenAll(pending).WaitAsync(ct).ConfigureAwait(false);
    }

    private static bool IsSingleFlag(BootStage stage)
    {
        int value = (int)stage;
        return value != 0 && (value & (value - 1)) == 0;
    }
}
