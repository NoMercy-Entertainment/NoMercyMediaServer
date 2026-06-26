namespace NoMercy.Encoder.Naming;

public record MediaItemRef(MediaType Type, long Id, string Title, int? Year);

public interface IOutputNamingResolver
{
    BundleLayout Resolve(MediaItemRef media, Profiles.EncodingProfile profile);

    string VideoVariantPath(BundleLayout layout, string label, string filename);

    string VideoSegmentPath(BundleLayout layout, string label, int seq);

    string AudioPlaylistPath(BundleLayout layout, string language, string codec);

    string AudioInitPath(BundleLayout layout, string language, string codec);

    string AudioSegmentPath(BundleLayout layout, string language, string codec, int seq);

    string SubtitlePath(BundleLayout layout, string language, string extension);

    string DerivativePath(BundleLayout layout, string filename);
}
