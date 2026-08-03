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

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace NoMercyQueue;

/// <summary>
/// Restricts deserialization to types in NoMercy.* and NoMercyQueue.* namespaces only.
/// Prevents arbitrary type instantiation from untrusted JSON payloads
/// (CWE-502, CVE-2017-9424 class of vulnerabilities in Newtonsoft TypeNameHandling.All).
/// </summary>
internal sealed class NoMercySerializationBinder : DefaultSerializationBinder
{
    private static readonly string[] AllowedNamespacePrefixes =
    [
        "NoMercy.",
        "NoMercyQueue.",
        "NoMercyQueue.Core.",
    ];

    /// <summary>
    /// The framework types a job payload legitimately carries. TypeNameHandling.Objects
    /// writes a $type for a dictionary or list nested in a job, and rejecting those
    /// outright meant any job holding one serialized fine and then died on reserve with
    /// "Deserialization of type … is not allowed" — MusicEncodeJob carries a
    /// Dictionary&lt;string, Guid&gt;, so every music encode failed and no track was ever
    /// written. These are data containers and primitives; none is a gadget, and the
    /// element types are checked on their own turn.
    /// </summary>
    private static readonly HashSet<string> AllowedSystemTypes = new(StringComparer.Ordinal)
    {
        "System.Boolean",
        "System.Byte",
        "System.Char",
        "System.Collections.Generic.Dictionary`2",
        "System.Collections.Generic.HashSet`1",
        "System.Collections.Generic.KeyValuePair`2",
        "System.Collections.Generic.List`1",
        "System.Collections.Generic.Queue`1",
        "System.Collections.Generic.SortedDictionary`2",
        "System.Collections.Generic.Stack`1",
        "System.DateTime",
        "System.DateTimeOffset",
        "System.Decimal",
        "System.Double",
        "System.Guid",
        "System.Int16",
        "System.Int32",
        "System.Int64",
        "System.Nullable`1",
        "System.Single",
        "System.String",
        "System.TimeSpan",
        "System.Uri",
        "System.Version",
    };

    public override Type BindToType(string? assemblyName, string typeName)
    {
        // Every type named anywhere in the string is checked, not only the root: a
        // permitted List`1 must not become a way to smuggle a forbidden element type in.
        foreach (string candidate in NamedTypes(typeName))
        {
            bool isAllowed =
                AllowedNamespacePrefixes.Any(prefix =>
                    candidate.StartsWith(prefix, StringComparison.Ordinal)
                ) || AllowedSystemTypes.Contains(candidate);

            if (!isAllowed)
                throw new JsonSerializationException(
                    $"Deserialization of type '{candidate}' is not allowed. "
                        + "Only NoMercy.* types and plain framework collections are permitted."
                );
        }

        return base.BindToType(assemblyName, typeName);
    }

    /// <summary>
    /// Each type name in an assembly-qualified name. A generic name nests its arguments
    /// in brackets and suffixes each with its assembly, so the separators are '[', ']'
    /// and ',', and the assembly/version/culture tokens that follow are not type names.
    /// </summary>
    private static IEnumerable<string> NamedTypes(string typeName)
    {
        foreach (string segment in typeName.Split(['[', ']'], StringSplitOptions.TrimEntries))
        {
            if (segment.Length == 0)
                continue;

            // Only the first comma-separated token of a segment is a type; the
            // assembly name, version, culture and key token follow it and are not.
            int assemblySeparator = segment.IndexOf(',');
            string candidate = (
                assemblySeparator < 0 ? segment : segment[..assemblySeparator]
            ).Trim();

            if (candidate.Length == 0)
                continue;

            yield return candidate.EndsWith('&') ? candidate[..^1] : candidate;
        }
    }
}

public static class SerializationHelper
{
    private static readonly NoMercySerializationBinder Binder = new();

    public static string Serialize(object obj)
    {
        JsonSerializerSettings settings = new()
        {
            TypeNameHandling = TypeNameHandling.Objects,
            SerializationBinder = Binder,
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy(),
            },
        };

        return JsonConvert.SerializeObject(obj, settings);
    }

    public static T Deserialize<T>(string data)
    {
        JsonSerializerSettings settings = new()
        {
            TypeNameHandling = TypeNameHandling.Objects,
            SerializationBinder = Binder,
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy(),
            },
        };

        return JsonConvert.DeserializeObject<T>(data, settings)!;
    }

    /// <summary>
    /// Applies the serialized job DATA onto an already-constructed instance
    /// (e.g. one built through dependency injection), leaving constructor-injected
    /// services untouched.
    /// </summary>
    public static void Populate(string data, object target)
    {
        JsonSerializerSettings settings = new()
        {
            TypeNameHandling = TypeNameHandling.Objects,
            SerializationBinder = Binder,
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            ContractResolver = new DefaultContractResolver
            {
                NamingStrategy = new CamelCaseNamingStrategy(),
            },
        };
        JsonConvert.PopulateObject(data, target, settings);
    }
}
