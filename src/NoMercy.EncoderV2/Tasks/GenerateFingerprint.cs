using NoMercy.EncoderV2.Core.Contracts;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.EncoderV2.Tasks;

public class GenerateFingerprint: ITaskContract
{
    private string InputFile { get; set; }
    private string? Fingerprint { get; set; }
    
    public GenerateFingerprint(string inputFile)
    {
        InputFile = inputFile;
    }
    
    private string GetCommand()
    {
        // return "-hide_banner -i \"" + file + "\" -map 0:a:0  -ar 11025 -f chromaprint -t 120 -"
        Dictionary<string, dynamic?> args = new()
        {
            { "-hide_banner", null },
            { "-i", InputFile },
            { "-map", "0:a:0" },
            { "-ar", 11025 },
            { "-f", "chromaprint" },
            { "-t", 120 },
            { "-", null }
        };
            
        return args.ToCLIString();
    }

    public async Task Run(CancellationTokenSource cts)
    {
        string command = GetCommand();
    
        Logger.Encoder(command, LogEventLevel.Debug);
        
        string result = await Shell.ExecStdOutAsync(AppFiles.FfmpegPath, command, cts: cts);
        if (string.IsNullOrEmpty(result)) return;
        
        Fingerprint = result.Trim();
    }

    public string? Get()
    {
        return Fingerprint;
    }

    public static async Task<string?> GetStatic(string file, CancellationTokenSource cts)
    {
        GenerateFingerprint generateFingerprint = new(file);
        await generateFingerprint.Run(cts);
        return generateFingerprint.Get();
    }
}