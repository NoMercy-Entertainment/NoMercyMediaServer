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

using System.Text;
using Newtonsoft.Json;
using NoMercy.Encoder.Bundle;
using NoMercy.Encoder.Decomposition;
using NoMercy.Encoder.Profiles;
using NoMercy.Storage;

namespace NoMercy.Encoder.Reconciliation;

/// <summary>
/// Default <see cref="IEncodeReconciler"/>. Stateless — every dependency it
/// needs is either a constructor-free pure computation
/// (<see cref="ProfileFingerprint"/>) or passed in as a method parameter, so
/// the decision path stays trivially unit-testable without a filesystem, a
/// database, or a mock <see cref="IStorage"/>.
/// </summary>
public class EncodeReconciler : IEncodeReconciler
{
    /// <summary>Subtitle sidecars produced by the OCR pass embed this marker
    /// in their filename — see <c>SubtitleOcrEngine.ResolveOutputPath</c>
    /// (<c>{lang}.ocr{streamIndex}.{ext}</c>). Declared-subtitle extraction
    /// never emits this marker, so it is what tells the two apart even
    /// though both land in the same <c>subtitles/</c> folder.</summary>
    private const string OcrMarker = ".ocr";

    public ReconciliationDecision Decide(ReconciliationInput input)
    {
        if (input.Force)
            return new(
                ReconciliationAction.Full,
                [],
                input.BitmapSubtitleStreamCount > 0,
                "Force flag set on the job — reconciliation skipped, full re-encode."
            );

        EncodingProfile profile = input.Profile;
        ExistingOutputSnapshot existing = input.Existing;

        bool desiredOcr = input.BitmapSubtitleStreamCount > 0;
        bool hasValidOcr = existing.ValidOcrSidecarCount >= input.BitmapSubtitleStreamCount;
        bool needsOcr = desiredOcr && !hasValidOcr;

        if (input.IsSingleFileOutput)
            return DecideSingleFile(profile, existing, needsOcr);

        bool desiredVideo = profile.Video is not null || profile.Ladder?.Rungs is { Length: > 0 };
        bool desiredAudio = profile.Audio.Length > 0;
        bool desiredSubtitle = profile.Subtitles.Length > 0;
        // Mirror ThumbnailPlanBuilder: an explicit Thumbnails config wins, but
        // with none set it still builds a sprite whenever GenerateSpriteVtt is
        // on — which it is by default. Reading only profile.Thumbnails would
        // treat every preset that relies on that default as wanting no
        // thumbnails, so a missing sprite would never be topped up.
        bool desiredThumbnails =
            desiredVideo
            && (
                profile.Thumbnails is not null
                || (profile.HlsDerivatives ?? new HlsDerivatives()).GenerateSpriteVtt
            );
        bool desiredChapters = profile.HlsDerivatives?.GenerateChapters ?? true;
        bool desiredMasterPlaylist = profile.HlsDerivatives?.GenerateMasterPlaylist ?? true;

        // A missing or empty master playlist means the previous finalize
        // pass never completed — nothing downstream of it can be trusted
        // as a reliable "top up only the gap" case, so fall back to a full
        // re-encode rather than guessing which rungs are actually intact.
        if (desiredMasterPlaylist && !HasValidRootPlaylist(existing.BundleFiles))
            return new(
                ReconciliationAction.Full,
                [],
                needsOcr,
                "Master playlist missing or invalid — encode output is structurally incomplete."
            );

        List<EncodeTaskKind> missingKinds = [];
        if (desiredVideo && !HasValidUnder(existing.BundleFiles, "video_"))
            missingKinds.Add(EncodeTaskKind.Video);
        if (desiredAudio && !HasValidUnder(existing.BundleFiles, "audio_"))
            missingKinds.Add(EncodeTaskKind.Audio);
        if (desiredSubtitle && !HasValidDeclaredSubtitle(existing.BundleFiles))
            missingKinds.Add(EncodeTaskKind.Subtitle);
        // ThumbnailGenerator writes the sprite as `thumbs_{W}x{H}.webp` beside
        // its `thumbs_{W}x{H}.vtt`; the word "sprite" never appears on disk, so
        // matching it would report thumbnails missing on every re-dispatch.
        if (desiredThumbnails && !HasValidNameContaining(existing.BundleFiles, "thumbs_"))
            missingKinds.Add(EncodeTaskKind.Thumbnails);
        if (desiredChapters && !HasValidNamed(existing.BundleFiles, "chapters.vtt"))
            missingKinds.Add(EncodeTaskKind.Chapters);

        return Finalize(profile, existing.ProfileFingerprint, missingKinds, needsOcr);
    }

    public async Task<ExistingOutputSnapshot> InspectAsync(
        string mediaRootPath,
        IStorage destinationStorage,
        CancellationToken ct
    )
    {
        string trimmedRoot = mediaRootPath.TrimEnd('/');

        List<ExistingOutputEntry> files = [];
        try
        {
            string dirPrefix = trimmedRoot + "/";
            foreach (
                StorageEntry entry in destinationStorage.List(trimmedRoot, "*", recursive: true)
            )
            {
                if (entry.IsDirectory)
                    continue;

                string rel = entry.Path.StartsWith(dirPrefix, StringComparison.OrdinalIgnoreCase)
                    ? entry.Path[dirPrefix.Length..]
                    : entry.Path;
                files.Add(new(rel, entry.SizeBytes));
            }
        }
        catch (Exception)
        {
            // Nothing on disk yet for this media (first-ever encode) — an
            // empty snapshot is the correct read, not a failure. Different
            // storage backends signal "directory doesn't exist" differently
            // (DirectoryNotFoundException locally, a provider-specific
            // exception remotely), so this is intentionally broad.
        }

        int ocrCount = files.Count(f => f.IsValid && IsOcrSidecar(f.RelativePath));

        string? fingerprint = await TryReadManifestFingerprintAsync(
            trimmedRoot,
            files,
            destinationStorage,
            ct
        );

        return new(fingerprint, files, ocrCount);
    }

    /// <summary>
    /// Reads the fingerprint a finalized encode stamped into manifest.json.
    /// Anything encoded before that stamp existed has no manifest, so a
    /// missing or unreadable one is never an error — it is the expected case
    /// for every pre-existing library and falls through to the real on-disk
    /// listing instead.
    /// </summary>
    private static async Task<string?> TryReadManifestFingerprintAsync(
        string trimmedRoot,
        IReadOnlyCollection<ExistingOutputEntry> files,
        IStorage destinationStorage,
        CancellationToken ct
    )
    {
        ExistingOutputEntry? manifestEntry = files.FirstOrDefault(f =>
            string.Equals(
                Path.GetFileName(f.RelativePath),
                "manifest.json",
                StringComparison.OrdinalIgnoreCase
            )
        );

        if (manifestEntry is null || !manifestEntry.IsValid)
            return null;

        try
        {
            byte[] bytes = await destinationStorage.ReadAsync(
                $"{trimmedRoot}/{manifestEntry.RelativePath}",
                ct
            );
            BundleManifest? manifest = JsonConvert.DeserializeObject<BundleManifest>(
                Encoding.UTF8.GetString(bytes)
            );
            return manifest?.ProfileFingerprint;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static ReconciliationDecision DecideSingleFile(
        EncodingProfile profile,
        ExistingOutputSnapshot existing,
        bool needsOcr
    )
    {
        bool desiredAnyStream = profile.Video is not null || profile.Audio.Length > 0;
        bool hasValidOutput = existing.BundleFiles.Any(f =>
            f.IsValid && !IsOcrSidecar(f.RelativePath)
        );

        if (desiredAnyStream && !hasValidOutput)
            return new(
                ReconciliationAction.Full,
                [],
                needsOcr,
                "Single-file output missing or invalid — no partial concept for this container."
            );

        return Finalize(profile, existing.ProfileFingerprint, [], needsOcr);
    }

    /// <summary>
    /// Shared tail once every per-kind presence check has run: compares the
    /// stored fingerprint (if any) against the profile's current one and
    /// picks Full / Partial / Skip. A missing stored fingerprint (every
    /// output produced before fingerprinting shipped) is deliberately NOT
    /// treated as "profile changed" — that would force a full re-encode of
    /// an operator's entire library the moment the server upgrades.
    /// </summary>
    private static ReconciliationDecision Finalize(
        EncodingProfile profile,
        string? storedFingerprint,
        List<EncodeTaskKind> missingKinds,
        bool needsOcr
    )
    {
        bool fingerprintKnown = !string.IsNullOrEmpty(storedFingerprint);
        if (fingerprintKnown)
        {
            string currentFingerprint = ProfileFingerprint.Compute(profile);
            if (!string.Equals(storedFingerprint, currentFingerprint, StringComparison.Ordinal))
                return new(
                    ReconciliationAction.Full,
                    [],
                    needsOcr,
                    "Encoding profile changed since this output was produced."
                );
        }

        if (missingKinds.Count == 0 && !needsOcr)
            return new(
                ReconciliationAction.Skip,
                [],
                false,
                fingerprintKnown
                    ? "All desired outputs present, valid, and match the current profile fingerprint."
                    : "All desired outputs present and valid; no fingerprint on record (legacy output) treated as same profile."
            );

        List<string> gaps = missingKinds.Select(kind => kind.ToString()).ToList();
        if (needsOcr)
            gaps.Add("subtitle OCR sidecar");

        return new(
            ReconciliationAction.Partial,
            missingKinds,
            needsOcr,
            $"Profile unchanged — re-running only: {string.Join(", ", gaps)}."
        );
    }

    private static bool IsOcrSidecar(string relativePath) =>
        Path.GetFileName(relativePath).Contains(OcrMarker, StringComparison.OrdinalIgnoreCase);

    private static bool HasValidUnder(
        IReadOnlyCollection<ExistingOutputEntry> files,
        string prefix
    ) =>
        files.Any(f =>
            f.IsValid && f.RelativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
        );

    /// <summary>Declared (profile-driven) subtitle extraction, as opposed to
    /// a bitmap-source OCR sidecar — both share the <c>subtitles/</c>
    /// folder, so the OCR marker is what separates them.</summary>
    private static bool HasValidDeclaredSubtitle(IReadOnlyCollection<ExistingOutputEntry> files) =>
        files.Any(f =>
            f.IsValid
            && f.RelativePath.StartsWith("subtitles/", StringComparison.OrdinalIgnoreCase)
            && !IsOcrSidecar(f.RelativePath)
        );

    private static bool HasValidNamed(
        IReadOnlyCollection<ExistingOutputEntry> files,
        string exactFileName
    ) =>
        files.Any(f =>
            f.IsValid
            && string.Equals(
                Path.GetFileName(f.RelativePath),
                exactFileName,
                StringComparison.OrdinalIgnoreCase
            )
        );

    private static bool HasValidNameContaining(
        IReadOnlyCollection<ExistingOutputEntry> files,
        string fragment
    ) =>
        files.Any(f =>
            f.IsValid
            && Path.GetFileName(f.RelativePath)
                .Contains(fragment, StringComparison.OrdinalIgnoreCase)
        );

    /// <summary>The master playlist sits directly at the media root, never
    /// inside a per-rung/kind subfolder.</summary>
    private static bool HasValidRootPlaylist(IReadOnlyCollection<ExistingOutputEntry> files) =>
        files.Any(f =>
            f.IsValid
            && !f.RelativePath.Contains('/')
            && f.RelativePath.EndsWith(".m3u8", StringComparison.OrdinalIgnoreCase)
        );
}
