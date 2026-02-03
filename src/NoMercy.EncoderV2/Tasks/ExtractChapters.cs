using System.Text;
using Newtonsoft.Json;
using NoMercy.EncoderV2.Core.Contracts;
using NoMercy.EncoderV2.Shared.Dtos;
using NoMercy.NmSystem.Dto;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.EncoderV2.Tasks;

public class ExtractChapters: ITaskContract
{
    private string InputFile { get; set; }
    private string Destination { get; set; }
    private string FileName { get; set; }
    
    public ExtractChapters(string inputFile, string destination, string fileName)
    {
        InputFile = inputFile;
        Destination = destination;
        FileName = fileName;
    }
    
    private string GetCommand()
    {
        // return $"-v quiet -print_format json -show_chapters \"{inputFile}\"";
        Dictionary<string, dynamic?> args = new()
        {
            { "-v", "quiet" },
            { "-print_format", "json" },
            { "-show_chapters", InputFile }
        };
            
        return args.ToCLIString();
    }

    public async Task Run(CancellationTokenSource cts)
    {
        string command = GetCommand();
    
        Logger.Encoder(command, LogEventLevel.Debug);
        
        string result = await Shell.ExecStdOutAsync(AppFiles.FfProbePath, command, cts: cts);
        if (string.IsNullOrEmpty(result)) return;
        
        FfprobeChapterRoot? root = JsonConvert.DeserializeObject<FfprobeChapterRoot>(result);
        if (root?.Chapters is null) return;
        if (root.Chapters.Length == 0) return;

        await GenerateFile(root);
    }

    public static async Task RunStatic(string inputFile, string destination, string fileName, CancellationTokenSource cts)
    {
        ExtractChapters task = new(inputFile, destination, fileName);
        await task.Run(cts);
    }

    private async Task GenerateFile(FfprobeChapterRoot root)
    {
        string chapterFile = $"{Destination}/{FileName}.vtt";
        
        if(!Path.Exists(Destination))
            Directory.CreateDirectory(Destination);

        await using StreamWriter writer = new(chapterFile);

        await writer.WriteLineAsync("WEBVTT");
        await writer.WriteLineAsync();

        foreach (FfprobeChapter chapter in root.Chapters)
        {
            int id = Array.IndexOf(root.Chapters, chapter) + 1;
            await writer.WriteLineAsync($"Chapter {id}");
            await writer.WriteLineAsync($"{chapter.StartTime.ToHis()} --> {chapter.EndTime.ToHis()}");
            await writer.WriteLineAsync($"{chapter.FfprobeTags.Title}");
            await writer.WriteLineAsync();
        }
    }
}