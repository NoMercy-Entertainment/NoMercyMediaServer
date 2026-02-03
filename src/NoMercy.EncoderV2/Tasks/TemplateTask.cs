using NoMercy.EncoderV2.Core.Contracts;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.EncoderV2.Tasks;

public class TemplateTask: ITaskContract
{
    private string InputFile { get; set; }
    private string Destination { get; set; }
    
    public TemplateTask(string inputFile, string destination)
    {
        InputFile = inputFile;
        Destination = destination;
    }
    
    private string GetCommand()
    {
        Dictionary<string, dynamic?> args = new()
        {
        };
            
        return args.ToCLIString();
    }

    public async Task Run(CancellationTokenSource cts)
    {
        string command = GetCommand();
    
        Logger.Encoder(command, LogEventLevel.Debug);
        
        string result = await Shell.ExecStdOutAsync(AppFiles.FfProbePath, command, cts: cts);
        if (string.IsNullOrEmpty(result)) return;
        
        await GenerateFile(result);
    }

    private async Task GenerateFile(string result)
    {
        
    }
}