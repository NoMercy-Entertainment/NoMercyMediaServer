namespace NoMercy.Encoder.Output;

using NoMercy.Encoder.Codecs;

public interface IOutputStrategyFactory
{
    /// <summary>
    /// Resolves the <see cref="IOutputStrategy"/> registered for the given
    /// output format. Plugins can replace built-in strategies by registering
    /// an <see cref="IOutputStrategy"/> for the same <see cref="OutputFormat"/>
    /// — the last registration wins, matching how <c>IEncodingStrategy</c>
    /// resolution works.
    /// </summary>
    IOutputStrategy Resolve(OutputFormat format);
}
