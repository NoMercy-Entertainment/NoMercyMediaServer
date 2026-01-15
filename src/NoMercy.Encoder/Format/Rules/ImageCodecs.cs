namespace NoMercy.Encoder.Format.Rules;

public static class ImageCodecs
{
    public static readonly Classes.CodecDto Jpeg = new()
    {
        Name = "Joint Photographic Experts Group",
        Value = "mjpeg",
        SimpleValue = "jpeg",
        IsDefault = true
    };

    public static readonly Classes.CodecDto Png = new()
    {
        Name = "Portable Network Graphics",
        Value = "png",
        SimpleValue = "png",
        IsDefault = true
    };

    public static readonly Classes.CodecDto Gif = new()
    {
        Name = "Graphics Interchange Format",
        Value = "gif",
        SimpleValue = "gif",
        IsDefault = true
    };

    public static readonly Classes.CodecDto Bmp = new()
    {
        Name = "Bitmap",
        Value = "bmp",
        SimpleValue = "bmp",
        IsDefault = false
    };

    public static readonly Classes.CodecDto Tiff = new()
    {
        Name = "Tagged Image File Format",
        Value = "tiff",
        SimpleValue = "tiff",
        IsDefault = false
    };

    public static readonly Classes.CodecDto Webp = new()
    {
        Name = "WebP",
        Value = "webp",
        SimpleValue = "webp",
        IsDefault = true
    };

    public static readonly Classes.CodecDto Heif = new()
    {
        Name = "High Efficiency Image Format",
        Value = "heif",
        SimpleValue = "heif",
        IsDefault = false
    };

    public static readonly Classes.CodecDto Heic = new()
    {
        Name = "High Efficiency Image Container",
        Value = "heic",
        SimpleValue = "heic",
        IsDefault = false
    };
}