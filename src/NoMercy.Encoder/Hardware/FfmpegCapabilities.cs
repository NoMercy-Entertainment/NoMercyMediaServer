using System.Text.RegularExpressions;
using NoMercy.Encoder.Infrastructure;
using NoMercy.NmSystem.Information;

namespace NoMercy.Encoder.Hardware;

public partial class FfmpegCapabilities(IProcessRunner processRunner) : IFfmpegCapabilities
{
    private HashSet<string> _encoders = [];
    private HashSet<string> _decoders = [];
    private HashSet<string> _demuxers = [];
    private HashSet<string> _filters = [];
    private HashSet<string> _protocols = [];

    public IReadOnlySet<string> AvailableEncoders => _encoders;
    public IReadOnlySet<string> AvailableDecoders => _decoders;
    public IReadOnlySet<string> AvailableDemuxers => _demuxers;
    public IReadOnlySet<string> AvailableFilters => _filters;
    public IReadOnlySet<string> AvailableProtocols => _protocols;

    public bool HasEncoder(string name) => _encoders.Contains(name);

    public bool HasDemuxer(string name) => _demuxers.Contains(name);

    public bool HasFilter(string name) => _filters.Contains(name);

    public bool HasProtocol(string name) => _protocols.Contains(name);

    public async Task ProbeAsync(CancellationToken ct = default)
    {
        _encoders = await ProbeListAsync("-encoders", EncoderPattern(), ct);
        _decoders = await ProbeListAsync("-decoders", EncoderPattern(), ct);
        _demuxers = await ProbeListAsync("-demuxers", DemuxerPattern(), ct);
        _filters = await ProbeListAsync("-filters", FilterPattern(), ct);
        _protocols = await ProbeListAsync("-protocols", ProtocolPattern(), ct);
    }

    private async Task<HashSet<string>> ProbeListAsync(
        string flag,
        Regex pattern,
        CancellationToken ct
    )
    {
        ProcessResult result = await processRunner.RunAsync(AppFiles.FfmpegPath, [flag], null, ct);
        HashSet<string> names = [];
        foreach (string line in result.StdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            Match match = pattern.Match(line.Trim());
            if (match.Success)
                names.Add(match.Groups["name"].Value);
        }

        return names;
    }

    [GeneratedRegex(@"^[VASD][F.][S.][X.][B.][D.]\s+(?<name>\S+)")]
    private static partial Regex EncoderPattern();

    // Demuxer rows: "D   3dostr             3DO STR". The flag column is
    // 3 chars (D.. / .E. / ..d), real rows often print just "D" padded
    // with spaces. Exclude legend rows (third column is "=") by requiring
    // the name to start with [a-z0-9] and to be followed by a non-empty
    // description token.
    [GeneratedRegex(@"^[DE.d ]+\s+(?<name>[a-z0-9][a-z0-9_]*)\s+\S")]
    private static partial Regex DemuxerPattern();

    // Filter rows: optional flag chars (NoMercy fork shows 2, stock ffmpeg
    // shows 3), then a name, then a "AA->A" / "VV->V" / "N->V" type
    // signature. The signature requirement is what excludes legend rows
    // like "T.. = Timeline support" without needing a separate skip list.
    [GeneratedRegex(@"^[TSC.]+\s+(?<name>\S+)\s+[VANS|]+->[VANS|]+")]
    private static partial Regex FilterPattern();

    // Protocols print one identifier per line under "Input:" / "Output:"
    // headers. Match lowercase identifiers only — the headers (capitalised,
    // colon-suffixed) and the "Supported file protocols:" preamble are
    // automatically excluded.
    [GeneratedRegex(@"^(?<name>[a-z][a-z0-9_]*)$")]
    private static partial Regex ProtocolPattern();
}
