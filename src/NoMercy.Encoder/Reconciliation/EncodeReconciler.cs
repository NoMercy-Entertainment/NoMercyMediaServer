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
using NoMercy.Encoder.Subtitles;
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
    /// <summary>Text sidecar formats the OCR pass can emit.</summary>
    private static readonly string[] TextSidecarExtensions = [".vtt", ".srt"];

    public ReconciliationDecision Decide(ReconciliationInput input)
    {
        if (input.Force)
            return new(
                Action: ReconciliationAction.Full,
                MissingKinds: [],
                NeedsSubtitleOcr: input.BitmapSubtitleStreamCount > 0,
                Reason: "Force flag set on the job — reconciliation skipped, full re-encode."
            );

        EncodingProfile profile = input.Profile;
        ExistingOutputSnapshot existing = input.Existing;

        bool desiredOcr = input.BitmapSubtitleStreamCount > 0;
        bool hasValidOcr = existing.ValidOcrSidecarCount >= input.BitmapSubtitleStreamCount;
        bool needsOcr = desiredOcr && !hasValidOcr;

        if (input.IsSingleFileOutput)
            return DecideSingleFile(profile: profile, existing: existing, needsOcr: needsOcr);

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
        // Only expect chapters.vtt from a source that has chapters to write —
        // FinalizeStage applies the same condition. Asking for it from a source
        // with none can never be satisfied, so the media reads as incomplete on
        // every pass and re-encodes in full, forever.
        bool desiredChapters =
            (profile.HlsDerivatives?.GenerateChapters ?? true) && input.SourceChapterCount > 0;
        bool desiredMasterPlaylist = profile.HlsDerivatives?.GenerateMasterPlaylist ?? true;

        // A missing or empty master playlist means the previous finalize
        // pass never completed — nothing downstream of it can be trusted
        // as a reliable "top up only the gap" case, so fall back to a full
        // re-encode rather than guessing which rungs are actually intact.
        if (desiredMasterPlaylist && !HasValidRootPlaylist(files: existing.BundleFiles))
            return new(
                Action: ReconciliationAction.Full,
                MissingKinds: [],
                NeedsSubtitleOcr: needsOcr,
                Reason: "Master playlist missing or invalid — encode output is structurally incomplete."
            );

        List<EncodeTaskKind> missingKinds = [];
        if (desiredVideo && !HasValidUnder(files: existing.BundleFiles, prefix: "video_"))
            missingKinds.Add(item: EncodeTaskKind.Video);
        if (desiredAudio && !HasValidUnder(files: existing.BundleFiles, prefix: "audio_"))
            missingKinds.Add(item: EncodeTaskKind.Audio);
        if (desiredSubtitle && !HasValidDeclaredSubtitle(files: existing.BundleFiles))
            missingKinds.Add(item: EncodeTaskKind.Subtitle);
        // ThumbnailGenerator writes the sprite as `thumbs_{W}x{H}.webp` beside
        // its `thumbs_{W}x{H}.vtt`; the word "sprite" never appears on disk, so
        // matching it would report thumbnails missing on every re-dispatch.
        if (desiredThumbnails && !HasValidNameContaining(files: existing.BundleFiles, fragment: "thumbs_"))
            missingKinds.Add(item: EncodeTaskKind.Thumbnails);
        if (desiredChapters && !HasValidNamed(files: existing.BundleFiles, exactFileName: "chapters.vtt"))
            missingKinds.Add(item: EncodeTaskKind.Chapters);

        return Finalize(profile: profile, storedFingerprint: existing.ProfileFingerprint, missingKinds: missingKinds, needsOcr: needsOcr);
    }

    public async Task<ExistingOutputSnapshot> InspectAsync(
        string mediaRootPath,
        string presetId,
        IStorage destinationStorage,
        CancellationToken ct
    )
    {
        string trimmedRoot = mediaRootPath.TrimEnd(trimChar: '/');

        List<ExistingOutputEntry> files = [];
        try
        {
            string dirPrefix = trimmedRoot + "/";
            foreach (
                StorageEntry entry in destinationStorage.List(path: trimmedRoot, pattern: "*", recursive: true)
            )
            {
                if (entry.IsDirectory)
                    continue;

                string rel = entry.Path.StartsWith(value: dirPrefix, comparisonType: StringComparison.OrdinalIgnoreCase)
                    ? entry.Path[dirPrefix.Length..]
                    : entry.Path;
                files.Add(item: new(RelativePath: rel, SizeBytes: entry.SizeBytes));
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

        int ocrCount = CountOcredBitmapSidecars(files: files);

        string? fingerprint = await TryReadBlueprintFingerprintAsync(
            trimmedRoot: trimmedRoot,
            presetId: presetId,
            files: files,
            destinationStorage: destinationStorage,
            ct: ct
        );

        return new(ProfileFingerprint: fingerprint, BundleFiles: files, ValidOcrSidecarCount: ocrCount);
    }

    /// <summary>
    /// Reads the fingerprint a finalized encode stamped into this preset's
    /// entry in the media item's <c>.nomercy.json</c> blueprint. Anything
    /// encoded before that stamp existed (or before the blueprint shipped)
    /// carries none, so a missing file, a missing preset entry, or an
    /// unreadable one is never an error — it is the expected case for every
    /// pre-existing library and falls through to the real on-disk listing
    /// instead.
    /// </summary>
    private static async Task<string?> TryReadBlueprintFingerprintAsync(
        string trimmedRoot,
        string presetId,
        IReadOnlyCollection<ExistingOutputEntry> files,
        IStorage destinationStorage,
        CancellationToken ct
    )
    {
        ExistingOutputEntry? blueprintEntry = files.FirstOrDefault(predicate: f =>
            string.Equals(
                a: Path.GetFileName(path: f.RelativePath),
                b: MediaBlueprintWriter.FileName,
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        );

        if (blueprintEntry is null || !blueprintEntry.IsValid)
            return null;

        try
        {
            byte[] bytes = await destinationStorage.ReadAsync(
                path: $"{trimmedRoot}/{blueprintEntry.RelativePath}",
                ct: ct
            );
            MediaBlueprint? blueprint = JsonConvert.DeserializeObject<MediaBlueprint>(
                value: Encoding.UTF8.GetString(bytes: bytes)
            );
            return blueprint
                ?.Encodes.FirstOrDefault(predicate: e =>
                    string.Equals(a: e.PresetId, b: presetId, comparisonType: StringComparison.OrdinalIgnoreCase)
                )
                ?.ProfileFingerprint;
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
        // The container itself, not a sidecar beside it: subtitles/ can be
        // populated (by extraction or by OCR) while the mp4/mkv this decision is
        // about is missing entirely.
        bool desiredAnyStream = profile.Video is not null || profile.Audio.Length > 0;
        bool hasValidOutput = existing.BundleFiles.Any(predicate: f =>
            f.IsValid
            && !f.RelativePath.StartsWith(value: "subtitles/", comparisonType: StringComparison.OrdinalIgnoreCase)
        );

        if (desiredAnyStream && !hasValidOutput)
            return new(
                Action: ReconciliationAction.Full,
                MissingKinds: [],
                NeedsSubtitleOcr: needsOcr,
                Reason: "Single-file output missing or invalid — no partial concept for this container."
            );

        return Finalize(profile: profile, storedFingerprint: existing.ProfileFingerprint, missingKinds: [], needsOcr: needsOcr);
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
        bool fingerprintKnown = !string.IsNullOrEmpty(value: storedFingerprint);
        if (fingerprintKnown)
        {
            string currentFingerprint = ProfileFingerprint.Compute(profile: profile);
            if (!string.Equals(a: storedFingerprint, b: currentFingerprint, comparisonType: StringComparison.Ordinal))
                return new(
                    Action: ReconciliationAction.Full,
                    MissingKinds: [],
                    NeedsSubtitleOcr: needsOcr,
                    Reason: "Encoding profile changed since this output was produced."
                );
        }

        if (missingKinds.Count == 0 && !needsOcr)
            return new(
                Action: ReconciliationAction.Skip,
                MissingKinds: [],
                NeedsSubtitleOcr: false,
                Reason: fingerprintKnown
                    ? "All desired outputs present, valid, and match the current profile fingerprint."
                    : "All desired outputs present and valid; no fingerprint on record (legacy output) treated as same profile."
            );

        List<string> gaps = missingKinds.Select(selector: kind => kind.ToString()).ToList();
        if (needsOcr)
            gaps.Add(item: "subtitle OCR sidecar");

        return new(
            Action: ReconciliationAction.Partial,
            MissingKinds: missingKinds,
            NeedsSubtitleOcr: needsOcr,
            Reason: $"Profile unchanged — re-running only: {string.Join(separator: ", ", values: gaps)}."
        );
    }

    /// <summary>
    /// Counts bitmap subtitle sidecars that already have their OCR result: a
    /// text sidecar carrying the same <c>{lang}.{type}</c>. An OCR sidecar is
    /// named as its bitmap track's sibling (see <c>OcrSidecarTarget</c>) — it
    /// carries no marker distinguishing it from a declared text subtitle, and
    /// must not, because that name is exactly what makes a player list it.
    /// Pairing is therefore the only honest way to ask "has OCR run for this
    /// track", and it is the same question the library scan's orphan check asks.
    /// </summary>
    internal static int CountOcredBitmapSidecars(IReadOnlyCollection<ExistingOutputEntry> files)
    {
        HashSet<string> textKeys = new(comparer: StringComparer.OrdinalIgnoreCase);
        foreach (ExistingOutputEntry file in files)
        {
            if (
                file.IsValid
                && TryReadSubtitleKey(relativePath: file.RelativePath, extensions: TextSidecarExtensions, key: out string textKey)
            )
                textKeys.Add(item: textKey);
        }

        // A bitmap track is only counted as OCR'd when a text sidecar carries the
        // same {lang}.{type} — the same pairing the library scan uses to decide
        // whether a bitmap subtitle is orphaned, and it reads bitmap-ness from
        // SubtitleClassifier so both agree on what a bitmap sidecar is.
        return files.Count(predicate: f =>
            f.IsValid
            && TryReadBitmapSubtitleKey(relativePath: f.RelativePath, key: out string bitmapKey)
            && textKeys.Contains(item: bitmapKey)
        );
    }

    private static bool TryReadBitmapSubtitleKey(string relativePath, out string key)
    {
        key = string.Empty;
        string extension = Path.GetExtension(path: Path.GetFileName(path: relativePath));

        return SubtitleClassifier.IsBitmapSidecarExtension(extension: extension)
            && TryReadSubtitleKey(relativePath: relativePath, extensions: [extension], key: out key);
    }

    /// <summary>
    /// Reads the <c>{lang}.{type}</c> key out of a <c>{name}.{lang}.{type}.{ext}</c>
    /// sidecar filename, when its extension is one of <paramref name="extensions"/>.
    /// Mirrors <c>FileManager.SubtitleFileRegex</c>.
    /// </summary>
    private static bool TryReadSubtitleKey(string relativePath, string[] extensions, out string key)
    {
        key = string.Empty;
        string fileName = Path.GetFileName(path: relativePath);
        string extension = Path.GetExtension(path: fileName);

        if (!extensions.Contains(value: extension, comparer: StringComparer.OrdinalIgnoreCase))
            return false;

        string[] parts = Path.GetFileNameWithoutExtension(path: fileName).Split(separator: '.');
        if (parts.Length < 2)
            return false;

        key = $"{parts[^2]}|{parts[^1]}";
        return true;
    }

    private static bool HasValidUnder(
        IReadOnlyCollection<ExistingOutputEntry> files,
        string prefix
    ) =>
        files.Any(predicate: f =>
            f.IsValid && f.RelativePath.StartsWith(value: prefix, comparisonType: StringComparison.OrdinalIgnoreCase)
        );

    /// <summary>Whether the subtitle pass produced anything at all. An OCR
    /// sidecar is indistinguishable from a declared one by name (deliberately —
    /// see <see cref="CountOcredBitmapSidecars"/>), and both mean the same thing
    /// here: the subtitle stage ran and left output.</summary>
    private static bool HasValidDeclaredSubtitle(IReadOnlyCollection<ExistingOutputEntry> files) =>
        files.Any(predicate: f =>
            f.IsValid && f.RelativePath.StartsWith(value: "subtitles/", comparisonType: StringComparison.OrdinalIgnoreCase)
        );

    private static bool HasValidNamed(
        IReadOnlyCollection<ExistingOutputEntry> files,
        string exactFileName
    ) =>
        files.Any(predicate: f =>
            f.IsValid
            && string.Equals(
                a: Path.GetFileName(path: f.RelativePath),
                b: exactFileName,
                comparisonType: StringComparison.OrdinalIgnoreCase
            )
        );

    private static bool HasValidNameContaining(
        IReadOnlyCollection<ExistingOutputEntry> files,
        string fragment
    ) =>
        files.Any(predicate: f =>
            f.IsValid
            && Path.GetFileName(path: f.RelativePath)
                .Contains(value: fragment, comparisonType: StringComparison.OrdinalIgnoreCase)
        );

    /// <summary>The master playlist sits directly at the media root, never
    /// inside a per-rung/kind subfolder.</summary>
    private static bool HasValidRootPlaylist(IReadOnlyCollection<ExistingOutputEntry> files) =>
        files.Any(predicate: f =>
            f.IsValid
            && !f.RelativePath.Contains(value: '/')
            && f.RelativePath.EndsWith(value: ".m3u8", comparisonType: StringComparison.OrdinalIgnoreCase)
        );
}
