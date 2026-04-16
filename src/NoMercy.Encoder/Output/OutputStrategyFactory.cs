namespace NoMercy.Encoder.Output;

using NoMercy.Encoder.Codecs;

public class OutputStrategyFactory(IEnumerable<IOutputStrategy> strategies) : IOutputStrategyFactory
{
    // Walk in reverse so later DI registrations (plugins) shadow built-ins.
    private readonly IReadOnlyList<IOutputStrategy> _strategies = strategies.Reverse().ToList();

    public IOutputStrategy Resolve(OutputFormat format)
    {
        foreach (IOutputStrategy strategy in _strategies)
        {
            if (strategy.Format == format)
                return strategy;
        }

        throw new ArgumentOutOfRangeException(
            nameof(format),
            format,
            $"No IOutputStrategy is registered for format {format}"
        );
    }
}
