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

namespace NoMercy.Tests.MediaProcessing.Jobs;

/// <summary>
/// Regression guard for the "reconstruction.json / manifest.json missing for
/// every Whole-task (inline) encode" gap: <c>VideoEncodeJob.RunInlineAsync</c>
/// runs Build+Execute+Finalize over the SAME <c>EncodingRequest</c> instance
/// <c>HandleInitialRunAsync</c> builds — the coordinator state machine
/// (<c>HandleCoordinatorWakeUpAsync</c> → <c>Finalize</c> → <c>HandleFinalizeAsync</c>)
/// is never reached for a single-file Whole task, so that request is the only
/// place the inline path can pick up <c>MediaItemRef</c> identity from.
///
/// These tests operate on the source text (same convention as
/// <see cref="PhaseTransitionTests"/>) because exercising the real code path
/// end-to-end requires a live <c>MediaContext</c> + seeded Movie/Episode rows
/// + queue runner; the pipeline-level behavior (manifest/reconstruction
/// actually landing on disk once MediaItem is attached) is covered instead by
/// <c>NoMercy.Tests.Encoder.Pipeline.EncoderTests</c>, which exercises the
/// identical request shape through the real <c>Encoder</c>.
/// </summary>
[Trait("Category", "Unit")]
public class InlineEncodeMediaItemWiringTests
{
    private static string? _cachedSource;

    private static string LoadVideoEncodeJobSource()
    {
        if (_cachedSource is not null)
            return _cachedSource;

        string? dir = AppDomain.CurrentDomain.BaseDirectory;

        while (dir is not null)
        {
            string srcCandidate = Path.Combine(dir, "src");
            if (Directory.Exists(srcCandidate))
            {
                string[] files = Directory.GetFiles(
                    srcCandidate,
                    "VideoEncodeJob.cs",
                    SearchOption.AllDirectories
                );

                if (files.Length > 0)
                {
                    _cachedSource = File.ReadAllText(files[0]);
                    return _cachedSource;
                }
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new FileNotFoundException("VideoEncodeJob.cs not found under any src/ ancestor");
    }

    private static string ExtractMethodWindow(string source, int methodStart, int maxChars = 2000)
    {
        int braceDepth = 0;
        bool foundFirstBrace = false;
        int methodEnd = methodStart;

        for (int charIndex = methodStart; charIndex < source.Length; charIndex++)
        {
            char current = source[charIndex];

            if (current == '{')
            {
                braceDepth++;
                foundFirstBrace = true;
            }
            else if (current == '}')
            {
                braceDepth--;
                if (foundFirstBrace && braceDepth == 0)
                {
                    methodEnd = charIndex;
                    break;
                }
            }
        }

        int length = Math.Min(methodEnd - methodStart + 1, maxChars);
        return source.Substring(methodStart, length);
    }

    [Fact]
    public void RunSinglePresetEncodeAsync_RequestReachingRunInlineAsync_CarriesMediaItem()
    {
        // This is the actual gap: before the fix, the EncodingRequest built for
        // a preset (the one RunInlineAsync passes straight into
        // orchestrator.EncodeAsync for a Whole task) never set MediaItem, so
        // PlanStage never resolved a BundleLayout and FinalizeStage's manifest/
        // reconstruction gate was always skipped for every inline encode.
        //
        // The smart-orchestrator unification moved per-preset dispatch out of
        // HandleInitialRunAsync (which now only resolves + reconciles presets
        // and picks the merged vs. per-preset path) and into
        // RunSinglePresetEncodeAsync — the method that still builds the exact
        // request RunInlineAsync consumes for a single-file (Whole task)
        // preset, so the guard now targets that method.
        string source = LoadVideoEncodeJobSource();

        int methodStart = source.IndexOf(
            "private async Task RunSinglePresetEncodeAsync",
            StringComparison.Ordinal
        );
        methodStart.Should().BeGreaterThan(0, "RunSinglePresetEncodeAsync must exist");

        // The brace scan is what bounds this window; the cap is only a guard
        // against pathological input. At 8000 it truncated the method it was
        // meant to inspect, so adding one constructor argument above the
        // assertion failed a test about something else entirely.
        string window = ExtractMethodWindow(source, methodStart, maxChars: 40_000);

        int requestStart = window.IndexOf(
            "EncodingRequest request = new(",
            StringComparison.Ordinal
        );
        requestStart
            .Should()
            .BeGreaterThan(0, "RunSinglePresetEncodeAsync must build an EncodingRequest");

        // Isolate just the request-construction call so a MediaItem: mention
        // anywhere later in the (long) method can't produce a false pass.
        int requestEnd = window.IndexOf(");", requestStart, StringComparison.Ordinal);
        requestEnd.Should().BeGreaterThan(requestStart);

        string requestConstruction = window.Substring(requestStart, requestEnd - requestStart);

        requestConstruction
            .Should()
            .Contain(
                "MediaItem:",
                "the request that reaches RunInlineAsync (and DecomposeAsync) must carry "
                    + "MediaItem so the inline Whole-task path resolves a BundleLayout and "
                    + "writes manifest.json/reconstruction.json"
            );

        requestConstruction
            .Should()
            .NotContain(
                "Options:",
                "this request must NOT set EncodingOptions.EnableMetadataInjection — "
                    + "attaching MediaItem here must never change the emitted ffmpeg command"
            );
    }

    [Fact]
    public void RunMergedEncodeAsync_RequestsReachingDecomposeMergedAsync_CarryMediaItem()
    {
        // The smart-orchestrator merged path builds one EncodingRequest per
        // preset before calling DecomposeMergedAsync — RunInlineAsync only
        // ever sees requests[0] out of that list when the merge covers a
        // single preset, so every request built here needs the same
        // MediaItem wiring RunSinglePresetEncodeAsync carries.
        string source = LoadVideoEncodeJobSource();

        int methodStart = source.IndexOf(
            "private async Task RunMergedEncodeAsync",
            StringComparison.Ordinal
        );
        methodStart.Should().BeGreaterThan(0, "RunMergedEncodeAsync must exist");

        string window = ExtractMethodWindow(source, methodStart, maxChars: 4000);

        int requestStart = window.IndexOf("new EncodingRequest(", StringComparison.Ordinal);
        requestStart
            .Should()
            .BeGreaterThan(0, "RunMergedEncodeAsync must build an EncodingRequest per preset");

        int requestEnd = window.IndexOf("))", requestStart, StringComparison.Ordinal);
        requestEnd.Should().BeGreaterThan(requestStart);

        string requestConstruction = window.Substring(requestStart, requestEnd - requestStart);

        requestConstruction
            .Should()
            .Contain(
                "MediaItem:",
                "every request in a merged run must carry MediaItem so DecomposeMergedAsync's "
                    + "plans resolve a BundleLayout the same way a single-preset run does"
            );
    }

    [Fact]
    public void HandleFinalizeAsync_FinalizeRequest_StillCarriesMediaItem()
    {
        // Regression guard for the decomposed/coordinator path fixed by 9dd9041e:
        // the coordinator's FinalizeOnly pass must keep attaching MediaItem so
        // multi-rung (HLS/DASH) encodes keep writing both artifacts too.
        string source = LoadVideoEncodeJobSource();

        int methodStart = source.IndexOf(
            "private async Task HandleFinalizeAsync",
            StringComparison.Ordinal
        );
        methodStart.Should().BeGreaterThan(0, "HandleFinalizeAsync must exist");

        // The brace scan is what bounds this window; the cap is only a guard
        // against pathological input. At 8000 it truncated the method it was
        // meant to inspect, so adding one constructor argument above the
        // assertion failed a test about something else entirely.
        string window = ExtractMethodWindow(source, methodStart, maxChars: 40_000);

        window
            .Should()
            .Contain(
                "MediaItem: fileMetadata.MediaItem",
                "the coordinator finalize request must keep carrying MediaItem"
            );

        window
            .Should()
            .Contain(
                "FinalizeOnly: true",
                "the coordinator finalize request must keep skipping Build+Execute"
            );
    }
}
