namespace NoMercy.EncoderV2.Core.Contracts;

public interface IChapterWriter
{
    /// <summary>
    /// Create or update chapter file for the given source.
    /// Implementations should be idempotent and thread-safe.
    /// </summary>
    Task WriteChaptersAsync(string sourcePath, string outputFolder, CancellationToken cancellationToken = default);
}