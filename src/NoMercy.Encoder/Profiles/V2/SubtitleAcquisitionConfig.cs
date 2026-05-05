namespace NoMercy.Encoder.Profiles.V2;

public enum SubtitleProvider
{
    OpenSubtitles,
}

public enum SubtitleMatchStrategy
{
    HashOnly,
    HashThenFilename,
    HashThenFilenameThenTitle,
    TitleOnly,
}

public enum SubtitleEmbedPolicy
{
    ExactMatchOnly,
    AlwaysSidecar,
}

public record SubtitleAcquisitionConfig
{
    public bool Enabled { get; init; }
    public SubtitleProvider[] Providers { get; init; } = [SubtitleProvider.OpenSubtitles];
    public string[] Languages { get; init; } = [];
    public SubtitleMatchStrategy Strategy { get; init; } =
        SubtitleMatchStrategy.HashThenFilenameThenTitle;
    public int MaxPerLanguage { get; init; } = 1;
    public double MinRating { get; init; }
    public int MinDownloads { get; init; }
    public bool TrustedUploadersOnly { get; init; }
    public bool RequireMatchingFps { get; init; }
    public TimeSpan PerRequestTimeout { get; init; } = TimeSpan.FromSeconds(5);
    public bool FillMissingOnly { get; init; } = true;
    public SubtitleEmbedPolicy EmbedPolicy { get; init; } = SubtitleEmbedPolicy.ExactMatchOnly;
}
