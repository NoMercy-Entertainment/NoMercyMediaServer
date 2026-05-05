using NoMercy.OpticalMedia.Sources.Bluray;

namespace NoMercy.Tests.Encoder.DiscRipping;

public class BlurayProtectionClassifierTests
{
    [Fact]
    public void Classify_NullOrEmpty_ReturnsNull()
    {
        BlurayDiscSource.ClassifyProtection("").Should().BeNull();
        BlurayDiscSource.ClassifyProtection(null!).Should().BeNull();
    }

    [Fact]
    public void Classify_DriveCertNotSupported_ReturnsAacsBusKind()
    {
        const string stderr =
            @"src/libaacs/mmc.c:719: Drive does not support reading drive certificate
src/libaacs/aacs.c:1181: Unable to read drive certificate";

        var p = BlurayDiscSource.ClassifyProtection(stderr);
        p.Should().NotBeNull();
        p!.Kind.Should().Be("AACS");
        p.Message.Should().Contain("bus key");
    }

    [Fact]
    public void Classify_UnableToDecryptUnit_ReturnsAacsKeyKind()
    {
        const string stderr = @"src/libbluray/disc/aacs.c:306: Unable to decrypt unit (AACS)!";

        var p = BlurayDiscSource.ClassifyProtection(stderr);
        p.Should().NotBeNull();
        p!.Kind.Should().Be("AACS");
        p.Message.Should().Contain("KEYDB");
    }

    [Fact]
    public void Classify_NoMatchingConverter_ReturnsBdPlus()
    {
        const string stderr = "bdplus: no matching converter";

        var p = BlurayDiscSource.ClassifyProtection(stderr);
        p.Should().NotBeNull();
        p!.Kind.Should().Be("BD+");
    }

    [Fact]
    public void Classify_CleanProbeOutput_ReturnsNull()
    {
        const string stderr =
            @"[bluray @ 0000028e11e4bfc0] 19 usable playlists:
[bluray @ 0000028e11e4bfc0] playlist 01601.mpls (0:23:39)
[bluray @ 0000028e11e4bfc0] selected 00600.mpls";

        BlurayDiscSource.ClassifyProtection(stderr).Should().BeNull();
    }

    [Fact]
    public void Classify_AvatarStderr_ReturnsAacsBusKind()
    {
        // Real captured stderr from probing Avatar Last Airbender Book 1 Disc 1
        // on the HL-DT-ST drive (drive can read playlist structure but not
        // bus-key decrypt).
        const string stderr =
            @"src/libaacs/mmc.c:394: AACS not active or supported by the drive
src/libaacs/mmc.c:719: Drive does not support reading drive certificate
src/libaacs/aacs.c:1181: Unable to read drive certificate
[bluray @ 0000028e11e4bfc0] 19 usable playlists:
[bluray @ 0000028e11e4bfc0] playlist 01601.mpls (0:23:39)
[bluray @ 0000028e11e4bfc0] selected 00600.mpls
src/libbluray/disc/aacs.c:306: Unable to decrypt unit (AACS)!";

        var p = BlurayDiscSource.ClassifyProtection(stderr);
        p.Should().NotBeNull();
        p!.Kind.Should().Be("AACS");
        // Drive-cert-not-supported takes precedence over generic decrypt-unit
        p.Message.Should().Contain("bus key");
    }
}
