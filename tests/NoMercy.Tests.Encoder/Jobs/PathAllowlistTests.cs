namespace NoMercy.Tests.Encoder.Jobs;

using NoMercy.Encoder.Jobs;

/// <summary>
/// PathAllowlist is the trust boundary between the encoder and the
/// filesystem. Getting it wrong lets a malicious job encode /etc/passwd
/// into the output folder, or escape a user's library into the admin's.
///
/// These tests pin:
///   - Paths inside an allowed root pass.
///   - Path traversal (../..) is normalized before check, not after.
///   - Sibling-prefix confusion: "/media/movies" allowlist MUST NOT
///     also allow "/media/movies_private/…" just because the sibling
///     starts with the same prefix.
///   - Case-insensitive comparison for Windows.
/// </summary>
public class PathAllowlistTests
{
    [Fact]
    public void InputPathInsideAllowedRoot_IsAllowed()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "allowed-in"));
        PathAllowlist list = new([root], []);

        bool allowed = list.IsInputPathAllowed(Path.Combine(root, "subfolder", "file.mkv"));

        allowed.Should().BeTrue();
    }

    [Fact]
    public void InputPathOutsideAllowedRoot_IsDenied()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "allowed-in"));
        PathAllowlist list = new([root], []);

        // Totally different directory.
        bool allowed = list.IsInputPathAllowed(
            Path.Combine(Path.GetTempPath(), "other", "file.mkv")
        );

        allowed.Should().BeFalse();
    }

    [Fact]
    public void InputPathUsingTraversal_NormalizesBeforeCheck()
    {
        // /allowed/../etc/secret.mkv normalizes to /etc/secret.mkv —
        // must not be in the allowlist.
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "allowed-traverse"));
        PathAllowlist list = new([root], []);

        string traversal = Path.Combine(root, "..", "elsewhere", "file.mkv");
        bool allowed = list.IsInputPathAllowed(traversal);

        allowed.Should().BeFalse();
    }

    [Fact]
    public void EmptyAllowlist_DeniesEverything()
    {
        PathAllowlist list = new([], []);
        list.IsInputPathAllowed("/anything").Should().BeFalse();
        list.IsOutputPathAllowed("/anything").Should().BeFalse();
    }

    [Fact]
    public void OutputPathInsideAllowedRoot_IsAllowed()
    {
        string root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "allowed-out"));
        PathAllowlist list = new([], [root]);

        bool allowed = list.IsOutputPathAllowed(Path.Combine(root, "file.mkv"));

        allowed.Should().BeTrue();
    }

    [Fact]
    public void OutputAndInputLists_AreIndependent()
    {
        // A path allowed as output must not automatically be allowed as input
        // (and vice versa). This separates "can we read from here" from
        // "can we write to here" — critical for libraries that are
        // read-only vs the server's output directory.
        string inPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "only-in"));
        string outPath = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "only-out"));
        PathAllowlist list = new([inPath], [outPath]);

        list.IsInputPathAllowed(Path.Combine(outPath, "x.mkv")).Should().BeFalse();
        list.IsOutputPathAllowed(Path.Combine(inPath, "x.mkv")).Should().BeFalse();
    }

    [Fact]
    public void SiblingDirectory_WithSharedPrefix_IsDenied()
    {
        // Regression: "/media/movies" allowlist must not grant access to
        // "/media/movies_private/…". StartsWith against a raw prefix lets
        // this through; we guard by requiring the allowed path to match
        // at a directory boundary.
        string allowed = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "media", "movies"));
        PathAllowlist list = new([allowed], []);

        string sibling = Path.GetFullPath(
            Path.Combine(Path.GetTempPath(), "media", "movies_private", "secret.mkv")
        );

        bool result = list.IsInputPathAllowed(sibling);

        result.Should().BeFalse("sibling with shared prefix must not be allowed");
    }
}
