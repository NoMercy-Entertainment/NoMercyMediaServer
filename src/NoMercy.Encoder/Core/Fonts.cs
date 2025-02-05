using Newtonsoft.Json;
using NoMercy.Encoder.Format.Rules;
using NoMercy.NmSystem;

namespace NoMercy.Encoder.Core;

public static class Fonts
{
    public static async Task Extract(string inputFilePath, string location)
    {
        string folder = Path.Combine(location, "fonts");
        string attachmentsFile = $"{location}/fonts.json";

        if (!Directory.Exists(folder))
        {
            Directory.CreateDirectory(folder);
        }

        string command = $"-dump_attachment:t \"\" -i \"{inputFilePath}\" -y  -hide_banner -max_muxing_queue_size 9999 -async 1 -loglevel panic 2>&1";
        await FfMpeg.Exec(command, folder);

        string[] files = Directory.GetFiles(folder);
        if (files.Length == 0) return;

        List<Attachment> attachments = [];
        foreach (string file in files)
        {
            attachments.Add(new()
            {
                Filename = Path.GetFileName(file),
                MimeType = MimeTypes.GetMimeTypeFromFile(file)
            });
        }

        await File.WriteAllTextAsync(attachmentsFile, attachments.ToJson());
    }

    private class Attachment
    {
        [JsonProperty("file")] public string Filename { get; set; } = string.Empty;
        [JsonProperty("mimeType")] public string MimeType { get; set; } = string.Empty;
    }
}