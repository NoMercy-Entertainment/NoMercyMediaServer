using Newtonsoft.Json;
using NoMercy.Encoder.Format.Rules;
using NoMercy.EncoderV2.Core.Contracts;
using NoMercy.EncoderV2.Shared.Dtos;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.NmSystem.NewtonSoftConverters;
using NoMercy.NmSystem.SystemCalls;
using Serilog.Events;

namespace NoMercy.EncoderV2.Tasks;

public class ExtractFonts: ITaskContract
{
    private string InputFile { get; set; }
    private string Destination { get; set; }
    
    public ExtractFonts(string inputFile, string destination)
    {
        InputFile = inputFile;
        Destination = Path.Combine(destination, "fonts");
    }
    
    private string GetCommand()
    {
        // return $@"-dump_attachment:t """" -i ""{inputFile}"" -y -hide_banner -t 0 -f null null";
        Dictionary<string, dynamic?> args = new()
        {
            { "-dump_attachment:t", @"""" },
            { "-i", InputFile },
            { "-y", null },
            { "-hide_banner", null },
            { "-t", 0 },
            { "-f", "null" },
            // TODO: Test if -o null works
            { "-o", "null" }
        };
            
        return args.ToCLIString();
    }
    
    public async Task Run(CancellationTokenSource cts)
    {
        string command = GetCommand();
    
        Logger.Encoder(command, LogEventLevel.Debug);

        if (!Directory.Exists(Destination)) 
            Directory.CreateDirectory(Destination);
        
        await Shell.ExecAsync(AppFiles.FfProbePath, command, new() { WorkingDirectory = Destination }, cts);
        
        await GenerateFile(cts);
    }

    private async Task GenerateFile(CancellationTokenSource cts)
    {
        string attachmentsFile = Path.Combine(Destination, "fonts.json");

        string[] files = Directory.GetFiles(Destination);
        if (files.Length == 0) return;

        List<Attachment> attachments = [];
        foreach (string file in files)
        {
            attachments.Add(new()
            {
                Filename = "fonts/" + Path.GetFileName(file),
                MimeType = MimeTypes.GetMimeTypeFromFile(file)
            });
        }

        await File.WriteAllTextAsync(attachmentsFile, attachments.ToJson(), cts.Token);
    }

}