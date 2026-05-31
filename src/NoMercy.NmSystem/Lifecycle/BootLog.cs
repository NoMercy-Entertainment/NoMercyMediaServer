using System.Diagnostics;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.NmSystem.Lifecycle;

/// <summary>
/// Structured boot-phase helper. Wraps a named phase with a header, elapsed-time
/// footer, and a global <see cref="IsBootInProgress"/> flag that callers (Http,
/// Socket, Ping loggers) use to demote chattiness during startup.
/// </summary>
public static class BootLog
{
    private static volatile bool _bootInProgress = true;

    /// <summary>
    /// True from process start until <see cref="Complete"/> is called.
    /// Http/Socket/Ping loggers check this to demote to Debug during boot.
    /// </summary>
    public static bool IsBootInProgress => _bootInProgress;

    // Phase timings for the boot-complete summary.
    private static readonly List<(string Name, TimeSpan Elapsed)> _phaseTimings = [];
    private static readonly object _lock = new();

    /// <summary>
    /// Begins a named boot phase. Logs a header immediately and returns a
    /// <see cref="PhaseScope"/> that logs a footer on dispose. Default footer
    /// glyph is ✓; callers can flip to ✗ by calling
    /// <see cref="PhaseScope.Fail"/> from a catch block before the using-scope
    /// disposes.
    /// </summary>
    public static PhaseScope Phase(string name)
    {
        return new PhaseScope(name);
    }

    /// <summary>
    /// Marks boot complete. Logs the summary line with per-phase timings and
    /// clears <see cref="IsBootInProgress"/> so Http/Socket/Ping chatter returns
    /// to normal Info level.
    /// </summary>
    public static void Complete()
    {
        _bootInProgress = false;

        (string Name, TimeSpan Elapsed)[] timings;
        lock (_lock)
        {
            timings = [.. _phaseTimings];
        }

        if (timings.Length == 0)
            return;

        double totalSeconds = timings.Sum(t => t.Elapsed.TotalSeconds);
        string phases = string.Join(
            " · ",
            timings.Select(t => $"{t.Name}: {t.Elapsed.TotalSeconds:F1}s")
        );

        Logger.App($"Boot complete in {totalSeconds:F1}s");
        Logger.App($"  {phases}");
    }

    public sealed class PhaseScope : IDisposable
    {
        private readonly string _name;
        private readonly Stopwatch _sw;
        private bool _disposed;
        private string? _failureReason;

        internal PhaseScope(string name)
        {
            _name = name;
            _sw = Stopwatch.StartNew();
            Logger.Setup($"▸ {name}");
        }

        /// <summary>
        /// Marks this phase as failed. The Dispose footer logs ✗ instead of ✓
        /// and includes the optional reason for triage. Call from a catch
        /// block before the using-scope unwinds.
        /// </summary>
        public void Fail(string? reason = null)
        {
            _failureReason = reason ?? "failed";
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _sw.Stop();

            if (_failureReason is null)
            {
                Logger.Setup($"  ✓ {_name} ready ({_sw.Elapsed.TotalSeconds:F1}s)");
            }
            else
            {
                Logger.Setup($"  ✗ {_name} {_failureReason} ({_sw.Elapsed.TotalSeconds:F1}s)");
            }

            lock (_lock)
            {
                _phaseTimings.Add((_name, _sw.Elapsed));
            }
        }
    }
}
