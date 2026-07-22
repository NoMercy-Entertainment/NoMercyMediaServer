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

using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.FFProbe;
using TagLib;

namespace NoMercy.MediaProcessing.Jobs.Dto;

public class AudioTagModel
{
    public class MusicBrainzDto
    {
        public Guid ReleaseId { get; set; }
        public Guid ReleaseArtistId { get; set; }
        public Guid ArtistId { get; set; }
        public Guid ReleaseTrackId { get; set; }
        public Guid RecordingId { get; set; }
        public string FingerPrint { get; set; } = string.Empty;
        public Guid AcoustIdId { get; set; }
    }

    public MusicBrainzDto? MusicBrainz { get; set; }
    public FfProbeFormat Format { get; set; } = new();
    public FfProbeAudioStream? Stream { get; set; }
    public Tag? Tags { get; set; }

    public double Duration { get; set; }

    public MediaFile FileItem { get; set; } = null!;

    public static async Task<AudioTagModel> Create(MediaFile fileItem)
    {
        FfProbeData ffProbeData = await FfProbe.CreateAsync(file: fileItem.Path);
        Dictionary<string, string> tagsContainer = ffProbeData.Format.Tags ?? [];
        MusicBrainzDto? mb = null;

        if (fileItem.TagFile?.Tag is not null)
        {
            mb ??= new();

            if (Guid.TryParse(input: fileItem.TagFile.Tag.MusicBrainzReleaseId, result: out Guid rId))
                mb.ReleaseId = rId;

            if (Guid.TryParse(input: fileItem.TagFile.Tag.MusicBrainzArtistId, result: out Guid aId))
                mb.ArtistId = aId;

            if (Guid.TryParse(input: fileItem.TagFile.Tag.MusicBrainzReleaseArtistId, result: out Guid raId))
                mb.ReleaseArtistId = raId;

            if (Guid.TryParse(input: fileItem.TagFile.Tag.MusicBrainzTrackId, result: out Guid tId))
                mb.ReleaseTrackId = tId;

            if (Guid.TryParse(input: fileItem.TagFile.Tag.MusicBrainzTrackId, result: out Guid recId))
                mb.RecordingId = recId;

            if (tagsContainer.TryGetValue(key: "Acoustid Fingerprint", value: out string? fingerPrint))
                mb.FingerPrint = fingerPrint;

            if (
                tagsContainer.TryGetValue(key: "Acoustid Id", value: out string? acoustId)
                && Guid.TryParse(input: acoustId, result: out Guid acoustGuid)
            )
                mb.AcoustIdId = acoustGuid;

            if (
                mb.ReleaseId == Guid.Empty
                && tagsContainer.TryGetValue(key: "MusicBrainz Release Id", value: out string? releaseId)
                && Guid.TryParse(input: releaseId, result: out Guid releaseGuid)
            )
                mb.ReleaseId = releaseGuid;

            if (
                mb.ArtistId == Guid.Empty
                && tagsContainer.TryGetValue(key: "MusicBrainz Artist Id", value: out string? albumId)
            )
                mb.ArtistId = Guid.TryParse(input: albumId.Split(separator: ";").First().Trim(), result: out Guid albumGuid)
                    ? albumGuid
                    : Guid.Empty;

            if (
                mb.ReleaseArtistId == Guid.Empty
                && tagsContainer.TryGetValue(
                    key: "MusicBrainz Release Artist Id",
                    value: out string? albumTrackId
                )
                && Guid.TryParse(input: albumTrackId, result: out Guid albumTrackGuid)
            )
                mb.ReleaseArtistId = albumTrackGuid;

            if (
                mb.ReleaseTrackId == Guid.Empty
                && tagsContainer.TryGetValue(key: "MusicBrainz Track Id", value: out string? trackId)
                && Guid.TryParse(input: trackId, result: out Guid trackGuid)
            )
                mb.ReleaseTrackId = trackGuid;

            if (
                mb.ReleaseTrackId == Guid.Empty
                && tagsContainer.TryGetValue(key: "MusicBrainz Recording Id", value: out string? recordingId)
                && Guid.TryParse(input: recordingId, result: out Guid recordingGuid)
            )
                mb.RecordingId = recordingGuid;

            if (
                mb.ReleaseTrackId == Guid.Empty
                && tagsContainer.TryGetValue(key: "MusicBrainz Track Id", value: out string? trackId2)
                && Guid.TryParse(input: trackId2, result: out Guid trackGuid2)
            )
                mb.RecordingId = trackGuid2;
        }
        else
        {
            mb ??= new();
            if (tagsContainer.TryGetValue(key: "Acoustid Fingerprint", value: out string? fingerPrint))
                mb.FingerPrint = fingerPrint;

            if (
                tagsContainer.TryGetValue(key: "Acoustid Id", value: out string? acoustId)
                && Guid.TryParse(input: acoustId, result: out Guid acoustGuid)
            )
                mb.AcoustIdId = acoustGuid;

            if (
                tagsContainer.TryGetValue(key: "MusicBrainz Release Id", value: out string? releaseId)
                && Guid.TryParse(input: releaseId, result: out Guid releaseGuid)
            )
                mb.ReleaseId = releaseGuid;

            if (
                tagsContainer.TryGetValue(key: "MusicBrainz Artist Id", value: out string? albumId)
                && Guid.TryParse(input: albumId, result: out Guid artistGuid)
            )
                mb.ArtistId = artistGuid;

            if (
                tagsContainer.TryGetValue(key: "MusicBrainz Release Artist Id", value: out string? albumTrackId)
                && Guid.TryParse(input: albumTrackId, result: out Guid albumTrackGuid)
            )
                mb.ReleaseArtistId = albumTrackGuid;

            if (
                tagsContainer.TryGetValue(key: "MusicBrainz Track Id", value: out string? trackId)
                && Guid.TryParse(input: trackId, result: out Guid trackGuid)
            )
                mb.ReleaseTrackId = trackGuid;

            if (
                tagsContainer.TryGetValue(key: "MusicBrainz Recording Id", value: out string? recordingId)
                && Guid.TryParse(input: recordingId, result: out Guid recordingGuid)
            )
                mb.RecordingId = recordingGuid;

            if (
                tagsContainer.TryGetValue(key: "MusicBrainz Track Id", value: out string? trackId2)
                && Guid.TryParse(input: trackId2, result: out Guid trackGuid2)
            )
                mb.RecordingId = trackGuid2;
        }

        foreach (KeyValuePair<string, string> tag in tagsContainer)
        {
            string key = tag
                .Key.ToLowerInvariant()
                .Replace(oldValue: "musicbrainz_", newValue: "")
                .Replace(oldValue: "musicbrainz", newValue: "")
                .Replace(oldValue: " ", newValue: "")
                .Replace(oldValue: "_", newValue: "");
            string value = tag.Value;
            switch (key)
            {
                case "albumid":
                case "releaseid":
                    if (Guid.TryParse(input: value, result: out Guid releaseId) && mb.ReleaseId != releaseId)
                    {
                        mb.ReleaseId = releaseId;
                    }
                    continue;
                case "artistid":
                    if (Guid.TryParse(input: value, result: out Guid artistId) && mb.ArtistId != artistId)
                    {
                        mb.ArtistId = artistId;
                    }
                    continue;
                case "albumartistid":
                case "releaseartistid":
                    if (
                        Guid.TryParse(input: value, result: out Guid releaseArtistId)
                        && mb.ReleaseArtistId != releaseArtistId
                    )
                    {
                        mb.ReleaseArtistId = releaseArtistId;
                    }
                    continue;
                case "trackid":
                case "releasetrackid":
                    if (
                        Guid.TryParse(input: value, result: out Guid releaseTrackId)
                        && mb.ReleaseTrackId != releaseTrackId
                    )
                    {
                        mb.ReleaseTrackId = releaseTrackId;
                    }
                    continue;
                case "recordingid":
                    if (Guid.TryParse(input: value, result: out Guid recordingId) && mb.RecordingId != recordingId)
                    {
                        mb.RecordingId = recordingId;
                    }
                    continue;
                case "acoustidfingerprint":
                    if (mb.FingerPrint != value)
                    {
                        mb.FingerPrint = value;
                    }
                    continue;
                case "acoustidid":
                    if (Guid.TryParse(input: value, result: out Guid acoustIdId) && mb.AcoustIdId != acoustIdId)
                    {
                        mb.AcoustIdId = acoustIdId;
                    }
                    continue;
            }
        }

        AudioTagModel metaData = new()
        {
            Format = ffProbeData.Format,
            Stream = ffProbeData.AudioStreams.FirstOrDefault(),
            MusicBrainz = mb,
            Tags = fileItem.TagFile?.Tag,
            FileItem = fileItem,
            Duration = ffProbeData.Format.Duration.TotalSeconds,
        };

        return metaData;
    }
}
