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
using NoMercy.Encoder.Subtitles;
using SubtitlePolicy = NoMercy.Encoder.Profiles.SubtitlePolicy;

namespace NoMercy.Tests.Encoder.Subtitles;

public class SubtitleRouterTests
{
    private readonly SubtitleRouter _router = new();

    public static TheoryData<
        SubtitleSourceType,
        OutputFormat,
        SubtitlePolicy,
        SubtitleAction
    > SpecMatrix =>
        new()
        {
            // text → mkv → copy regardless of policy
            {
                SubtitleSourceType.Text,
                OutputFormat.Mkv,
                SubtitlePolicy.Extract,
                SubtitleAction.Copy
            },
            { SubtitleSourceType.Text, OutputFormat.Mkv, SubtitlePolicy.Copy, SubtitleAction.Copy },
            // text → hls → extract → vtt (segmented)
            {
                SubtitleSourceType.Text,
                OutputFormat.Hls,
                SubtitlePolicy.Extract,
                SubtitleAction.ExtractVtt
            },
            // text → mp4 → extract → mov_text (embedded). Copy → sidecar.
            {
                SubtitleSourceType.Text,
                OutputFormat.Mp4,
                SubtitlePolicy.Extract,
                SubtitleAction.MovText
            },
            {
                SubtitleSourceType.Text,
                OutputFormat.Mp4,
                SubtitlePolicy.Copy,
                SubtitleAction.ExtractVttSidecar
            },
            // text → dash → vtt sidecar
            {
                SubtitleSourceType.Text,
                OutputFormat.Dash,
                SubtitlePolicy.Extract,
                SubtitleAction.ExtractVttSidecar
            },
            // bitmap → mkv → copy
            {
                SubtitleSourceType.Bitmap,
                OutputFormat.Mkv,
                SubtitlePolicy.Extract,
                SubtitleAction.Copy
            },
            // bitmap → hls → ocr (or BurnIn when explicit)
            {
                SubtitleSourceType.Bitmap,
                OutputFormat.Hls,
                SubtitlePolicy.Extract,
                SubtitleAction.Ocr
            },
            {
                SubtitleSourceType.Bitmap,
                OutputFormat.Hls,
                SubtitlePolicy.BurnIn,
                SubtitleAction.BurnIn
            },
            // bitmap → mp4 → ocr sidecar (or BurnIn)
            {
                SubtitleSourceType.Bitmap,
                OutputFormat.Mp4,
                SubtitlePolicy.Extract,
                SubtitleAction.OcrSidecar
            },
            {
                SubtitleSourceType.Bitmap,
                OutputFormat.Mp4,
                SubtitlePolicy.BurnIn,
                SubtitleAction.BurnIn
            },
            // bitmap → dash → ocr sidecar (or BurnIn)
            {
                SubtitleSourceType.Bitmap,
                OutputFormat.Dash,
                SubtitlePolicy.Extract,
                SubtitleAction.OcrSidecar
            },
            {
                SubtitleSourceType.Bitmap,
                OutputFormat.Dash,
                SubtitlePolicy.BurnIn,
                SubtitleAction.BurnIn
            },
        };

    [Theory]
    [MemberData(nameof(SpecMatrix))]
    public void Resolve_returns_expected_action(
        SubtitleSourceType source,
        OutputFormat container,
        SubtitlePolicy policy,
        SubtitleAction expected
    )
    {
        SubtitleRouting routing = _router.Resolve(source, container, policy);
        routing.Action.Should().Be(expected);
    }

    [Theory]
    [InlineData(OutputFormat.Mp3)]
    [InlineData(OutputFormat.Flac)]
    [InlineData(OutputFormat.Ogg)]
    public void Resolve_audio_only_container_returns_None_with_reason(OutputFormat container)
    {
        SubtitleRouting textRouting = _router.Resolve(
            SubtitleSourceType.Text,
            container,
            SubtitlePolicy.Extract
        );
        SubtitleRouting bitmapRouting = _router.Resolve(
            SubtitleSourceType.Bitmap,
            container,
            SubtitlePolicy.BurnIn
        );

        textRouting.Action.Should().Be(SubtitleAction.None);
        textRouting.Reason.Should().Contain("no subtitle support");

        // BurnIn doesn't sneak past the audio-only guard.
        bitmapRouting.Action.Should().Be(SubtitleAction.None);
    }

    [Fact]
    public void Resolve_BurnIn_overrides_source_type_when_container_supports_video()
    {
        // BurnIn is policy-driven, not source-driven — text + hls + BurnIn still burns.
        SubtitleRouting routing = _router.Resolve(
            SubtitleSourceType.Text,
            OutputFormat.Hls,
            SubtitlePolicy.BurnIn
        );
        routing.Action.Should().Be(SubtitleAction.BurnIn);
    }

    [Fact]
    public void Resolve_Omit_returns_None_for_any_container()
    {
        SubtitleRouting routing = _router.Resolve(
            SubtitleSourceType.Text,
            OutputFormat.Hls,
            SubtitlePolicy.Omit
        );
        routing.Action.Should().Be(SubtitleAction.None);
        routing.Reason.Should().Contain("omitted by policy");
    }
}
