#pragma warning disable CS0246 // Type or namespace not found (V1 encoder types — will be restored in Tasks 4-7)
#pragma warning disable CS0103 // Name does not exist (V1 encoder types — will be restored in Tasks 4-7)
using AcoustID;
// TODO(encoder-v3): restore when V3 types are wired up (Tasks 4-7)
// using NoMercy.Encoder;
using NoMercy.NmSystem.Extensions;
using NoMercy.Providers.AcoustId.Models;
using NoMercy.Setup;

namespace NoMercy.Providers.AcoustId.Client;

public class AcoustIdFingerprintClient : AcoustIdBaseClient
{
    public AcoustIdFingerprintClient()
    {
        Configuration.ClientKey = ApiInfo.AcousticIdKey;
    }

    private Task<AcoustIdFingerprint?> WithFingerprint(
        string[] appendices,
        FingerPrintData fingerprintData,
        bool? priority = false
    )
    {
        Dictionary<string, string?> queryParams = new()
        {
            ["client"] = ApiInfo.AcousticIdKey,
            ["duration"] = fingerprintData.Duration.ToString(),
            ["fingerprint"] = fingerprintData.Fingerprint,
        };

        return Get<AcoustIdFingerprint>(
            "lookup?meta=" + string.Join("+", appendices),
            queryParams,
            priority
        );
    }

    public async ValueTask<AcoustIdFingerprint?> Lookup(string? file, bool? priority = false)
    {
        if (file == null)
            return null;

        string fingerprint = await FfMpeg.GetFingerprint(file);
        string duration = await FfMpeg.GetDuration(file);

        FingerPrintData? fingerprintData = new()
        {
            Fingerprint = fingerprint,
            Duration = duration.ToInt(),
        };

        if (fingerprintData == null)
            throw new("Fingerprint data is null");

        return await WithFingerprint(
            ["recordings", "releases", "tracks", "compress", "usermeta", "sources"],
            fingerprintData,
            priority
        );
    }
}
