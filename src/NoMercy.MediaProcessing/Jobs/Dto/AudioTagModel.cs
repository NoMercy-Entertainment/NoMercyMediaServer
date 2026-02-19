using NoMercy.Encoder.Dto;
using NoMercy.NmSystem.Dto;
using TagLib;
using AudioStream = NoMercy.Encoder.Dto.AudioStream;
using Ffprobe = NoMercy.Encoder.Ffprobe;

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
        public string FingerPrint  { get; set; } = string.Empty;
        public Guid AcoustIdId { get; set; }
    }

    public MusicBrainzDto? MusicBrainz { get; set; }
    public FfprobeSourceDataFormat Format { get; set; } = new();
    public AudioStream? Stream { get; set; }
    public Tag? Tags { get; set; }
        
    public double Duration { get; set; }

    public MediaFile FileItem { get; set; } = null!;
    
    public static async Task<AudioTagModel> Create(MediaFile fileItem)
    {
        Ffprobe ffProbe = new(fileItem.Path);
        Ffprobe ffProbeData = await ffProbe.GetStreamData();
        Dictionary<string,string> tagsContainer = ffProbeData.Format.Tags;
        MusicBrainzDto? mb = null;
        
        if (fileItem.TagFile?.Tag is not null)
        {
            mb ??= new();

            if (Guid.TryParse(fileItem.TagFile.Tag.MusicBrainzReleaseId, out Guid rId))
                mb.ReleaseId = rId;
            
            if (Guid.TryParse(fileItem.TagFile.Tag.MusicBrainzArtistId, out Guid aId))
                mb.ArtistId = aId;
            
            if (Guid.TryParse(fileItem.TagFile.Tag.MusicBrainzReleaseArtistId, out Guid raId))
                mb.ReleaseArtistId = raId;
            
            if (Guid.TryParse(fileItem.TagFile.Tag.MusicBrainzTrackId, out Guid tId))
                mb.ReleaseTrackId = tId;
            
            if (Guid.TryParse(fileItem.TagFile.Tag.MusicBrainzTrackId, out Guid recId))
                mb.RecordingId = recId;
            
            if (tagsContainer.TryGetValue("Acoustid Fingerprint", out string? fingerPrint))
                mb.FingerPrint = fingerPrint;
            
            if (tagsContainer.TryGetValue("Acoustid Id", out string? acoustId))
                mb.AcoustIdId = Guid.Parse(acoustId);
            
            if (mb.ReleaseId == Guid.Empty && tagsContainer.TryGetValue("MusicBrainz Release Id", out string? releaseId))
                mb.ReleaseId = Guid.Parse(releaseId);
            
            if (mb.ArtistId == Guid.Empty && tagsContainer.TryGetValue("MusicBrainz Artist Id", out string? albumId))
                mb.ArtistId = Guid.TryParse(albumId.Split(";").First().Trim(), out Guid albumGuid) ? albumGuid : Guid.Empty;
            
            if (mb.ReleaseArtistId == Guid.Empty && tagsContainer.TryGetValue("MusicBrainz Release Artist Id", out string? albumTrackId))
                mb.ReleaseArtistId = Guid.Parse(albumTrackId);
            
            if (mb.ReleaseTrackId == Guid.Empty && tagsContainer.TryGetValue("MusicBrainz Track Id", out string? trackId))
                mb.ReleaseTrackId = Guid.Parse(trackId);
            
            if (mb.ReleaseTrackId == Guid.Empty && tagsContainer.TryGetValue("MusicBrainz Recording Id", out string? recordingId))
                mb.RecordingId = Guid.Parse(recordingId);
            
            if (mb.ReleaseTrackId == Guid.Empty && tagsContainer.TryGetValue("MusicBrainz Track Id", out string? trackId2))
                mb.RecordingId = Guid.Parse(trackId2);
        }
        else
        {
            mb ??= new();
            if (tagsContainer.TryGetValue("Acoustid Fingerprint", out string? fingerPrint))
                mb.FingerPrint = fingerPrint;
            
            if (tagsContainer.TryGetValue("Acoustid Id", out string? acoustId))
                mb.AcoustIdId = Guid.Parse(acoustId);
            
            if (tagsContainer.TryGetValue("MusicBrainz Release Id", out string? releaseId))
                mb.ReleaseId = Guid.Parse(releaseId);
            
            if (tagsContainer.TryGetValue("MusicBrainz Artist Id", out string? albumId))
                mb.ArtistId = Guid.Parse(albumId);
            
            if (tagsContainer.TryGetValue("MusicBrainz Release Artist Id", out string? albumTrackId))
                mb.ReleaseArtistId = Guid.Parse(albumTrackId);
            
            if (tagsContainer.TryGetValue("MusicBrainz Track Id", out string? trackId))
                mb.ReleaseTrackId = Guid.Parse(trackId);
            
            if (tagsContainer.TryGetValue("MusicBrainz Recording Id", out string? recordingId))
                mb.RecordingId = Guid.Parse(recordingId);
            
            if (tagsContainer.TryGetValue("MusicBrainz Track Id", out string? trackId2))
                mb.RecordingId = Guid.Parse(trackId2);
            
        }
        
        foreach (KeyValuePair<string, string> tag in tagsContainer)
        {
            string key = tag.Key.ToLowerInvariant().Replace("musicbrainz_","").Replace("musicbrainz","").Replace(" ","").Replace("_","");
            string value = tag.Value;
            switch (key)
            {
                case "albumid":
                case "releaseid":
                    if (Guid.TryParse(value, out Guid releaseId) && mb.ReleaseId != releaseId)
                    {
                        mb.ReleaseId = releaseId;
                    }
                    continue;
                case "artistid":
                    if (Guid.TryParse(value, out Guid artistId) && mb.ArtistId != artistId)
                    {
                        mb.ArtistId = artistId;
                    }
                    continue;
                case "albumartistid":
                case "releaseartistid":
                    if (Guid.TryParse(value, out Guid releaseArtistId) && mb.ReleaseArtistId != releaseArtistId)
                    {
                        mb.ReleaseArtistId = releaseArtistId;
                    }
                    continue;
                case "trackid":
                case "releasetrackid":
                    if (Guid.TryParse(value, out Guid releaseTrackId) && mb.ReleaseTrackId != releaseTrackId)
                    {
                        mb.ReleaseTrackId = releaseTrackId;
                    }
                    continue;
                case "recordingid":
                    if (Guid.TryParse(value, out Guid recordingId) && mb.RecordingId != recordingId)
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
                    if (Guid.TryParse(value, out Guid acoustIdId) && mb.AcoustIdId != acoustIdId)
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
            Duration = ffProbeData.Format.Duration?.TotalSeconds ?? 0
        };
        
        return metaData;
    }
}