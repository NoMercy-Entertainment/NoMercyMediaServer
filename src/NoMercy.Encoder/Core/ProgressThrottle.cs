namespace NoMercy.Encoder.Core;

/// <summary>
/// Throttles progress updates to a configurable interval.
/// Ensures at most one update per interval while always allowing
/// "final" updates through unthrottled.
/// </summary>
internal class ProgressThrottle
{
    private readonly int _intervalMs;
    private DateTime _lastUpdate = DateTime.MinValue;

    internal ProgressThrottle(int intervalMs = 500)
    {
        _intervalMs = intervalMs;
    }

    /// <summary>
    /// Returns true if enough time has elapsed since the last allowed update.
    /// Automatically records the timestamp when returning true.
    /// </summary>
    internal bool ShouldSend()
    {
        DateTime now = DateTime.UtcNow;
        if ((now - _lastUpdate).TotalMilliseconds < _intervalMs)
            return false;

        _lastUpdate = now;
        return true;
    }

    /// <summary>
    /// Resets the throttle so the next call to ShouldSend always returns true.
    /// </summary>
    internal void Reset()
    {
        _lastUpdate = DateTime.MinValue;
    }
}
