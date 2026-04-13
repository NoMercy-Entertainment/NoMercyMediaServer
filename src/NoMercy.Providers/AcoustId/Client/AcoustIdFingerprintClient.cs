using AcoustID;
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

        // TODO(encoder-v3): Wire up V3 IAudioFingerprinter for fingerprint/duration extraction
        throw new NotImplementedException(
            "AcoustID fingerprinting requires V3 IAudioFingerprinter — not yet wired (encoder-v3)"
        );
    }
}
