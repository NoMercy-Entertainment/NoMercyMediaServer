using System.Collections;
using System.Text;

namespace NoMercy.EncoderV2.Adapters.Ffmpeg;

public class FfmpegAdapterCommandBuilder
{
    public FfmpegAdapterCommandBuilder()
    {
    }

    public string Build(string inputPath, string outputPath, Dictionary<string, dynamic?>? preInputOptions = null, Dictionary<string, dynamic?>? options = null)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
            throw new ArgumentException("inputPath is required", nameof(inputPath));

        if (string.IsNullOrWhiteSpace(outputPath))
            throw new ArgumentException("outputPath is required", nameof(outputPath));

        StringBuilder sb = new();

        // Overwrite output by default
        sb.Append("-y ");
        
        if (preInputOptions != null)
        {
            foreach (KeyValuePair<string, dynamic?> kv in preInputOptions)
            {
                string key = kv.Key;
                dynamic? value = kv.Value;

                if (string.IsNullOrWhiteSpace(key)) continue;

                AppendOption(sb, key, value);
            }
        }

        // Input
        sb.Append("-i ");
        sb.Append(Escape(inputPath));
        sb.Append(' ');

        // Options
        if (options != null)
        {
            foreach (KeyValuePair<string, dynamic?> kv in options)
            {
                string key = kv.Key;
                dynamic? value = kv.Value;

                if (string.IsNullOrWhiteSpace(key)) continue;

                AppendOption(sb, key, value);
            }
        }

        // Output
        sb.Append(Escape(outputPath));

        return sb.ToString().Trim();
    }

    static void AppendOption(StringBuilder sb, string key, dynamic? value)
    {
        // Null -> skip
        if (value is null) return;

        // Boolean true -> flag only (e.g., -y)
        if (value is bool b)
        {
            if (b)
            {
                sb.Append(key);
                sb.Append(' ');
            }
            return;
        }

        // If value is a string, append key + escaped value
        if (value is string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                // append flag only if empty string explicitly provided
                sb.Append(key);
                sb.Append(' ');
                return;
            }

            sb.Append(key);
            sb.Append(' ');
            sb.Append(Escape(s));
            sb.Append(' ');
            return;
        }

        // If value is an enumerable (but not string), treat each element as a separate occurrence.
        if (value is IEnumerable enumerable && !(value is string))
        {
            // Convert to list of strings for stable iteration
            List<string> items = new();
            foreach (object? item in enumerable) // <-- fixed: iterate as object? to avoid compiler error
            {
                if (item is null) continue;
                items.Add(item.ToString() ?? string.Empty);
            }

            // If no items, skip
            if (items.Count == 0) return;

            // For duplicated flags (like -init_hw_device opencl=ocl ; -init_hw_device cuda=cu:0)
            // repeat the flag for each element.
            foreach (string item in items)
            {
                if (string.IsNullOrEmpty(item))
                {
                    sb.Append(key);
                    sb.Append(' ');
                }
                else
                {
                    sb.Append(key);
                    sb.Append(' ');
                    sb.Append(Escape(item));
                    sb.Append(' ');
                }
            }

            return;
        }

        // Fallback: numeric or other single value -> append key + value
        sb.Append(key);
        sb.Append(' ');
        sb.Append(Escape(value.ToString() ?? string.Empty));
        sb.Append(' ');
    }

    static string Escape(string value)
    {
        if (value is null) return "\"\"";
        if (value.Length == 0) return "\"\"";

        // If value contains spaces or quotes, wrap in quotes and escape internal quotes
        if (value.IndexOfAny(new[] { ' ', '\t', '"' }) >= 0)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        return value;
    }
}
