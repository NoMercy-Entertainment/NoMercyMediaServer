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

using Microsoft.Extensions.Logging;
using MovieFileLibrary;

namespace NoMercy.MediaProcessing.Files.Parsing;

/// <summary>
/// Ordered filename parser. Adapters are supplied by DI (so plugins extend the set)
/// and ordered by <see cref="FilenameParsingOptions"/> overrides first, then their
/// default <see cref="IFilenameParseAdapter.Order"/>. The first adapter to produce a
/// result wins.
/// </summary>
public sealed class FilenameParserPipeline : IFilenameParserPipeline
{
    private readonly IReadOnlyList<IFilenameParseAdapter> _adapters;
    private readonly ILogger<FilenameParserPipeline>? _logger;

    public FilenameParserPipeline(
        IEnumerable<IFilenameParseAdapter> adapters,
        FilenameParsingOptions? options = null,
        ILogger<FilenameParserPipeline>? logger = null
    )
    {
        options ??= new();
        _logger = logger;
        _adapters = adapters
            .Where(predicate: adapter => !options.Disabled.Contains(item: adapter.Name))
            .OrderBy(keySelector: adapter =>
            {
                int index = options.Order.FindIndex(match: name =>
                    string.Equals(a: name, b: adapter.Name, comparisonType: StringComparison.OrdinalIgnoreCase)
                );
                return index >= 0 ? index : int.MaxValue;
            })
            .ThenBy(keySelector: adapter => adapter.Order)
            .ToList();
    }

    /// <summary>The effective adapter order, for diagnostics.</summary>
    public IReadOnlyList<string> Order => _adapters.Select(selector: adapter => adapter.Name).ToList();

    public MovieFile Parse(ParseContext context)
    {
        foreach (IFilenameParseAdapter adapter in _adapters)
        {
            MovieFile? result = adapter.TryParse(context: context);
            if (result is not null)
            {
                _logger?.LogDebug(
                    message: "Filename '{File}' parsed by adapter {Adapter}", args: [context.FileNameWithExtension, adapter.Name]
                );
                return result;
            }
        }

        // Every adapter disabled — return an empty, unmatched result.
        return new(filePath: context.Title);
    }
}
