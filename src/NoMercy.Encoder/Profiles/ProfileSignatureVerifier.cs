using System.Security.Cryptography;
using System.Text;
using NoMercy.Database.Models.Media;
using NoMercy.Encoder.Errors;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;

namespace NoMercy.Encoder.Profiles;

/// <summary>
/// Verifies detached Ed25519 signatures on imported encoding profiles using
/// BouncyCastle's Ed25519 signer. The signature covers SHA-256(profileJson)
/// encoded as a 32-byte digest — not the raw JSON bytes — so the canonical
/// form is deterministic regardless of whitespace normalisation.
/// </summary>
public sealed class ProfileSignatureVerifier : IProfileSignatureVerifier
{
    /// <inheritdoc />
    public EncoderRule? Verify(
        string profileJson,
        string fingerprint,
        string base64Signature,
        Func<string, TrustedPublisherKey?> keyLookup
    )
    {
        TrustedPublisherKey? key = keyLookup(fingerprint);

        if (key is null)
        {
            return new EncoderRule(
                EncoderRuleId.ImportPublisherUntrusted,
                EncoderRuleSeverity.Error,
                "publisher_key_fingerprint",
                $"Publisher key with fingerprint '{fingerprint}' is not in the trusted keys list.",
                "Add the publisher's public key via POST /api/v1/encoder/trusted-publishers before importing."
            );
        }

        byte[] publicKeyBytes;
        byte[] signatureBytes;
        byte[] digest;

        try
        {
            publicKeyBytes = Convert.FromBase64String(key.PublicKeyBase64);
        }
        catch (FormatException)
        {
            return new EncoderRule(
                EncoderRuleId.ImportSignatureInvalid,
                EncoderRuleSeverity.Error,
                "signature",
                "The stored public key is not valid base64 — the trusted key record may be corrupt.",
                "Re-register the publisher's public key via POST /api/v1/encoder/trusted-publishers."
            );
        }

        try
        {
            signatureBytes = Convert.FromBase64String(base64Signature);
        }
        catch (FormatException)
        {
            return new EncoderRule(
                EncoderRuleId.ImportSignatureInvalid,
                EncoderRuleSeverity.Error,
                "signature",
                "The profile signature field is not valid base64.",
                "The profile JSON does not match the supplied signature — re-export from the source."
            );
        }

        digest = SHA256.HashData(Encoding.UTF8.GetBytes(profileJson));

        Ed25519PublicKeyParameters pubKey = new(publicKeyBytes, 0);
        Ed25519Signer signer = new();
        signer.Init(false, pubKey);
        signer.BlockUpdate(digest, 0, digest.Length);

        bool valid;
        try
        {
            valid = signer.VerifySignature(signatureBytes);
        }
        catch (Exception)
        {
            valid = false;
        }

        if (!valid)
        {
            return new EncoderRule(
                EncoderRuleId.ImportSignatureInvalid,
                EncoderRuleSeverity.Error,
                "signature",
                "The profile JSON does not match the supplied Ed25519 signature.",
                "The profile JSON does not match the supplied signature — re-export from the source."
            );
        }

        return null;
    }
}
