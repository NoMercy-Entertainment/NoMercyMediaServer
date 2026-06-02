using NoMercy.Database.Models.Media;

namespace NoMercy.Data.Repositories;

public interface IContentSegmentRepository
{
    Task<List<ContentSegment>> GetForEpisodeAsync(int episodeId);

    Task<List<ContentSegment>> ListAsync(
        int pageSize,
        int pageIndex,
        ContentSegmentType? filterType = null
    );

    Task<int> GetTotalCountAsync();

    Task<List<ContentSegment>> GetForMovieAsync(int movieId);

    Task<ContentSegment?> GetByIdAsync(Ulid id);

    Task<ContentSegment> CreateAsync(ContentSegment segment);

    Task<ContentSegment?> UpdateAsync(Ulid id, Action<ContentSegment> apply);

    Task<bool> DeleteAsync(Ulid id);

    Task ReplaceDetectorSegmentsForEpisodeAsync(
        int episodeId,
        IReadOnlyList<ContentSegment> newSegments
    );
}
