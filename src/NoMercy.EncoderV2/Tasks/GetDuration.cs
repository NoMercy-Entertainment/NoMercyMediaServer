using NoMercy.EncoderV2.Core.Contracts;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.EncoderV2.Tasks;

public class GetDuration: ITaskContract
{
    private string InputFile { get; set; }
    private string? Duration { get; set; }
    
    public GetDuration(string inputFile)
    {
        InputFile = inputFile;
    }
    
    private string GetCommand()
    {
        // return $"-i \"{InputFile}\" -hide_banner -show_entries format=duration -of default=noprint_wrappers=1:nokey=1"
        Dictionary<string, dynamic?> args = new()
        {
            { "-i", InputFile },
            { "-hide_banner", null },
            { "-show_entries", "format=duration" },
            { "-of", "default=noprint_wrappers=1:nokey=1" }
        };
            
        return args.ToCLIString();
    }

    public async Task Run(CancellationTokenSource cts)
    {
        string command = GetCommand();
    
        Logger.Encoder(command, LogEventLevel.Debug);
        
        string result = await Shell.ExecStdOutAsync(AppFiles.FfProbePath, command, cts: cts);

        if (string.IsNullOrEmpty(result)) throw new("Failed to get duration");

        if (result.Contains("N/A")) throw new("Failed to get duration");

        if (result.Contains("Duration")) result = result.Split("Duration: ")[1].Split(",")[0];
        
        Duration = result.Trim();
    }

    public double? Get()
    {
        return Duration?.ToDouble();
    }

    public static async Task<double?> GetStatic(string file, CancellationTokenSource cts)
    {
        GetDuration getDuration = new(file);
        await getDuration.Run(cts);
        return getDuration.Get();
    }
}