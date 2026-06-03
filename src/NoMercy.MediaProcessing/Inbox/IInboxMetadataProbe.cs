using NoMercy.Database.Models.Libraries;

namespace NoMercy.MediaProcessing.Inbox;

public interface IInboxMetadataProbe
{
    Task<CandidateMatch[]> SearchMoviesAsync(string title, int? year, CancellationToken ct);

    Task<CandidateMatch[]> SearchTvAsync(string title, int? year, CancellationToken ct);

    Task<CandidateMatch?> LookupMusicReleaseAsync(Guid releaseId, CancellationToken ct);
}
