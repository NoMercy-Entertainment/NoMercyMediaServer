using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NoMercy.Encoder.Profiles.V2;

public static class PresetResolver
{
    private const int MaxDepth = 8;
    private const int CurrentSchemaVersion = 2;

    /// <summary>
    /// Test seam — production startup leaves this empty.
    /// Inject migrations here in tests to verify upgrade paths.
    /// </summary>
    internal static IReadOnlyList<IProfileMigration> Migrations { get; set; } = [];

    private static JObject EnsureCurrent(JObject input)
    {
        int version = input["schemaVersion"]?.Value<int>() ?? CurrentSchemaVersion;
        while (version < CurrentSchemaVersion)
        {
            IProfileMigration? step = Migrations.FirstOrDefault(m => m.FromVersion == version);
            if (step is null)
                throw new InvalidOperationException($"No migration from schema v{version}.");
            input = step.Migrate(input);
            version = step.ToVersion;
        }
        return input;
    }

    public static EncodingProfile Resolve(Ulid presetId, IPresetLookup lookup)
    {
        List<(Ulid Id, string Json)> chain = [];
        HashSet<Ulid> visited = [];
        Ulid? cursor = presetId;

        while (cursor.HasValue)
        {
            if (!visited.Add(cursor.Value))
                throw new InvalidOperationException(
                    $"Inheritance cycle detected at preset {cursor.Value}."
                );

            if (chain.Count >= MaxDepth)
                throw new InvalidOperationException(
                    $"Inheritance chain exceeds max depth of {MaxDepth}."
                );

            (string ProfileJson, Ulid? ParentPresetId)? entry = lookup.Get(cursor.Value);
            if (entry is null)
                throw new InvalidOperationException($"Preset {cursor.Value} not found in lookup.");

            chain.Add((cursor.Value, entry.Value.ProfileJson));
            cursor = entry.Value.ParentPresetId;
        }

        chain.Reverse();
        JObject accumulator = EnsureCurrent(JObject.Parse(chain[0].Json));
        for (int i = 1; i < chain.Count; i++)
        {
            JObject child = EnsureCurrent(JObject.Parse(chain[i].Json));
            accumulator.Merge(
                child,
                new JsonMergeSettings
                {
                    MergeArrayHandling = MergeArrayHandling.Replace,
                    MergeNullValueHandling = MergeNullValueHandling.Merge,
                }
            );
        }

        EncodingProfile? resolved = accumulator.ToObject<EncodingProfile>();
        if (resolved is null)
            throw new InvalidOperationException("Resolved profile failed to deserialize.");
        return resolved;
    }
}
