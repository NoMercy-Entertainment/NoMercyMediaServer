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

using NoMercy.Encoder.Codecs;
using NoMercy.Encoder.Decomposition;
using NoMercy.Encoder.Profiles;
using NoMercy.Encoder.Reconciliation;

namespace NoMercy.Tests.Encoder.Reconciliation;

/// <summary>
/// Unit tests for the reconciliation DECISION only — no filesystem, no
/// database. Every scenario hands a hand-built <see cref="ExistingOutputSnapshot"/>
/// straight to <see cref="EncodeReconciler.Decide"/> and asserts the verdict.
/// </summary>
public class EncodeReconcilerDecideTests
{
    private readonly EncodeReconciler _reconciler = new();

    [Fact]
    public void Decide_ReturnsSkip_WhenFingerprintMatchesAndEveryOutputIsPresentAndValid()
    {
        EncodingProfile profile = MakeHlsProfile();
        string fingerprint = ProfileFingerprint.Compute(profile: profile);
        ExistingOutputSnapshot existing = new(
            ProfileFingerprint: fingerprint,
            BundleFiles: AllPresentFiles(),
            ValidOcrSidecarCount: 0
        );

        ReconciliationDecision decision = _reconciler.Decide(
            input: new(Profile: profile, IsSingleFileOutput: false, BitmapSubtitleStreamCount: 0, Existing: existing)
        );

        decision.Action.Should().Be(expected: ReconciliationAction.Skip);
        decision.MissingKinds.Should().BeEmpty();
        decision.NeedsSubtitleOcr.Should().BeFalse();
    }

    [Fact]
    public void Decide_ReturnsPartialOcrOnly_WhenFingerprintMatchesButBitmapOcrVttIsMissing_FrierenRegression()
    {
        // This is the exact regression the reconciler exists to fix: video,
        // audio, and declared subtitles are all valid and on-profile — only
        // the bitmap-subtitle OCR sidecar is missing. Re-dispatching must
        // never re-run Build/Execute for this file; MissingKinds must stay
        // empty and only NeedsSubtitleOcr should be set.
        EncodingProfile profile = MakeHlsProfile();
        string fingerprint = ProfileFingerprint.Compute(profile: profile);
        ExistingOutputSnapshot existing = new(
            ProfileFingerprint: fingerprint,
            BundleFiles: AllPresentFiles(),
            ValidOcrSidecarCount: 0
        );

        ReconciliationDecision decision = _reconciler.Decide(
            input: new(Profile: profile, IsSingleFileOutput: false, BitmapSubtitleStreamCount: 1, Existing: existing)
        );

        decision.Action.Should().Be(expected: ReconciliationAction.Partial);
        decision.MissingKinds.Should().BeEmpty(because: "nothing in the decomposed plan is missing");
        decision.NeedsSubtitleOcr.Should().BeTrue();
    }

    [Fact]
    public void Decide_TreatsTheRealSpriteFilenameAsPresent_WhenThumbnailsComeFromTheGenerateSpriteVttDefault()
    {
        // The preset leaves Thumbnails null and inherits GenerateSpriteVtt, which
        // is how every HLS preset here gets its sprite. ThumbnailGenerator writes
        // that sprite as thumbs_{W}x{H}.webp at the default 160px width, so an
        // output carrying thumbs_160x90.webp is complete and must not re-run the
        // thumbnail pass.
        EncodingProfile profile = MakeHlsProfile();
        profile.Thumbnails.Should().BeNull(because: "this preset relies on the sprite default");

        ReconciliationDecision decision = _reconciler.Decide(
            input: new(
                Profile: profile,
                IsSingleFileOutput: false,
                BitmapSubtitleStreamCount: 0,
                Existing: new(ProfileFingerprint: ProfileFingerprint.Compute(profile: profile), BundleFiles: AllPresentFiles(), ValidOcrSidecarCount: 0)
            )
        );

        decision.MissingKinds.Should().NotContain(unexpected: EncodeTaskKind.Thumbnails);
        decision.Action.Should().Be(expected: ReconciliationAction.Skip);
    }

    [Fact]
    public void Decide_ReturnsPartialThumbnails_WhenTheSpriteIsMissingUnderTheGenerateSpriteVttDefault()
    {
        // Counterpart to the test above: same preset, sprite absent. A preset that
        // never sets Thumbnails still wants one, so the gap must be topped up.
        EncodingProfile profile = MakeHlsProfile();
        List<ExistingOutputEntry> withoutSprite = AllPresentFiles()
            .Where(predicate: f => !f.RelativePath.StartsWith(value: "thumbs_", comparisonType: StringComparison.Ordinal))
            .ToList();

        ReconciliationDecision decision = _reconciler.Decide(
            input: new(
                Profile: profile,
                IsSingleFileOutput: false,
                BitmapSubtitleStreamCount: 0,
                Existing: new(ProfileFingerprint: ProfileFingerprint.Compute(profile: profile), BundleFiles: withoutSprite, ValidOcrSidecarCount: 0)
            )
        );

        decision.Action.Should().Be(expected: ReconciliationAction.Partial);
        decision.MissingKinds.Should().Contain(expected: EncodeTaskKind.Thumbnails);
        decision.MissingKinds.Should().NotContain(unexpected: EncodeTaskKind.Video);
    }

    [Fact]
    public void Decide_ReturnsPartialSubtitleOnly_WhenTheDeclaredSubtitleTrackIsMissing()
    {
        EncodingProfile profile = MakeHlsProfile();
        string fingerprint = ProfileFingerprint.Compute(profile: profile);
        List<ExistingOutputEntry> files = AllPresentFiles()
            .Where(predicate: f => !f.RelativePath.StartsWith(value: "subtitles/", comparisonType: StringComparison.Ordinal))
            .ToList();
        ExistingOutputSnapshot existing = new(ProfileFingerprint: fingerprint, BundleFiles: files, ValidOcrSidecarCount: 0);

        ReconciliationDecision decision = _reconciler.Decide(
            input: new(Profile: profile, IsSingleFileOutput: false, BitmapSubtitleStreamCount: 0, Existing: existing)
        );

        decision.Action.Should().Be(expected: ReconciliationAction.Partial);
        decision.MissingKinds.Should().ContainSingle().Which.Should().Be(expected: EncodeTaskKind.Subtitle);
        decision.NeedsSubtitleOcr.Should().BeFalse();
    }

    [Fact]
    public void Decide_ReturnsPartialAudioOnly_WhenTheAudioRenditionIsMissing()
    {
        // The video-present / audio-missing case: deleting an audio_* rendition
        // must top up only the audio, exactly as a missing subtitle or thumbnail
        // does — never a full re-encode of the video that is already on disk.
        // Confirmed live against a real special (0 video / 1 audio bundle); pinned
        // here so that decomposed-bundle path cannot regress unseen.
        EncodingProfile profile = MakeHlsProfile();
        string fingerprint = ProfileFingerprint.Compute(profile: profile);
        List<ExistingOutputEntry> files = AllPresentFiles()
            .Where(predicate: f => !f.RelativePath.StartsWith(value: "audio_", comparisonType: StringComparison.Ordinal))
            .ToList();
        ExistingOutputSnapshot existing = new(ProfileFingerprint: fingerprint, BundleFiles: files, ValidOcrSidecarCount: 0);

        ReconciliationDecision decision = _reconciler.Decide(
            input: new(Profile: profile, IsSingleFileOutput: false, BitmapSubtitleStreamCount: 0, Existing: existing)
        );

        decision.Action.Should().Be(expected: ReconciliationAction.Partial);
        decision.MissingKinds.Should().ContainSingle().Which.Should().Be(expected: EncodeTaskKind.Audio);
        decision.MissingKinds.Should().NotContain(unexpected: EncodeTaskKind.Video);
    }

    [Fact]
    public void Decide_ReturnsFull_WhenTheProfileFingerprintDiffers()
    {
        EncodingProfile profile = MakeHlsProfile();
        ExistingOutputSnapshot existing = new(
            ProfileFingerprint: "stale-fingerprint-from-before-the-preset-was-edited",
            BundleFiles: AllPresentFiles(),
            ValidOcrSidecarCount: 0
        );

        ReconciliationDecision decision = _reconciler.Decide(
            input: new(Profile: profile, IsSingleFileOutput: false, BitmapSubtitleStreamCount: 0, Existing: existing)
        );

        decision.Action.Should().Be(expected: ReconciliationAction.Full);
        decision.MissingKinds.Should().BeEmpty();
    }

    [Fact]
    public void Decide_ReturnsSkip_WhenNoFingerprintIsOnRecordButEveryOutputIsPresent_UpgradeSafety()
    {
        // Every real output on disk today predates fingerprinting. Treating
        // "no fingerprint" as "profile changed" would force a full re-encode
        // of an operator's entire library the moment the server upgrades —
        // unacceptable for a self-hosted product.
        EncodingProfile profile = MakeHlsProfile();
        ExistingOutputSnapshot existing = new(ProfileFingerprint: null, BundleFiles: AllPresentFiles(), ValidOcrSidecarCount: 0);

        ReconciliationDecision decision = _reconciler.Decide(
            input: new(Profile: profile, IsSingleFileOutput: false, BitmapSubtitleStreamCount: 0, Existing: existing)
        );

        decision.Action.Should().Be(expected: ReconciliationAction.Skip);
    }

    [Fact]
    public void Decide_ReturnsPartialOcrOnly_WhenNoFingerprintAndBitmapOcrVttIsMissing()
    {
        EncodingProfile profile = MakeHlsProfile();
        ExistingOutputSnapshot existing = new(ProfileFingerprint: null, BundleFiles: AllPresentFiles(), ValidOcrSidecarCount: 0);

        ReconciliationDecision decision = _reconciler.Decide(
            input: new(Profile: profile, IsSingleFileOutput: false, BitmapSubtitleStreamCount: 2, Existing: existing)
        );

        decision.Action.Should().Be(expected: ReconciliationAction.Partial);
        decision.MissingKinds.Should().BeEmpty();
        decision.NeedsSubtitleOcr.Should().BeTrue();
    }

    [Fact]
    public void Decide_TreatsAZeroByteOutputAsInvalid_AndRePairsOnlyThatKind()
    {
        EncodingProfile profile = MakeHlsProfile();
        string fingerprint = ProfileFingerprint.Compute(profile: profile);
        List<ExistingOutputEntry> files = AllPresentFiles()
            .Select(selector: f =>
                f.RelativePath.StartsWith(value: "audio_", comparisonType: StringComparison.Ordinal)
                    ? f with
                    {
                        SizeBytes = 0,
                    }
                    : f
            )
            .ToList();
        ExistingOutputSnapshot existing = new(ProfileFingerprint: fingerprint, BundleFiles: files, ValidOcrSidecarCount: 0);

        ReconciliationDecision decision = _reconciler.Decide(
            input: new(Profile: profile, IsSingleFileOutput: false, BitmapSubtitleStreamCount: 0, Existing: existing)
        );

        decision.Action.Should().Be(expected: ReconciliationAction.Partial);
        decision.MissingKinds.Should().ContainSingle().Which.Should().Be(expected: EncodeTaskKind.Audio);
    }

    [Fact]
    public void Decide_ReturnsFull_WhenForced_RegardlessOfFingerprintOrWhatIsPresent()
    {
        EncodingProfile profile = MakeHlsProfile();
        string fingerprint = ProfileFingerprint.Compute(profile: profile);
        ExistingOutputSnapshot existing = new(
            ProfileFingerprint: fingerprint,
            BundleFiles: AllPresentFiles(),
            ValidOcrSidecarCount: 5
        );

        ReconciliationDecision decision = _reconciler.Decide(
            input: new(
                Profile: profile,
                IsSingleFileOutput: false,
                BitmapSubtitleStreamCount: 0,
                Existing: existing,
                Force: true
            )
        );

        decision.Action.Should().Be(expected: ReconciliationAction.Full);
    }

    [Fact]
    public void Decide_ReturnsFull_WhenTheMasterPlaylistIsMissing_EvenIfTheFingerprintMatches()
    {
        EncodingProfile profile = MakeHlsProfile();
        string fingerprint = ProfileFingerprint.Compute(profile: profile);
        List<ExistingOutputEntry> files = AllPresentFiles()
            .Where(predicate: f =>
                f.RelativePath.Contains(value: '/')
                || !f.RelativePath.EndsWith(value: ".m3u8", comparisonType: StringComparison.Ordinal)
            )
            .ToList();
        ExistingOutputSnapshot existing = new(ProfileFingerprint: fingerprint, BundleFiles: files, ValidOcrSidecarCount: 0);

        ReconciliationDecision decision = _reconciler.Decide(
            input: new(Profile: profile, IsSingleFileOutput: false, BitmapSubtitleStreamCount: 0, Existing: existing)
        );

        decision.Action.Should().Be(expected: ReconciliationAction.Full);
    }

    [Fact]
    public void Decide_ReturnsFull_ForASingleFileContainer_WhenTheOutputIsMissing()
    {
        EncodingProfile profile = MakeMkvProfile();

        ReconciliationDecision decision = _reconciler.Decide(
            input: new(
                Profile: profile,
                IsSingleFileOutput: true,
                BitmapSubtitleStreamCount: 0,
                Existing: ExistingOutputSnapshot.Empty
            )
        );

        decision.Action.Should().Be(expected: ReconciliationAction.Full);
    }

    [Fact]
    public void Decide_ReturnsPartialOcrOnly_ForASingleFileContainer_WhenTheFileIsValidButOcrIsMissing()
    {
        EncodingProfile profile = MakeMkvProfile();
        ExistingOutputSnapshot existing = new(
            ProfileFingerprint: null,
            BundleFiles: [new ExistingOutputEntry(RelativePath: "Movie Title.NoMercy.mkv", SizeBytes: 900_000_000)],
            ValidOcrSidecarCount: 0
        );

        ReconciliationDecision decision = _reconciler.Decide(
            input: new(Profile: profile, IsSingleFileOutput: true, BitmapSubtitleStreamCount: 1, Existing: existing)
        );

        decision.Action.Should().Be(expected: ReconciliationAction.Partial);
        decision.MissingKinds.Should().BeEmpty();
        decision.NeedsSubtitleOcr.Should().BeTrue();
    }

    // ------------------------------------------------------------------
    // ── Chapters vs a source that has none ───────────────────────────────────
    // FinalizeStage writes chapters.vtt only when the source has chapters, so a
    // source with none can never satisfy a demand for it. Asking anyway left the
    // media permanently incomplete: every dispatch flagged Chapters missing,
    // decomposition had no Chapters task to offer, and the job fell back to a
    // full re-encode — of a file that was already finished.

    [Fact]
    public void Decide_DoesNotAskForChapters_WhenTheSourceHasNone()
    {
        EncodingProfile profile = MakeHlsProfile();
        ExistingOutputSnapshot existing = new(
            ProfileFingerprint: ProfileFingerprint.Compute(profile: profile),
            BundleFiles: FilesWithoutChapters(),
            ValidOcrSidecarCount: 0
        );

        ReconciliationDecision decision = _reconciler.Decide(
            input: new(
                Profile: profile,
                IsSingleFileOutput: false,
                BitmapSubtitleStreamCount: 0,
                Existing: existing,
                SourceChapterCount: 0
            )
        );

        decision.MissingKinds.Should().NotContain(unexpected: EncodeTaskKind.Chapters);
        decision
            .Action.Should()
            .Be(
                expected: ReconciliationAction.Skip,
                because: "everything the source can produce is present, so there is nothing to do"
            );
    }

    [Fact]
    public void Decide_StillAsksForChapters_WhenTheSourceHasThemAndTheVttIsMissing()
    {
        EncodingProfile profile = MakeHlsProfile();
        ExistingOutputSnapshot existing = new(
            ProfileFingerprint: ProfileFingerprint.Compute(profile: profile),
            BundleFiles: FilesWithoutChapters(),
            ValidOcrSidecarCount: 0
        );

        ReconciliationDecision decision = _reconciler.Decide(
            input: new(
                Profile: profile,
                IsSingleFileOutput: false,
                BitmapSubtitleStreamCount: 0,
                Existing: existing,
                SourceChapterCount: 6
            )
        );

        decision
            .MissingKinds.Should()
            .Contain(
                expected: EncodeTaskKind.Chapters,
                because: "the source has chapters, so a missing chapters.vtt is a real gap"
            );
    }

    // ------------------------------------------------------------------
    // Fixtures
    // ------------------------------------------------------------------

    private static List<ExistingOutputEntry> FilesWithoutChapters() =>
        AllPresentFiles().Where(predicate: f => f.RelativePath != "chapters.vtt").ToList();

    // Names here mirror what the encoder actually writes. The sprite pair in
    // particular comes from ThumbnailGenerator's `thumbs_{W}x{H}` shape at the
    // default 160px width — inventing a name that merely matches the reconciler
    // would prove nothing about a real output directory.
    private static List<ExistingOutputEntry> AllPresentFiles() =>
        [
            new(RelativePath: "web-1080p_master.m3u8", SizeBytes: 500),
            new(RelativePath: "video_1920x1080_sdr/video_1920x1080_sdr.m3u8", SizeBytes: 300),
            new(RelativePath: "audio_eng_aac/audio_eng_aac.m3u8", SizeBytes: 200),
            new(RelativePath: "subtitles/eng.vtt", SizeBytes: 150),
            new(RelativePath: "chapters.vtt", SizeBytes: 80),
            new(RelativePath: "thumbs_160x90.webp", SizeBytes: 298_000),
            new(RelativePath: "thumbs_160x90.vtt", SizeBytes: 4_000),
        ];

    private static EncodingProfile MakeHlsProfile() =>
        new(
            Id: Ulid.NewUlid(),
            Name: "web-1080p",
            Container: Container.HlsFmp4,
            Video: MakeVideoOutput(),
            Audio: [MakeAudioOutput()],
            Subtitles: [MakeSubtitleOutput()]
        );

    private static EncodingProfile MakeMkvProfile() =>
        new(
            Id: Ulid.NewUlid(),
            Name: "archive-mkv",
            Container: Container.Mkv,
            Video: MakeVideoOutput(),
            Audio: [MakeAudioOutput()],
            Subtitles: []
        );

    private static VideoOutput MakeVideoOutput() =>
        new(
            Policy: StreamPolicy.Transcode,
            Codec: VideoCodecType.H264,
            Width: 1920,
            Height: 1080,
            RateControl: NoMercy.Encoder.Profiles.RateControlMode.Crf,
            Crf: 23,
            BitrateKbps: 4000,
            MaxBitrateKbps: null,
            BufferSizeKbps: null,
            Preset: "medium",
            CodecProfile: CodecProfile.High,
            Level: null,
            Tune: null,
            BitDepth: 8,
            PixelFormat: null,
            KeyframeIntervalSeconds: 4,
            ConvertHdrToSdr: false,
            SegmentNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:",
            PlaylistNameTemplate: ":type:_:framesize:_:colorrange:/:type:_:framesize:_:colorrange:"
        );

    private static AudioOutput MakeAudioOutput() =>
        new(
            Policy: StreamPolicy.Transcode,
            Codec: AudioCodecType.Aac,
            BitrateKbps: 128,
            Channels: 2,
            SampleRateHz: 48000,
            AllowedLanguages: ["eng"],
            DefaultLanguage: "eng",
            Loudness: null,
            Downmix: null,
            SegmentNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:",
            PlaylistNameTemplate: ":type:_:language:_:codec:/:type:_:language:_:codec:"
        );

    private static SubtitleOutput MakeSubtitleOutput() =>
        new(
            Policy: SubtitlePolicy.Extract,
            Codec: SubtitleCodecType.WebVtt,
            AllowedLanguages: ["eng"],
            IncludeForced: false,
            OcrLanguage: null,
            PlaylistNameTemplate: "subtitles/:filename:.:language:.:variant:"
        );
}
