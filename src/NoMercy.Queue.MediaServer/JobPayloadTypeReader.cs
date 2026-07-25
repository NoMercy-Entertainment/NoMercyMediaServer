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

using System.Text.Json;

namespace NoMercy.Queue.MediaServer;

/// <summary>
/// Best-effort extraction of the job class name from a serialized queue job
/// payload, for log messages only. <see cref="SerializationHelper"/> writes
/// Newtonsoft's <c>TypeNameHandling.Objects</c> discriminator as a
/// <c>"$type": "Namespace.JobClass, AssemblyName"</c> property; this reads
/// just that property without deserializing (and therefore without
/// instantiating) the job.
/// </summary>
internal static class JobPayloadTypeReader
{
    private const string UnknownTypeName = "unknown";

    public static string ReadShortTypeName(string payload)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(payload);
            if (
                !document.RootElement.TryGetProperty("$type", out JsonElement typeElement)
                || typeElement.ValueKind != JsonValueKind.String
            )
                return UnknownTypeName;

            string? rawTypeName = typeElement.GetString();
            if (string.IsNullOrEmpty(rawTypeName))
                return UnknownTypeName;

            string withoutAssembly = rawTypeName.Split(',')[0];
            int lastDot = withoutAssembly.LastIndexOf('.');
            return lastDot >= 0 ? withoutAssembly[(lastDot + 1)..] : withoutAssembly;
        }
        catch (JsonException)
        {
            return UnknownTypeName;
        }
    }
}
