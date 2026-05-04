using NoMercy.Storage;

namespace NoMercy.Helpers;

/// <summary>
/// <see cref="ICredentialResolver"/> backed by <see cref="CredentialManager"/>.
/// </summary>
public class CredentialResolver : ICredentialResolver
{
    public (string AccessKey, string SecretKey)? Resolve(string credentialsRef)
    {
        UserPass? cred = CredentialManager.Credential(credentialsRef);
        if (cred is null)
            return null;

        // Treat an entry with empty fields the same as a missing entry — a
        // saved-but-blank credential stored an empty UserPass in the secrets
        // store, and propagating that to the AWS SDK / WebDAV client surfaces
        // as "Credential access key has length 0" instead of falling through
        // to the default credential chain or anonymous-mode wiring.
        if (string.IsNullOrEmpty(cred.Username) && string.IsNullOrEmpty(cred.Password))
            return null;

        return (cred.Username, cred.Password);
    }
}
