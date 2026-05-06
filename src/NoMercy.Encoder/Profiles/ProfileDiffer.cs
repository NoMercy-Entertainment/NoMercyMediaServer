namespace NoMercy.Encoder.Profiles;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

public static class ProfileDiffer
{
    private static readonly JsonSerializer CamelSerializer = JsonSerializer.Create(
        new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
        }
    );

    public static JObject Diff(EncodingProfile child, EncodingProfile resolvedParent)
    {
        JObject childJson = JObject.FromObject(child, CamelSerializer);
        JObject parentJson = JObject.FromObject(resolvedParent, CamelSerializer);
        return DiffObject(childJson, parentJson);
    }

    private static JObject DiffObject(JObject child, JObject parent)
    {
        JObject result = new();
        foreach (JProperty prop in child.Properties())
        {
            JToken? parentValue = parent[prop.Name];
            JToken childValue = prop.Value;

            if (parentValue is null || !JToken.DeepEquals(childValue, parentValue))
            {
                result[prop.Name] = childValue.DeepClone();
            }
        }
        return result;
    }
}
