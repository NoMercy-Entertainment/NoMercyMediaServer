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

    public override Type BindToType(string? assemblyName, string typeName)
    {
        // Strip generic arguments before prefix check (e.g. "System.Collections.Generic.List`1")
        string rootTypeName = typeName.Contains(value: '[') ? typeName[..typeName.IndexOf(value: '[')] : typeName;

        bool isAllowed = AllowedNamespacePrefixes.Any(predicate: prefix =>
            rootTypeName.StartsWith(value: prefix, comparisonType: StringComparison.Ordinal)
        );

        if (!isAllowed)
            throw new JsonSerializationException(
                message: $"Deserialization of type '{typeName}' is not allowed. "
                         + "Only NoMercy.* and NoMercyQueue.* types are permitted."
            );

        return base.BindToType(assemblyName: assemblyName, typeName: typeName);
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

        return JsonConvert.SerializeObject(value: obj, settings: settings);
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

        return JsonConvert.DeserializeObject<T>(value: data, settings: settings)!;
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
        JsonConvert.PopulateObject(value: data, target: target, settings: settings);
    }
}
