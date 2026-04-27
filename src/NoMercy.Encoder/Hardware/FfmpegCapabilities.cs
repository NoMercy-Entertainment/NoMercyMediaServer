namespace NoMercy.Encoder.Hardware;

using System.Text.RegularExpressions;
using NoMercy.Encoder.Infrastructure;
using NoMercy.NmSystem.Information;

public partial class FfmpegCapabilities(IProcessRunner processRunner) : IFfmpegCapabilities
{
    private HashSet<string> _encoders = [];
    private HashSet<string> _decoders = [];
    private HashSet<string> _filters = [];
    private HashSet<string> _protocols = [];

    public IReadOnlySet<string> AvailableEncoders => _encoders;
    public IReadOnlySet<string> AvailableDecoders => _decoders;
    public IReadOnlySet<string> AvailableFilters => _filters;
    public IReadOnlySet<string> AvailableProtocols => _protocols;

    public bool HasEncoder(string name) => _encoders.Contains(name);

    public bool HasFilter(string name) => _filters.Contains(name);

    public bool HasProtocol(string name) => _protocols.Contains(name);

    public async Task ProbeAsync(CancellationToken ct = default)
    {
        _encoders = await ProbeListAsync("-encoders", EncoderPattern(), ct);
        _decoders = await ProbeListAsync("-decoders", EncoderPattern(), ct);
        _filters = await ProbeListAsync("-filters", FilterPattern(), ct);
        _protocols = await ProbeListAsync("-protocols", ProtocolPattern(), ct);
    }

    private async Task<HashSet<string>> ProbeListAsync(
        string flag,
        Regex pattern,
        CancellationToken ct
    )
    {
        // Use the bundled NoMercy ffmpeg fork — a stock ffmpeg on PATH (or no
        // ffmpeg on PATH at all) would otherwise mask custom protocols the
        // fork ships, like libbluray and dvdread.
        string ffmpegPath = File.Exists(AppFiles.FfmpegPath) ? AppFiles.FfmpegPath : "ffmpeg";
        ProcessResult result = await processRunner.RunAsync(ffmpegPath, [flag], null, ct);
        HashSet<string> names = [];
        foreach (string line in result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            Match match = pattern.Match(line.Trim());
            if (match.Success)
                names.Add(match.Groups["name"].Value);
        }

        return names;
    }

    [GeneratedRegex(@"^\s*[VASD][F.][S.][X.][B.][D.]\s+(?<name>\S+)")]
    private static partial Regex EncoderPattern();

    [GeneratedRegex(@"^\s*[T.][S.][C.]\s+(?<name>\S+)")]
    private static partial Regex FilterPattern();

    [GeneratedRegex(@"^\s+(?<name>\S+)$")]
    private static partial Regex ProtocolPattern();
}
