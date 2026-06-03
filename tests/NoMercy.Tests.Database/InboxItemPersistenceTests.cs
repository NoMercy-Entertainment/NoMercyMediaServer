using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models.Libraries;

namespace NoMercy.Tests.Database;

public class InboxItemPersistenceTests : IDisposable
{
    private readonly MediaContext _context;

    public InboxItemPersistenceTests()
    {
        DbContextOptionsBuilder<MediaContext> optionsBuilder = new();
        optionsBuilder.UseSqlite("Data Source=:memory:");

        _context = new(optionsBuilder.Options);
        _context.Database.OpenConnection();
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Database.CloseConnection();
        _context.Dispose();
    }

    [Fact]
    public async Task InboxItem_RoundTrips_CandidatesJson()
    {
        Ulid id = Ulid.NewUlid();
        InboxItem item = new()
        {
            Id = id,
            SourcePath = "Inbox/The Matrix (1999)/movie.mkv",
            DriverId = Ulid.NewUlid(),
            DetectedType = "movie",
            Confidence = "high",
            Status = "NeedsReview",
            Candidates =
            [
                new CandidateMatch
                {
                    Provider = "tmdb",
                    ExternalId = "603",
                    Title = "The Matrix",
                    Year = 1999,
                    Score = 0.98,
                },
            ],
        };

        _context.InboxItems.Add(item);
        await _context.SaveChangesAsync();

        InboxItem loaded = await _context.InboxItems.SingleAsync(i => i.Id == id);

        Assert.Single(loaded.Candidates);
        Assert.Equal("The Matrix", loaded.Candidates[0].Title);
        Assert.Equal(1999, loaded.Candidates[0].Year);
    }
}
