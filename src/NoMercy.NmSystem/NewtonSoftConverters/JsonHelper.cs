using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace NoMercy.NmSystem.NewtonSoftConverters;

public static class JsonHelper
{
    public static readonly JsonSerializerSettings Settings = new()
    {
        // General settings
        Formatting = Formatting.Indented, // Pretty print JSON
        MetadataPropertyHandling = MetadataPropertyHandling.Ignore, // Ignore $id and $ref properties
        DateParseHandling = DateParseHandling.None, // Treat dates as strings
        FloatParseHandling = FloatParseHandling.Double, // Parse floats as double

        // Reference handling
        PreserveReferencesHandling = PreserveReferencesHandling.None, // Do not use $id and $ref
        ReferenceLoopHandling = ReferenceLoopHandling.Ignore, // Ignore reference loops

        // Null and default value handling
        NullValueHandling = NullValueHandling.Include, // Include null values
        DefaultValueHandling = DefaultValueHandling.Include, // Include default values

        // Error handling
        Error = (_, ev) =>
        {
            ev.ErrorContext.Handled = true;
        }, // Handle errors silently

        // Type handling
        TypeNameHandling = TypeNameHandling.None, // Do not include type names

        // Missing member handling
        MissingMemberHandling = MissingMemberHandling.Ignore, // Ignore missing members

        // Converters
        Converters =
        {
            new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }, // Convert dates to ISO format
            new LongConverter(), // Custom converter for long values
            new ParseNumbersAsInt32Converter(), // Custom converter for parsing numbers as int
            new StringEnumConverter(), // Convert enums to strings
            new DoubleConverter(), // Custom converter for double values
            new UlidNewtonsoftConverter(), // Serialize Ulid as canonical 26-char string
        },
    };

    /// <summary>
    /// Forgiving JSON parse: null/empty input or malformed JSON both return
    /// <c>default(T)</c> instead of throwing. Used widely on payloads we
    /// don't fully trust (FFprobe output, persisted job payloads, third-party
    /// API responses) where the throw path would propagate up to a generic
    /// 500 with no useful context. Callers that need strict validation
    /// should use <see cref="JsonConvert.DeserializeObject{T}(string)"/>
    /// directly.
    /// </summary>
    public static T? FromJson<T>(this string? json)
    {
        if (string.IsNullOrEmpty(json))
            return default;

        try
        {
            return JsonConvert.DeserializeObject<T>(json, Settings);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    public static string ToJson<T>(this T self)
    {
        return JsonConvert.SerializeObject(self, Settings);
    }
}
