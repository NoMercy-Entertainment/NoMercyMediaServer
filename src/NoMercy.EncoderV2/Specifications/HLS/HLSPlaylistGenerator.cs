using System.Text;

namespace NoMercy.EncoderV2.Specifications.HLS;

/// <summary>
/// Generates HLS master and media playlists
/// </summary>
public interface IHLSPlaylistGenerator
{
    Task<string> GenerateMasterPlaylistAsync(List<HLSVariantStream> variants, List<HLSMediaGroup>? mediaGroups = null);
    Task<string> GenerateMediaPlaylistAsync(HLSSpecification spec, List<string> segmentFiles, TimeSpan totalDuration);
    Task WriteMasterPlaylistAsync(string outputPath, List<HLSVariantStream> variants, List<HLSMediaGroup>? mediaGroups = null);
    Task WriteMediaPlaylistAsync(string outputPath, HLSSpecification spec, List<string> segmentFiles, TimeSpan totalDuration);
}

public class HLSPlaylistGenerator : IHLSPlaylistGenerator
{
    public async Task<string> GenerateMasterPlaylistAsync(List<HLSVariantStream> variants, List<HLSMediaGroup>? mediaGroups = null)
    {
        StringBuilder sb = new();

        sb.AppendLine("#EXTM3U");
        sb.AppendLine("#EXT-X-VERSION:3");

        if (mediaGroups != null && mediaGroups.Count > 0)
        {
            foreach (HLSMediaGroup group in mediaGroups)
            {
                sb.Append($"#EXT-X-MEDIA:TYPE={group.Type}");
                sb.Append($",GROUP-ID=\"{group.GroupId}\"");
                sb.Append($",NAME=\"{group.Name}\"");
                sb.Append($",LANGUAGE=\"{group.Language}\"");
                sb.Append($",DEFAULT={(group.IsDefault ? "YES" : "NO")}");
                sb.Append($",AUTOSELECT={(group.Autoselect ? "YES" : "NO")}");
                if (!string.IsNullOrEmpty(group.Uri))
                {
                    sb.Append($",URI=\"{group.Uri}\"");
                }
                sb.AppendLine();
            }
        }

        foreach (HLSVariantStream variant in variants.OrderByDescending(v => v.Bandwidth))
        {
            sb.Append("#EXT-X-STREAM-INF:");
            sb.Append($"BANDWIDTH={variant.Bandwidth}");

            if (variant.AverageBandwidth > 0)
            {
                sb.Append($",AVERAGE-BANDWIDTH={variant.AverageBandwidth}");
            }

            if (!string.IsNullOrEmpty(variant.Resolution))
            {
                sb.Append($",RESOLUTION={variant.Resolution}");
            }

            if (variant.Framerate > 0)
            {
                sb.Append($",FRAME-RATE={variant.Framerate:F3}");
            }

            if (!string.IsNullOrEmpty(variant.Codecs))
            {
                sb.Append($",CODECS=\"{variant.Codecs}\"");
            }

            if (!string.IsNullOrEmpty(variant.AudioGroup))
            {
                sb.Append($",AUDIO=\"{variant.AudioGroup}\"");
            }

            if (!string.IsNullOrEmpty(variant.SubtitleGroup))
            {
                sb.Append($",SUBTITLES=\"{variant.SubtitleGroup}\"");
            }

            sb.AppendLine();
            sb.AppendLine(variant.PlaylistUri);
        }

        return await Task.FromResult(sb.ToString());
    }

    public async Task<string> GenerateMediaPlaylistAsync(HLSSpecification spec, List<string> segmentFiles, TimeSpan totalDuration)
    {
        StringBuilder sb = new();

        sb.AppendLine("#EXTM3U");
        sb.AppendLine($"#EXT-X-VERSION:{spec.Version}");
        sb.AppendLine($"#EXT-X-TARGETDURATION:{spec.TargetDuration}");
        sb.AppendLine($"#EXT-X-MEDIA-SEQUENCE:{spec.MediaSequence}");

        if (spec.PlaylistType == "VOD")
        {
            sb.AppendLine("#EXT-X-PLAYLIST-TYPE:VOD");
        }

        if (spec.IndependentSegments)
        {
            sb.AppendLine("#EXT-X-INDEPENDENT-SEGMENTS");
        }

        double segmentDuration = spec.SegmentDuration;
        foreach (string segmentFile in segmentFiles)
        {
            sb.AppendLine($"#EXTINF:{segmentDuration:F6},");
            sb.AppendLine(Path.GetFileName(segmentFile));
        }

        if (spec.PlaylistType == "VOD")
        {
            sb.AppendLine("#EXT-X-ENDLIST");
        }

        return await Task.FromResult(sb.ToString());
    }

    public async Task WriteMasterPlaylistAsync(string outputPath, List<HLSVariantStream> variants, List<HLSMediaGroup>? mediaGroups = null)
    {
        string content = await GenerateMasterPlaylistAsync(variants, mediaGroups);
        await File.WriteAllTextAsync(outputPath, content);
    }

    public async Task WriteMediaPlaylistAsync(string outputPath, HLSSpecification spec, List<string> segmentFiles, TimeSpan totalDuration)
    {
        string content = await GenerateMediaPlaylistAsync(spec, segmentFiles, totalDuration);
        await File.WriteAllTextAsync(outputPath, content);
    }
}

