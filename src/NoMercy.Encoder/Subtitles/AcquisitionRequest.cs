using NoMercy.Encoder.Profiles;

namespace NoMercy.Encoder.Subtitles;

public record AcquisitionRequest(
    string SourcePath,
    long SourceFileSize,
    string SourceFilename,
    string MediaTitle,
    int? Season,
    int? Episode,
    int? Year,
    double? SourceFps,
    TimeSpan SourceDuration,
    string[] LanguagesAlreadyInSource,
    SubtitleAcquisitionConfig Config
);
