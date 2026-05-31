namespace NoMercy.Encoder.Subtitles;

public interface ITesseractModelManager
{
    Task<string> EnsureLanguageModelAsync(string language, CancellationToken ct);

    IReadOnlyList<string> GetAvailableLanguages();

    IReadOnlyList<string> GetDownloadedLanguages();

    string ModelDirectory { get; }
}
