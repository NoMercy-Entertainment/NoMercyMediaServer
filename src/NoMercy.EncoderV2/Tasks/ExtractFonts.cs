using System.Text;
using NoMercy.Encoder.Format.Rules;
using NoMercy.EncoderV2.Core.Contracts;
using NoMercy.EncoderV2.Shared.Dtos;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.EncoderV2.Tasks;

public class ExtractFonts: ITaskContract
{
    private string InputFile { get; }
    private string Destination { get; }
    private string FontsDir { get; }
    private string FontsFile { get; }
    
    public ExtractFonts(string inputFile, string destination)
    {
        InputFile = inputFile;
        Destination = destination;
        FontsDir = Path.Combine(destination, "fonts");
        FontsFile = Path.Combine(Destination, "fonts.json");
    }
    
    private string GetCommand()
    {
        // return $@"-dump_attachment:t """" -i ""{inputFile}"" -y -hide_banner -t 0 -f null null";
        StringBuilder command = new();
        command.Append(@"-dump_attachment:t """" ");
        command.Append($"-i \"{InputFile}\" ");
        command.Append("-y ");
        command.Append("-hide_banner ");
        command.Append("-t 0 ");
        command.Append("-f null ");
        command.Append("- ");
            
        return command.ToString();
    }
    
    public async Task Run(CancellationTokenSource cts)
    {
        string command = GetCommand();

        Logger.Encoder(command, LogEventLevel.Debug);
        
        if (!Directory.Exists(FontsDir))
            Directory.CreateDirectory(FontsDir);

        ExecResult res = await Shell.ExecAsync(AppFiles.FfmpegPath, command, new() { WorkingDirectory = FontsDir }, cts);
        if (res.ExitCode != 0)
            throw new InvalidOperationException($"ffmpeg extraction failed: {res.StandardError}");

        await GenerateFile(cts);
    }

    public static async Task RunStatic(string inputFile, string destination, CancellationTokenSource cts)
    {
        ExtractFonts task = new(inputFile, destination);
        await task.Run(cts);
    }

    private async Task GenerateFile(CancellationTokenSource cts)
    {
        string[] files = Directory.GetFiles(FontsDir);
        if (files.Length == 0) return;

        List<Attachment> attachments = new();
        foreach (string file in files)
        {
            attachments.Add(new()
            {
                Filename = "fonts/" + Path.GetFileName(file),
                MimeType = MimeTypes.GetMimeTypeFromFile(file)
            });
        }

        await File.WriteAllTextAsync(FontsFile, attachments.ToJson(), cts.Token);
    }
}