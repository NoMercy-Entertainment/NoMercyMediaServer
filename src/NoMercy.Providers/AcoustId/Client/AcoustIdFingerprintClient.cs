
using System.Diagnostics;
using AcoustID;
using NoMercy.Networking;
using NoMercy.NmSystem;
using NoMercy.Providers.AcoustId.Models;

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

    public ValueTask<AcoustIdFingerprint?> Lookup(string? file, bool? priority = false)
    {
        if (file == null) return ValueTask.FromResult<AcoustIdFingerprint?>(null);

        Process process = new();
        process.StartInfo.FileName = AppFiles.FpCalcPath;
        process.StartInfo.Arguments = "-json \"" + file + "\"";

        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;

        process.Start();

        string output = process.StandardOutput.ReadToEnd();

        process.WaitForExit();

        FingerPrintData? fingerprintData = output.FromJson<FingerPrintData>();

        if (fingerprintData == null) throw new("Fingerprint data is null");

        return new(WithFingerprint([
            "recordings",
            "releases",
            "tracks",
            "compress",
            "usermeta",
            "sources"
        ], fingerprintData, priority));
    }
}
