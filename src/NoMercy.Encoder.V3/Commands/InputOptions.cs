namespace NoMercy.Encoder.V3.Commands;

public record InputOptions(
    string FilePath,
    TimeSpan? SeekTo = null,
    TimeSpan? Duration = null,
    string? HwAccelDevice = null,
    string? HwAccelOutputFormat = null
);
