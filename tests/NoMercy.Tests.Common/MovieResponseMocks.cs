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

using System.Reflection;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.Providers.TMDB.Models.Movies;

namespace NoMercy.Tests.Common;

/// <summary>
/// Supplies the large recorded TMDB "movie appends" payload for tests, loaded
/// from an embedded JSON resource so the ~380 KB sample no longer ships inside
/// the production NoMercy.Providers assembly.
/// </summary>
public class MovieResponseMocks
{
    public TmdbMovieAppends? MockMovieAppendsResponse()
    {
        return MovieAppendsResponseJson().FromJson<TmdbMovieAppends>();
    }

    /// <summary>The raw recorded JSON (for HTTP-level response mocking).</summary>
    public static string MovieAppendsResponseJson()
    {
        return LoadResource(fileName: "MovieAppendsResponse.json");
    }

    private static string LoadResource(string fileName)
    {
        Assembly assembly = typeof(MovieResponseMocks).Assembly;
        string resourceName = assembly
            .GetManifestResourceNames()
            .Single(predicate: name => name.EndsWith(value: fileName, comparisonType: StringComparison.Ordinal));

        using Stream stream = assembly.GetManifestResourceStream(name: resourceName)!;
        using StreamReader reader = new(stream: stream);
        return reader.ReadToEnd();
    }
}
