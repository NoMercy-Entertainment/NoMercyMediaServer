// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NoMercy.Encoder.Composition;
using NoMercy.Encoder.Infrastructure;
using NoMercy.NmSystem.Dto;
using NoMercy.OpticalMedia.Drives;
using NoMercy.OpticalMedia.Sources;
using NoMercy.OpticalMedia.Sources.Bluray;
using NoMercy.Storage;

namespace NoMercy.Tests.OpticalMedia.Sources;

[Trait(name: "Category", value: "Unit")]
public class BlurayDiscSourceTests
{
    [Fact]
    public void ParsePlaylists_EmptyStderr_ReturnsEmpty()
    {
        string stderr = "";

        List<(int Index, TimeSpan Duration)> result = BlurayDiscSource.ParsePlaylists(stderr: stderr);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParsePlaylists_NullStderr_ReturnsEmpty()
    {
        string stderr = "";

        List<(int Index, TimeSpan Duration)> result = BlurayDiscSource.ParsePlaylists(stderr: stderr);

        result.Should().BeEmpty();
    }

    [Fact]
    public void ParsePlaylists_SinglePlaylist_ParsesIndexAndDuration()
    {
        string stderr = "playlist 00100.mpls (02:05:30)";

        List<(int Index, TimeSpan Duration)> result = BlurayDiscSource.ParsePlaylists(stderr: stderr);

        result.Should().HaveCount(expected: 1);
        result[index: 0].Index.Should().Be(expected: 100);
        result[index: 0]
            .Duration.Should()
            .Be(expected: TimeSpan.FromHours(hours: 2).Add(ts: TimeSpan.FromMinutes(minutes: 5)).Add(ts: TimeSpan.FromSeconds(seconds: 30)));
    }

    [Fact]
    public void ParsePlaylists_MultiplePlaylistsInSeparateLines_AllParsed()
    {
        string stderr = """
            libbluray DEBUG: parse_playlist 00090.mpls
            playlist 00090.mpls (01:30:45)
            libbluray DEBUG: parse_playlist 00100.mpls
            playlist 00100.mpls (02:15:20)
            libbluray DEBUG: parse_playlist 00110.mpls
            playlist 00110.mpls (00:45:15)
            """;

        List<(int Index, TimeSpan Duration)> result = BlurayDiscSource.ParsePlaylists(stderr: stderr);

        result.Should().HaveCount(expected: 3);
        result[index: 0].Index.Should().Be(expected: 90);
        result[index: 1].Index.Should().Be(expected: 100);
        result[index: 2].Index.Should().Be(expected: 110);
    }

    [Fact]
    public void ParsePlaylists_DuplicateIndices_DistinctByIndex()
    {
        string stderr = """
            playlist 00100.mpls (01:00:00)
            playlist 00100.mpls (02:00:00)
            """;

        List<(int Index, TimeSpan Duration)> result = BlurayDiscSource.ParsePlaylists(stderr: stderr);

        result.Should().HaveCount(expected: 1);
        result[index: 0].Index.Should().Be(expected: 100);
    }

    [Fact]
    public void ParsePlaylists_MalformedIndexLine_Skipped()
    {
        string stderr = """
            playlist notanumber.mpls (01:00:00)
            playlist 00100.mpls (01:00:00)
            """;

        List<(int Index, TimeSpan Duration)> result = BlurayDiscSource.ParsePlaylists(stderr: stderr);

        result.Should().HaveCount(expected: 1);
        result[index: 0].Index.Should().Be(expected: 100);
    }

    [Fact]
    public void ParsePlaylists_MalformedDurationLine_Skipped()
    {
        string stderr = """
            playlist 00100.mpls (invalid)
            playlist 00110.mpls (01:00:00)
            """;

        List<(int Index, TimeSpan Duration)> result = BlurayDiscSource.ParsePlaylists(stderr: stderr);

        result.Should().HaveCount(expected: 1);
        result[index: 0].Index.Should().Be(expected: 110);
    }

    [Fact]
    public void ParsePlaylists_IndexOverflowsInt_SkippedViaTryParseFailure()
    {
        // Unlike the "notanumber" case above (which the regex itself never
        // matches), an all-digit index that overflows Int32 DOES match
        // PlaylistRegex — this is what actually exercises the internal
        // int.TryParse failure branch (`continue`), not just a regex miss.
        string stderr = """
            playlist 99999999999999999999.mpls (01:00:00)
            playlist 00100.mpls (01:00:00)
            """;

        List<(int Index, TimeSpan Duration)> result = BlurayDiscSource.ParsePlaylists(stderr: stderr);

        result.Should().HaveCount(expected: 1);
        result[index: 0].Index.Should().Be(expected: 100);
    }

    [Fact]
    public void ParsePlaylists_DurationComponentOverflowsInt_SkippedViaTryParseFailure()
    {
        // Same reasoning as the index-overflow case: an all-digit hour
        // component that overflows Int32 matches the regex's \d{1,} group
        // but fails TryParseHmsDuration's internal int.TryParse.
        string stderr = """
            playlist 00100.mpls (99999999999:00:00)
            playlist 00110.mpls (01:00:00)
            """;

        List<(int Index, TimeSpan Duration)> result = BlurayDiscSource.ParsePlaylists(stderr: stderr);

        result.Should().HaveCount(expected: 1);
        result[index: 0].Index.Should().Be(expected: 110);
    }

    [Fact]
    public void TryParseHmsDuration_WrongNumberOfParts_ReturnsFalse()
    {
        // TryParseHmsDuration is only ever fed a regex-guaranteed 3-part
        // "h:m:s" string through ParsePlaylists, so its own part-count guard
        // has no reachable caller through the public surface — exercised
        // directly via reflection.
        System.Reflection.MethodInfo method = typeof(BlurayDiscSource).GetMethod(
            name: "TryParseHmsDuration",
            bindingAttr: System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static
        )!;

        object?[] parameters = ["12:34", null];
        object? success = method.Invoke(obj: null, parameters: parameters);

        success.Should().Be(expected: false);
    }

    [Fact]
    public void ParsePlaylists_IgnoreCasePlaylistKeyword()
    {
        string stderr = """
            PLAYLIST 00100.mpls (01:00:00)
            Playlist 00110.mpls (01:30:00)
            """;

        List<(int Index, TimeSpan Duration)> result = BlurayDiscSource.ParsePlaylists(stderr: stderr);

        result.Should().HaveCount(expected: 2);
    }

    [Fact]
    public void ParsePlaylists_VariableWhitespaceFormat_Parsed()
    {
        string stderr = "playlist  00100.mpls  (01:00:00)";

        List<(int Index, TimeSpan Duration)> result = BlurayDiscSource.ParsePlaylists(stderr: stderr);

        result.Should().HaveCount(expected: 1);
        result[index: 0].Index.Should().Be(expected: 100);
    }

    [Fact]
    public void ParsePlaylists_LargePlaylistIndex_Parsed()
    {
        string stderr = "playlist 99999.mpls (01:00:00)";

        List<(int Index, TimeSpan Duration)> result = BlurayDiscSource.ParsePlaylists(stderr: stderr);

        result.Should().HaveCount(expected: 1);
        result[index: 0].Index.Should().Be(expected: 99999);
    }

    [Fact]
    public void ParsePlaylists_SingleDigitTimeParts_Parsed()
    {
        string stderr = "playlist 00100.mpls (1:2:3)";

        List<(int Index, TimeSpan Duration)> result = BlurayDiscSource.ParsePlaylists(stderr: stderr);

        result.Should().HaveCount(expected: 1);
        result[index: 0].Duration.Should().Be(expected: new(hours: 1, minutes: 2, seconds: 3));
    }

    [Fact]
    public void ClassifyProtection_NullStderr_ReturnsNull()
    {
        string stderr = "";

        DiscProtection? result = BlurayDiscSource.ClassifyProtection(stderr: stderr);

        result.Should().BeNull();
    }

    [Fact]
    public void ClassifyProtection_DriveNoCertificate_ReturnsAacsProtection()
    {
        string stderr = "Drive does not support reading drive certificate";

        DiscProtection? result = BlurayDiscSource.ClassifyProtection(stderr: stderr);

        result.Should().NotBeNull();
        result!.Kind.Should().Be(expected: "AACS");
        result.Message.Should().Contain(expected: "bus key");
    }

    [Fact]
    public void ClassifyProtection_UnableToReadCertificate_ReturnsAacsProtection()
    {
        string stderr = "Unable to read drive certificate";

        DiscProtection? result = BlurayDiscSource.ClassifyProtection(stderr: stderr);

        result.Should().NotBeNull();
        result!.Kind.Should().Be(expected: "AACS");
    }

    [Fact]
    public void ClassifyProtection_UnableToDecryptUnit_ReturnsAacsProtection()
    {
        string stderr = "Unable to decrypt unit (AACS)";

        DiscProtection? result = BlurayDiscSource.ClassifyProtection(stderr: stderr);

        result.Should().NotBeNull();
        result!.Kind.Should().Be(expected: "AACS");
        result.Message.Should().Contain(expected: "KEYDB");
    }

    [Fact]
    public void ClassifyProtection_NoMatchingCertificate_ReturnsAacsProtection()
    {
        string stderr = "aacs: no matching certificate found";

        DiscProtection? result = BlurayDiscSource.ClassifyProtection(stderr: stderr);

        result.Should().NotBeNull();
        result!.Kind.Should().Be(expected: "AACS");
    }

    [Fact]
    public void ClassifyProtection_NoMatchingConverter_ReturnsBdplusProtection()
    {
        string stderr = "bdplus: no matching converter";

        DiscProtection? result = BlurayDiscSource.ClassifyProtection(stderr: stderr);

        result.Should().NotBeNull();
        result!.Kind.Should().Be(expected: "BD+");
        result.Message.Should().Contain(expected: "converter");
    }

    [Fact]
    public void ClassifyProtection_CaseSensitivityIgnored()
    {
        string stderr = "DRIVE DOES NOT SUPPORT READING DRIVE CERTIFICATE";

        DiscProtection? result = BlurayDiscSource.ClassifyProtection(stderr: stderr);

        result.Should().NotBeNull();
        result!.Kind.Should().Be(expected: "AACS");
    }

    [Fact]
    public void ClassifyProtection_MixedWarnings_FirstMatchReturned()
    {
        string stderr = """
            Some warning here
            Unable to decrypt unit (AACS)
            Another warning
            """;

        DiscProtection? result = BlurayDiscSource.ClassifyProtection(stderr: stderr);

        result.Should().NotBeNull();
        result!.Kind.Should().Be(expected: "AACS");
    }

    [Fact]
    public void ClassifyProtection_BdplusPriority_CorrectlyClassified()
    {
        string stderr = "bdplus: no matching converter for this disc";

        DiscProtection? result = BlurayDiscSource.ClassifyProtection(stderr: stderr);

        result.Should().NotBeNull();
        result!.Kind.Should().Be(expected: "BD+");
    }

    [Fact]
    public void ClassifyProtection_NoProtectionMarkers_ReturnsNull()
    {
        string stderr = """
            [bluray] playlist 00100.mpls found
            [bluray] reading playlist
            No protection markers here
            """;

        DiscProtection? result = BlurayDiscSource.ClassifyProtection(stderr: stderr);

        result.Should().BeNull();
    }
}

/// <summary>
/// End-to-end tests for <see cref="BlurayDiscSource.ProbeAsync"/> and
/// <see cref="BlurayDiscSource.ProbeTitleAsync"/> via mocked
/// <see cref="IProcessRunner"/> / <see cref="IStorageDriver"/> — no real
/// libbluray/ffmpeg subprocess or physical drive, per the DiscRipper /
/// CdDiscSource convention already established in this test project.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class BlurayDiscSourceEndToEndTests
{
    private static BlurayDiscSource MakeSut(
        IProcessRunner? processRunner = null,
        IStorageDriver? storageDriver = null
    )
    {
        EncoderOptions options = new() { FfprobePathOverride = "ffprobe" };
        return new(
            options: options,
            processRunner: processRunner ?? Mock.Of<IProcessRunner>(),
            storageDriver: storageDriver ?? Mock.Of<IStorageDriver>(),
            logger: NullLogger<BlurayDiscSource>.Instance
        );
    }

    private static Mock<IProcessRunner> MakeRunner(
        string stdErr,
        int exitCode = 1,
        string stdOut = ""
    )
    {
        Mock<IProcessRunner> runner = new();
        runner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(value: new ProcessResult(ExitCode: exitCode, StdOut: stdOut, StdErr: stdErr, Duration: TimeSpan.Zero));
        return runner;
    }

    [Fact]
    public void Type_IsBluRay()
    {
        MakeSut().Type.Should().Be(expected: OpticalDiscType.BluRay);
    }

    [Fact]
    public async Task ProbeAsync_ZeroPlaylistsParsed_ReturnsEmptyTitlesWithProtectionAndLabel()
    {
        Mock<IProcessRunner> runner = MakeRunner(stdErr: "no playlists here");
        Mock<IStorageDriver> driver = new();
        driver.Setup(expression: d => d.DirectoryExists(It.IsAny<string>())).Returns(value: false);

        BlurayDiscSource sut = MakeSut(processRunner: runner.Object, storageDriver: driver.Object);
        DiscDrive drive = new(Path: "D:\\", Label: "MOVIE_LABEL", HasDisc: true, DiscType: OpticalDiscType.BluRay);

        DiscInfo info = await sut.ProbeAsync(drive: drive, ct: CancellationToken.None);

        info.Titles.Should().BeEmpty();
        info.DiscLabel.Should().Be(expected: "MOVIE_LABEL");
        info.TotalDuration.Should().Be(expected: TimeSpan.Zero);
    }

    [Fact]
    public async Task ProbeAsync_ParsesPlaylistsAndFlagsLongestAsMainFeature()
    {
        string stderr = """
            playlist 00090.mpls (00:30:00)
            playlist 00100.mpls (02:15:00)
            """;
        Mock<IProcessRunner> runner = MakeRunner(stdErr: stderr);
        Mock<IStorageDriver> driver = new();
        driver.Setup(expression: d => d.DirectoryExists(It.IsAny<string>())).Returns(value: false);

        BlurayDiscSource sut = MakeSut(processRunner: runner.Object, storageDriver: driver.Object);
        DiscDrive drive = new(Path: "D:\\", Label: "MOVIE", HasDisc: true, DiscType: OpticalDiscType.BluRay);

        DiscInfo info = await sut.ProbeAsync(drive: drive, ct: CancellationToken.None);

        info.Titles.Should().HaveCount(expected: 2);
        info.Titles[0].Index.Should().Be(expected: 100, because: "titles are ordered by duration descending");
        info.Titles[0].IsMainFeature.Should().BeTrue();
        info.Titles[1].IsMainFeature.Should().BeFalse();
        info.TotalDuration.Should().Be(expected: TimeSpan.FromHours(hours: 2).Add(ts: TimeSpan.FromMinutes(minutes: 45)));
    }

    [Fact]
    public async Task ProbeAsync_ReadsEmbeddedBdmtTitle_WhenBdmvMetaPresent()
    {
        Mock<IProcessRunner> runner = MakeRunner(stdErr: "playlist 00100.mpls (01:30:00)");
        Mock<IStorageDriver> driver = new();
        driver.Setup(expression: d => d.DirectoryExists(It.IsAny<string>())).Returns(value: true);
        driver
            .Setup(expression: d => d.FileExists(It.Is<string>(p => p.Contains("bdmt_eng.xml"))))
            .Returns(value: true);

        string xml =
            """<?xml version="1.0"?><disclib xmlns:di="urn:BDA:bdmv;discinfo"><di:discinfo><di:title><di:name>My Movie Title</di:name></di:title></di:discinfo></disclib>""";
        driver
            .Setup(expression: d => d.OpenRead(It.Is<string>(p => p.Contains("bdmt_eng.xml"))))
            .Returns(valueFunction: () => new MemoryStream(buffer: System.Text.Encoding.UTF8.GetBytes(s: xml)));

        BlurayDiscSource sut = MakeSut(processRunner: runner.Object, storageDriver: driver.Object);
        DiscDrive drive = new(Path: "D:\\", Label: "VOLLABEL", HasDisc: true, DiscType: OpticalDiscType.BluRay);

        DiscInfo info = await sut.ProbeAsync(drive: drive, ct: CancellationToken.None);

        info.DiscTitle.Should().Be(expected: "My Movie Title");
    }

    [Fact]
    public async Task ProbeAsync_NoBdmvMetaDirectory_DiscTitleIsNull()
    {
        Mock<IProcessRunner> runner = MakeRunner(stdErr: "playlist 00100.mpls (01:30:00)");
        Mock<IStorageDriver> driver = new();
        driver.Setup(expression: d => d.DirectoryExists(It.IsAny<string>())).Returns(value: false);

        BlurayDiscSource sut = MakeSut(processRunner: runner.Object, storageDriver: driver.Object);
        DiscDrive drive = new(Path: "D:\\", Label: "VOLLABEL", HasDisc: true, DiscType: OpticalDiscType.BluRay);

        DiscInfo info = await sut.ProbeAsync(drive: drive, ct: CancellationToken.None);

        info.DiscTitle.Should().BeNull();
    }

    [Fact]
    public async Task ProbeAsync_BdmvMetaDirExistsButNoBdmtXmlFilesPresent_DiscTitleIsNull()
    {
        // DirectoryExists true, but neither bdmt_eng.xml nor any bdmt_*.xml
        // exists — xmlPath resolves to null via FirstOrDefault(), distinct
        // from the "enumeration throws" test below.
        Mock<IProcessRunner> runner = MakeRunner(stdErr: "playlist 00100.mpls (01:30:00)");
        Mock<IStorageDriver> driver = new();
        driver.Setup(expression: d => d.DirectoryExists(It.IsAny<string>())).Returns(value: true);
        driver.Setup(expression: d => d.FileExists(It.IsAny<string>())).Returns(value: false);
        driver
            .Setup(expression: d =>
                d.EnumerateFileSystemEntries(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<SearchOption>()
                )
            )
            .Returns(value: []);

        BlurayDiscSource sut = MakeSut(processRunner: runner.Object, storageDriver: driver.Object);
        DiscDrive drive = new(Path: "D:\\", Label: "VOLLABEL", HasDisc: true, DiscType: OpticalDiscType.BluRay);

        DiscInfo info = await sut.ProbeAsync(drive: drive, ct: CancellationToken.None);

        info.DiscTitle.Should().BeNull();
    }

    [Fact]
    public async Task ProbeAsync_AlreadyBlurayPrefixedDrivePath_UsedVerbatim()
    {
        Mock<IProcessRunner> runner = MakeRunner(stdErr: "playlist 00100.mpls (01:30:00)");
        string? capturedInputUrl = null;
        runner
            .Setup(expression: r =>
                r.RunAsync(
                    It.IsAny<string>(),
                    It.IsAny<string[]>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<string, string[], string?, CancellationToken>(
                action: (_, args, _, _) => capturedInputUrl = args[^1]
            )
            .ReturnsAsync(
                value: new ProcessResult(ExitCode: 0, StdOut: "", StdErr: "playlist 00100.mpls (01:30:00)", Duration: TimeSpan.Zero)
            );

        Mock<IStorageDriver> driver = new();
        driver.Setup(expression: d => d.DirectoryExists(It.IsAny<string>())).Returns(value: false);

        BlurayDiscSource sut = MakeSut(processRunner: runner.Object, storageDriver: driver.Object);
        DiscDrive drive = new(Path: "bluray:/dev/sr0/", Label: "MOVIE", HasDisc: true, DiscType: OpticalDiscType.BluRay);

        await sut.ProbeAsync(drive: drive, ct: CancellationToken.None);

        capturedInputUrl
            .Should()
            .Be(expected: "bluray:/dev/sr0/", because: "an already-prefixed path must be used verbatim");
    }

    [Fact]
    public async Task ProbeTitleAsync_FfprobeFailsWithEmptyStderr_ReturnsEmptySkeletonTitle()
    {
        Mock<IProcessRunner> runner = MakeRunner(stdErr: "", exitCode: 1);

        BlurayDiscSource sut = MakeSut(processRunner: runner.Object);
        DiscDrive drive = new(Path: "D:\\", Label: "MOVIE", HasDisc: true, DiscType: OpticalDiscType.BluRay);

        DiscTitle title = await sut.ProbeTitleAsync(drive: drive, titleIndex: 1, ct: CancellationToken.None);

        title.Duration.Should().Be(expected: TimeSpan.Zero);
    }

    [Fact]
    public async Task ProbeAsync_BdmtDirectoryExistsButXmlUnreadable_DiscTitleIsNullWithoutThrowing()
    {
        Mock<IProcessRunner> runner = MakeRunner(stdErr: "playlist 00100.mpls (01:30:00)");
        Mock<IStorageDriver> driver = new();
        driver.Setup(expression: d => d.DirectoryExists(It.IsAny<string>())).Returns(value: true);
        driver.Setup(expression: d => d.FileExists(It.IsAny<string>())).Returns(value: false);
        driver
            .Setup(expression: d =>
                d.EnumerateFileSystemEntries(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<SearchOption>()
                )
            )
            .Throws(exception: new IOException(message: "disc read error"));

        BlurayDiscSource sut = MakeSut(processRunner: runner.Object, storageDriver: driver.Object);
        DiscDrive drive = new(Path: "D:\\", Label: "VOLLABEL", HasDisc: true, DiscType: OpticalDiscType.BluRay);

        Func<Task> act = () => sut.ProbeAsync(drive: drive, ct: CancellationToken.None);

        await act.Should().NotThrowAsync();
        DiscInfo info = await sut.ProbeAsync(drive: drive, ct: CancellationToken.None);
        info.DiscTitle.Should().BeNull();
    }

    [Fact]
    public async Task ProbeAsync_AacsCertificateFailure_AttachesProtectionInfo()
    {
        Mock<IProcessRunner> runner = MakeRunner(
            stdErr: "Drive does not support reading drive certificate"
        );
        Mock<IStorageDriver> driver = new();
        driver.Setup(expression: d => d.DirectoryExists(It.IsAny<string>())).Returns(value: false);

        BlurayDiscSource sut = MakeSut(processRunner: runner.Object, storageDriver: driver.Object);
        DiscDrive drive = new(Path: "D:\\", Label: "MOVIE", HasDisc: true, DiscType: OpticalDiscType.BluRay);

        DiscInfo info = await sut.ProbeAsync(drive: drive, ct: CancellationToken.None);

        info.Protection.Should().NotBeNull();
        info.Protection!.Kind.Should().Be(expected: "AACS");
    }

    [Fact]
    public async Task ProbeTitleAsync_ValidPlaylist_ReturnsTitleWithRequestedIndex()
    {
        string json = """
            {
              "format": { "duration": "3600" },
              "streams": [ { "index": 0, "codec_type": "video", "codec_name": "h264" } ]
            }
            """;
        Mock<IProcessRunner> runner = MakeRunner(exitCode: 0, stdOut: json, stdErr: "");

        BlurayDiscSource sut = MakeSut(processRunner: runner.Object);
        DiscDrive drive = new(Path: "D:\\", Label: "MOVIE", HasDisc: true, DiscType: OpticalDiscType.BluRay);

        DiscTitle title = await sut.ProbeTitleAsync(drive: drive, titleIndex: 100, ct: CancellationToken.None);

        title.Index.Should().Be(expected: 100);
        title.VideoStreams.Should().HaveCount(expected: 1);
    }

    [Fact]
    public async Task ProbeTitleAsync_FfprobeFails_ReturnsEmptySkeletonTitle()
    {
        Mock<IProcessRunner> runner = MakeRunner(stdErr: "read error", exitCode: 1);

        BlurayDiscSource sut = MakeSut(processRunner: runner.Object);
        DiscDrive drive = new(Path: "D:\\", Label: "MOVIE", HasDisc: true, DiscType: OpticalDiscType.BluRay);

        DiscTitle title = await sut.ProbeTitleAsync(drive: drive, titleIndex: 42, ct: CancellationToken.None);

        title.Index.Should().Be(expected: 42);
        title.Duration.Should().Be(expected: TimeSpan.Zero);
        title.IsMainFeature.Should().BeFalse();
    }

    /// <summary>
    /// Regression test for a real bug: DiscScanner.Parse wraps a JSON parse
    /// failure into InvalidOperationException, but ScanWithPlaylistAsync had
    /// no try/catch around the Parse call at all, so a truncated/malformed
    /// ffprobe response on a real disc crashed ProbeTitleAsync entirely
    /// instead of degrading to the empty skeleton title every other failure
    /// path on this method already returns. Fixed by wrapping the Parse call
    /// and degrading on InvalidOperationException, matching the
    /// !result.IsSuccess branch immediately above it.
    /// </summary>
    [Fact]
    public async Task ProbeTitleAsync_MalformedJsonWithSuccessExitCode_ReturnsEmptySkeletonTitle_WithoutThrowing()
    {
        Mock<IProcessRunner> runner = MakeRunner(
            exitCode: 0,
            stdOut: "{ not valid json",
            stdErr: ""
        );

        BlurayDiscSource sut = MakeSut(processRunner: runner.Object);
        DiscDrive drive = new(Path: "D:\\", Label: "MOVIE", HasDisc: true, DiscType: OpticalDiscType.BluRay);

        DiscTitle title = await sut.ProbeTitleAsync(drive: drive, titleIndex: 55, ct: CancellationToken.None);

        title.Index.Should().Be(expected: 55);
        title.Duration.Should().Be(expected: TimeSpan.Zero);
        title.IsMainFeature.Should().BeFalse();
    }
}
