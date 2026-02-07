namespace NoMercy.Database;

/// <summary>
/// Represents a custom FFmpeg argument key-value pair
/// </summary>
public class CustomArgument
{
    /// <summary>
    /// The argument key (e.g., "-preset", "-tune")
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// The argument value
    /// </summary>
    public string Val { get; set; } = string.Empty;
}
