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
        string rootTypeName = typeName.Contains('[') ? typeName[..typeName.IndexOf('[')] : typeName;

        bool isAllowed = AllowedNamespacePrefixes.Any(prefix =>
            rootTypeName.StartsWith(prefix, StringComparison.Ordinal)
        );

        if (!isAllowed)
            throw new JsonSerializationException(
                $"Deserialization of type '{typeName}' is not allowed. "
                    + "Only NoMercy.* and NoMercyQueue.* types are permitted."
            );

        return base.BindToType(assemblyName, typeName);
    }
}

public static class SerializationHelper
{
    private static readonly NoMercySerializationBinder Binder = new();

    public static string Serialize(object obj)
    {
        JsonSerializerSettings settings = new()
        {
            TypeNameHandling = TypeNameHandling.Auto,
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
            TypeNameHandling = TypeNameHandling.Auto,
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
}
