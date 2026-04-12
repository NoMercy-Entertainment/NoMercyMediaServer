namespace NoMercy.Encoder.V3.Progress;

public class ProgressAggregator
{
    private readonly double[] _weights;
    private readonly double[] _groupProgress;
    private readonly double _totalWeight;

    public ProgressAggregator(TimeSpan[] estimatedDurations)
    {
        _weights = estimatedDurations.Select(d => d.TotalSeconds).ToArray();
        _groupProgress = new double[_weights.Length];
        _totalWeight = _weights.Sum();
    }

    public void UpdateGroup(int groupIndex, double percentage)
    {
        if (groupIndex >= 0 && groupIndex < _groupProgress.Length)
            _groupProgress[groupIndex] = Math.Clamp(percentage, 0, 100);
    }

    public double OverallPercentage
    {
        get
        {
            if (_totalWeight <= 0)
                return 0;
            double weighted = 0;
            for (int i = 0; i < _groupProgress.Length; i++)
                weighted += _groupProgress[i] * _weights[i];
            return weighted / _totalWeight;
        }
    }

    public TimeSpan? EstimatedRemaining(TimeSpan elapsed)
    {
        double percent = OverallPercentage;
        if (percent <= 0)
            return null;
        double totalEstimatedSeconds = elapsed.TotalSeconds / (percent / 100.0);
        double remainingSeconds = totalEstimatedSeconds - elapsed.TotalSeconds;
        return remainingSeconds > 0 ? TimeSpan.FromSeconds(remainingSeconds) : TimeSpan.Zero;
    }
}
