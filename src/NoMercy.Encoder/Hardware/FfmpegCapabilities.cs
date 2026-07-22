// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

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

    public bool HasEncoder(string name) => _encoders.Contains(item: name);

    public bool HasDemuxer(string name) => _demuxers.Contains(item: name);

    public bool HasFilter(string name) => _filters.Contains(item: name);

    public bool HasProtocol(string name) => _protocols.Contains(item: name);

    public async Task ProbeAsync(CancellationToken ct = default)
    {
        _encoders = await ProbeListAsync(flag: "-encoders", pattern: EncoderPattern(), ct: ct);
        _decoders = await ProbeListAsync(flag: "-decoders", pattern: EncoderPattern(), ct: ct);
        _demuxers = await ProbeListAsync(flag: "-demuxers", pattern: DemuxerPattern(), ct: ct);
        _filters = await ProbeListAsync(flag: "-filters", pattern: FilterPattern(), ct: ct);
        _protocols = await ProbeListAsync(flag: "-protocols", pattern: ProtocolPattern(), ct: ct);
    }

    private async Task<HashSet<string>> ProbeListAsync(
        string flag,
        Regex pattern,
        CancellationToken ct
    )
    {
        ProcessResult result = await processRunner.RunAsync(executable: AppFiles.FfmpegPath, arguments: [flag], workingDirectory: null, cancellationToken: ct);

        if (!result.IsSuccess)
            throw new InvalidOperationException(
                message: $"ffmpeg {flag} exited with code {result.ExitCode}: {result.StdErr.Trim()}"
            );

        HashSet<string> names = [];
        foreach (string line in result.StdOut.Split(separator: '\n', options: StringSplitOptions.RemoveEmptyEntries))
        {
            Match match = pattern.Match(input: line.Trim());
            if (match.Success)
                names.Add(item: match.Groups[groupname: "name"].Value);
        }

        return names;
    }

    [GeneratedRegex(pattern: @"^[VASD][F.][S.][X.][B.][D.]\s+(?<name>\S+)")]
    private static partial Regex EncoderPattern();

    // Demuxer rows: "D   3dostr             3DO STR". The flag column is
    // 3 chars (D.. / .E. / ..d), real rows often print just "D" padded
    // with spaces. Exclude legend rows (third column is "=") by requiring
    // the name to start with [a-z0-9] and to be followed by a non-empty
    // description token.
    [GeneratedRegex(pattern: @"^[DE.d ]+\s+(?<name>[a-z0-9][a-z0-9_]*)\s+\S")]
    private static partial Regex DemuxerPattern();

    // Filter rows: optional flag chars (NoMercy fork shows 2, stock ffmpeg
    // shows 3), then a name, then a "AA->A" / "VV->V" / "N->V" type
    // signature. The signature requirement is what excludes legend rows
    // like "T.. = Timeline support" without needing a separate skip list.
    [GeneratedRegex(pattern: @"^[TSC.]+\s+(?<name>\S+)\s+[VANS|]+->[VANS|]+")]
    private static partial Regex FilterPattern();

    // Protocols print one identifier per line under "Input:" / "Output:"
    // headers. Match lowercase identifiers only — the headers (capitalised,
    // colon-suffixed) and the "Supported file protocols:" preamble are
    // automatically excluded.
    [GeneratedRegex(pattern: @"^(?<name>[a-z][a-z0-9_]*)$")]
    private static partial Regex ProtocolPattern();
}
