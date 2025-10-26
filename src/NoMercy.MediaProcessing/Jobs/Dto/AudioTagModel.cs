using NoMercy.Encoder.Dto;
using NoMercy.NmSystem.Dto;
using TagLib;
using AudioStream = NoMercy.Encoder.Dto.AudioStream;
using Ffprobe = NoMercy.Encoder.Ffprobe;

namespace NoMercy.MediaProcessing.Jobs.Dto;

public class AudioTagModel
{
    public class MusicBrainz 
    {
        public Guid ReleaseId { get; set; }
        public Guid ReleaseArtistId { get; set; }
        public Guid ArtistId { get; set; }
        public Guid ReleaseTrackId { get; set; }
        public string FingerPrint  { get; set; } = string.Empty;
        public Guid AcoustIdId { get; set; }
    }

    public MusicBrainz? musicBrainz { get; set; }
    public FfprobeSourceDataFormat format { get; set; } = new();
    public AudioStream? stream { get; set; }
    public Tag? tags { get; set; }
    
    public MediaFile fileItem { get; set; }
    
    public static async Task<AudioTagModel> Create(MediaFile fileItem)
    {
        Ffprobe ffProbe = new(fileItem.Path);
        Ffprobe ffProbeData = await ffProbe.GetStreamData();
        Dictionary<string,string> tagsContainer = ffProbeData.Format.Tags;
        MusicBrainz? mb = null;

        if (fileItem.TagFile?.Tag is not null)
        {
            mb ??= new();

            if (Guid.TryParse(fileItem.TagFile.Tag.MusicBrainzReleaseId, out Guid rid))
                mb.ReleaseId = rid;
            
            if (Guid.TryParse(fileItem.TagFile.Tag.MusicBrainzArtistId, out Guid aid))
                mb.ArtistId = aid;
            
            if (Guid.TryParse(fileItem.TagFile.Tag.MusicBrainzReleaseArtistId, out Guid raid))
                mb.ReleaseArtistId = raid;
            
            if (Guid.TryParse(fileItem.TagFile.Tag.MusicBrainzTrackId, out Guid tid))
                mb.ReleaseTrackId = tid;
            
            if (tagsContainer.TryGetValue("Acoustid Fingerprint", out string? fingerPrint))
                mb.FingerPrint = fingerPrint;
            
            if (tagsContainer.TryGetValue("Acoustid Id", out string? acoustId))
                mb.AcoustIdId = Guid.Parse(acoustId);
            
            if (mb.ReleaseId == Guid.Empty && tagsContainer.TryGetValue("MusicBrainz Release Id", out string? releaseId))
                mb.ReleaseId = Guid.Parse(releaseId);
            
            if (mb.ArtistId == Guid.Empty && tagsContainer.TryGetValue("MusicBrainz Artist Id", out string? albumId))
                mb.ArtistId = Guid.Parse(albumId.Split(";").FirstOrDefault() ?? albumId);
            
            if (mb.ReleaseArtistId == Guid.Empty && tagsContainer.TryGetValue("MusicBrainz Release Artist Id", out string? albumTrackId))
                mb.ReleaseArtistId = Guid.Parse(albumTrackId);
            
            if (mb.ReleaseTrackId == Guid.Empty && tagsContainer.TryGetValue("MusicBrainz Track Id", out string? trackId))
                mb.ReleaseTrackId = Guid.Parse(trackId);
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
            
        }
        
        AudioTagModel metaData = new()
        {
            format = ffProbeData.Format,
            stream = ffProbeData.AudioStreams.FirstOrDefault(),
            musicBrainz = mb,
            tags = fileItem.TagFile?.Tag,
            fileItem = fileItem
        };
        
        return metaData;
    }
}