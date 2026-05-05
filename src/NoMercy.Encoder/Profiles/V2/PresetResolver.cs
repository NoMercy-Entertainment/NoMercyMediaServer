using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NoMercy.Encoder.Profiles.V2;

public static class PresetResolver
{
    private const int MaxDepth = 8;

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
        JObject accumulator = JObject.Parse(chain[0].Json);
        for (int i = 1; i < chain.Count; i++)
        {
            JObject child = JObject.Parse(chain[i].Json);
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
