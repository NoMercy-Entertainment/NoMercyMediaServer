using System.Text;
using NoMercy.EncoderV2.Core.Contracts;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;
using SixLabors.ImageSharp;

namespace NoMercy.EncoderV2.Tasks;

public class GenerateSprite: ITaskContract
{
    private string Destination { get; }
    private string BaseName { get; }
    private int FrameRate { get; }
    private string SpriteFilename { get; }
    private string TimeFilename { get; }
    private string SpriteFile { get; }
    private string TimeFile { get; }
    private string ThumbnailsFolder { get; }
    private string[] ImageFiles { get; }
    private int GridHeight { get; }
    private int GridWidth { get; }
    private string PathGrep { get; }
        
    public GenerateSprite(string destination, int width, int height, int frameRate)
    {
        Destination = destination;
        FrameRate = frameRate;
        
        BaseName = $"thumbs_{width}x{height}";
        
        SpriteFilename = BaseName + ".webp";
        TimeFilename = BaseName + ".vtt";
        
        SpriteFile = Path.Combine(Destination, SpriteFilename);
        TimeFile = Path.Combine(Destination, TimeFilename);

        ThumbnailsFolder = Path.Combine(Destination, BaseName);
        PathGrep = Path.Combine(ThumbnailsFolder, BaseName + "-%04d.jpg");
        
        ImageFiles = Directory.GetFiles(ThumbnailsFolder)
            .OrderBy(f => f)
            .ToArray();

        GridWidth = (int)Math.Ceiling(Math.Sqrt(ImageFiles.Length));
        GridHeight = (int)Math.Ceiling((double)ImageFiles.Length / GridWidth);
    }

    private string GetCommand()
    {
        // $"-i \"{Path.Combine(ThumbnailsFolder, BaseName + "-%04d.jpg")}\" -filter_complex tile=\"{gridWidth}x{gridHeight}\" -y \"{SpriteFile}\"";
        StringBuilder command = new();
        command.Append($"-i \"{PathGrep}\" ");
        command.Append($"-filter_complex tile=\"{GridWidth}x{GridHeight}\" ");
        command.Append($"-y \"{SpriteFile}\" ");
        
        return command.ToString();
    }
    
    public async Task Run(CancellationTokenSource cts)
    {
        if (ImageFiles.Length == 0) return;

        string montageCommand = GetCommand();
        
        Logger.Encoder(montageCommand, LogEventLevel.Debug);
        
        await Shell.ExecAsync(AppFiles.FfmpegPath, montageCommand, new() { WorkingDirectory = Destination }, cts);
        
        await GenerateFile();
        
        if (Directory.Exists(ThumbnailsFolder))
        {
            Logger.Encoder($"Deleting folder {ThumbnailsFolder}", LogEventLevel.Debug);
            Directory.Delete(ThumbnailsFolder, true);
        }
    }
    
    public static async Task RunStatic(string destination, int width, int height, int frameRate, CancellationTokenSource cts)
    {
        GenerateSprite task = new(destination, width, height, frameRate);
        await task.Run(cts);
    }
    
    private async Task GenerateFile()
    {
        (int thumbWidth, int thumbHeight) = GetImageDimensions(ImageFiles.First());

        List<string> times = CreateTimeInterval(ImageFiles.Length * FrameRate + 1, FrameRate);

        int dstX = 0;
        int dstY = 0;

        int jpg = 1;
        int line = 1;

        await using StreamWriter writer = new(TimeFile);
        await writer.WriteLineAsync("WEBVTT");
        await writer.WriteLineAsync("");

        foreach (string time in times.Take(times.Count - 1))
        {
            int index = times.IndexOf(time);
            await writer.WriteLineAsync(jpg.ToString());
            await writer.WriteLineAsync($"{time} --> {times[index + 1]}");
            await writer.WriteLineAsync($"{SpriteFilename}#xywh={dstX},{dstY},{thumbWidth},{thumbHeight}");
            await writer.WriteLineAsync("");

            if (line > GridHeight) continue;

            if (jpg % GridWidth == 0)
            {
                dstX = 0;
                dstY += thumbHeight;
            }
            else
            {
                dstX += thumbWidth;
            }

            jpg++;
        }
    }
    
    private (int width, int height) GetImageDimensions(string imagePath)
    {
        using Image image = Image.Load(imagePath);
        return (image.Width, image.Height);
    }

    private List<string> CreateTimeInterval(double duration, int interval)
    {
        DateTime d = new DateTime().Date;
        List<string> timeArr = new();

        for (int i = 0; i <= duration / interval; i++)
        {
            string hours = d.Hour.ToString("D2");
            string minute = d.Minute.ToString("D2");
            string second = d.Second.ToString("D2");
            string miliSecond = d.Millisecond.ToString("D3");

            timeArr.Add($"{hours}:{minute}:{second}.{miliSecond}");

            d = d.AddSeconds(interval);
        }

        return timeArr;
    }
}


