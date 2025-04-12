using System.Diagnostics;
using AcoustID;
using NoMercy.NmSystem.Extensions;
using NoMercy.NmSystem.Information;
using NoMercy.Providers.AcoustId.Models;
using NoMercy.Setup;

namespace NoMercy.Providers.AcoustId.Client;

public class AcoustIdFingerprintClient : AcoustIdBaseClient
{
    public AcoustIdFingerprintClient()
    {
        Configuration.ClientKey = ApiInfo.AcousticIdKey;
    }

    private Task<AcoustIdFingerprint?> WithFingerprint(string[] appendices, FingerPrintData fingerprintData,
        bool? priority = false)
    {
        Dictionary<string, string?> queryParams = new()
        {
            ["client"] = ApiInfo.AcousticIdKey,
            ["duration"] = fingerprintData.Duration.ToString(),
            ["fingerprint"] = fingerprintData.Fingerprint
        };

        return Get<AcoustIdFingerprint>("lookup?meta=" + string.Join("+", appendices), queryParams, priority);
    }

    public async ValueTask<AcoustIdFingerprint?> Lookup(string? file, bool? priority = false)
    {
        if (file == null) return null;

        Process process1 = new()
        {
            StartInfo =
            {
                FileName = AppFiles.FfmpegPath,
                Arguments = "-hide_banner -i \"" + file + "\" -map 0:a:0  -ar 11025 -f chromaprint -t 120 -",
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process1.Start();

        using StreamReader outputReader = process1.StandardOutput;
        using StreamReader errorReader = process1.StandardError;
    
        // Read both streams simultaneously
        Task<string> outputTask = outputReader.ReadToEndAsync();
        Task<string> errorTask = errorReader.ReadToEndAsync();
    
        await Task.WhenAll(outputTask, errorTask);
        string fingerprint = await outputTask;
        await process1.WaitForExitAsync();

        Process process2 = new()
        {
            StartInfo =
            {
                FileName = AppFiles.FfProbePath,
                Arguments = "-i \"" + file + "\" -hide_banner -show_entries format=duration -of default=noprint_wrappers=1:nokey=1",
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process2.Start();

        using StreamReader outputReader2 = process2.StandardOutput;
        using StreamReader errorReader2 = process2.StandardError;
    
        // Read both streams simultaneously
        Task<string> outputTask2 = outputReader2.ReadToEndAsync();
        Task<string> errorTask2 = errorReader2.ReadToEndAsync();
    
        await Task.WhenAll(outputTask2, errorTask2);
        string time = await outputTask2;
        await process2.WaitForExitAsync();

        FingerPrintData? fingerprintData = new()
        {
            Fingerprint = fingerprint,
            Duration = time.Trim().ToInt()
        };

        if (fingerprintData == null) throw new("Fingerprint data is null");

        return await WithFingerprint([
            "recordings",
            "releases",
            "tracks",
            "compress",
            "usermeta",
            "sources"
        ], fingerprintData, priority);
    }
}
