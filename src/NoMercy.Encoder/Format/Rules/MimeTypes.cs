// ReSharper disable MemberCanBePrivate.Global

namespace NoMercy.Encoder.Format.Rules;

public static class MimeTypes
{
    public static string GetMimeTypeFromFile(string file)
    {
        string ext = Path.GetExtension(file);

        return GetMimeType(ext);
    }

    public static string GetMimeType(string extension)
    {
        return extension.ToLower() switch
        {
            ".jpg" or ".jpeg" or ".jfif" or ".pjpg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".svg" => "image/svg+xml",
            ".bmp" => "image/bmp",
            ".tiff" or ".tif" => "image/tiff",
            ".ico" => "image/x-icon",

            ".pdf" => "application/pdf",
            ".doc" or ".docx" => "application/msword",
            ".xls" or ".xlsx" => "application/vnd.ms-excel",
            ".ppt" or ".pptx" => "application/vnd.ms-powerpoint",
            ".csv" => "text/csv",
            ".txt" => "text/plain",
            ".html" or ".htm" => "text/html",
            ".xml" => "application/xml",
            ".json" => "application/json",
            ".yaml" or ".yml" => "application/x-yaml",
            ".zip" => "application/zip",
            ".rar" => "application/vnd.rar",
            ".tar" => "application/x-tar",
            ".gz" => "application/gzip",

            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            ".midi" or ".mid" => "audio/midi",
            ".flac" => "audio/flac",
            
            ".m3u8" => "application/vnd.apple.mpegurl",
            ".m3u" => "application/vnd.apple.mpegurl",
            
            ".mp4" => "video/mp4",
            ".avi" => "video/x-msvideo",
            ".mov" => "video/quicktime",
            ".wmv" => "video/x-ms-wmv",
            ".webm" => "video/webm",
            ".mkv" => "video/x-matroska",
            ".mpeg" => "video/mpeg",
            ".flv" => "video/x-flv",
            ".3gp" => "video/3gpp",
            ".3g2" => "video/3gpp2",

            ".css" => "text/css",
            ".js" => "application/javascript",

            ".vtt" => "text/vtt",
            ".srt" => "text/srt",
            ".ass" => "text/x-ass",
            ".ttf" => "application/x-font-truetype",
            ".otf" => "application/x-font-opentype",
            ".woff" => "application/font-woff",
            ".woff2" => "application/font-woff2",
            ".eot" => "application/vnd.ms-fontobject",
            ".apk" => "application/vnd.android.package-archive",

            _ => "application/octet-stream"
        };
    }
}