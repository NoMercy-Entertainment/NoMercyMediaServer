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

        return (cred.Username, cred.Password);
    }
}
