namespace NoMercy.Database;

public class IAudioProfile
{
    public string Codec { get; set; } = string.Empty;
    public int Channels { get; set; }
    public int SampleRate { get; set; }
    public string SegmentName { get; set; } = string.Empty;
    public string PlaylistName { get; set; } = string.Empty;
    public string[] AllowedLanguages { get; set; } = [];
    public string[] Opts { get; set; } = [];
    public (string key, string Val)[] CustomArguments { get; set; } = [];

    /// <summary>
    /// Optional loudness normalization target — "ebu_r128", "replaygain",
    /// "custom", or null/none. Maps to an <c>loudnorm</c> filter downstream.
    /// </summary>
    public string? Loudness { get; set; }

    /// <summary>
    /// Optional channel-mix downmix preset — "stereo", "mono", "custom".
    /// Null means use ffmpeg's default <c>-ac N</c> downmix matrix. "custom"
    /// requires <see cref="CustomPanMatrix"/>.
    /// </summary>
    public string? Downmix { get; set; }

    /// <summary>
    /// Raw pan expression used when <see cref="Downmix"/> is "custom"
    /// (e.g. <c>stereo|FL&lt;FL+0.5*FC|FR&lt;FR+0.5*FC</c>).
    /// </summary>
    public string? CustomPanMatrix { get; set; }
}
