using Newtonsoft.Json;
using NoMercy.Encoder.Format.Rules;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;

namespace NoMercy.Encoder.Core;

public static class Fonts
{
    public static async Task Extract(string inputFilePath, string location)
    {
        string folder = Path.Combine(location, "fonts");
        string attachmentsFile = Path.Combine(location, "fonts.json");

        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);

        string command = $@"-dump_attachment:t """" -i ""{inputFilePath}"" -y -hide_banner -t 0 -f null null";
        await Shell.ExecAsync(AppFiles.FfmpegPath, command, new() { WorkingDirectory = folder});

        string[] files = Directory.GetFiles(folder);
        if (files.Length == 0) return;

        List<Attachment> attachments = [];
        foreach (string file in files)
            attachments.Add(new()
            {
                Filename = "fonts/" + Path.GetFileName(file),
                MimeType = MimeTypes.GetMimeTypeFromFile(file)
            });

        await File.WriteAllTextAsync(attachmentsFile, attachments.ToJson());
    }

    private class Attachment
    {
        [JsonProperty("file")] public string Filename { get; set; } = string.Empty;
        [JsonProperty("mimeType")] public string MimeType { get; set; } = string.Empty;
    }
}