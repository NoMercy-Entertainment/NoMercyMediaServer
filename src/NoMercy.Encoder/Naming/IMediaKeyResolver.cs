namespace NoMercy.Encoder.Naming;

public enum MediaType
{
    Movie,
    Episode,
    Track,
}

public interface IMediaKeyResolver
{
    string ForMedia(MediaType type, long id);
}
