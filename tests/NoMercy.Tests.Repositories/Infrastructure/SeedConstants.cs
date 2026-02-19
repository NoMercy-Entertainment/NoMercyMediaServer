namespace NoMercy.Tests.Repositories.Infrastructure;

public static class SeedConstants
{
    public static readonly Guid UserId = Guid.Parse("37d03e60-7b0a-4246-a85b-a5618966a383");
    public static readonly Guid OtherUserId = Guid.Parse("a1b2c3d4-e5f6-7890-abcd-ef1234567890");
    public static readonly Ulid MovieLibraryId = Ulid.NewUlid();
    public static readonly Ulid TvLibraryId = Ulid.NewUlid();
    public static readonly Ulid MovieFolderId = Ulid.NewUlid();
    public static readonly Ulid MusicLibraryId = Ulid.NewUlid();
    public static readonly Ulid MusicFolderId = Ulid.NewUlid();

    public static readonly Ulid MovieVideoFile1Id = Ulid.NewUlid();
    public static readonly Ulid MovieVideoFile2Id = Ulid.NewUlid();
    public static readonly Ulid TvVideoFile1Id = Ulid.NewUlid();
    public static readonly Ulid TvVideoFile2Id = Ulid.NewUlid();
    public static readonly Ulid EncoderProfileId = Ulid.NewUlid();
}
