using System.Security;
using System.Text;
using NeoSmart.SecureStore;
using Newtonsoft.Json;
using NoMercy.NmSystem.Information;
using NoMercy.Storage;

namespace NoMercy.Helpers;

public static class CredentialManager
{
    private static readonly IStorageBackend _backend = new SystemIoStorageBackend();

    private class SecretSerializer : ISecretSerializer
    {
        public T Deserialize<T>(SecureBuffer serialized)
        {
            string decoded = Encoding.UTF8.GetString(serialized.Buffer);
            return JsonConvert.DeserializeObject<T>(decoded)
                ?? throw new InvalidOperationException();
        }

        SecureBuffer ISecretSerializer.Serialize<T>(T input)
        {
            string serialized = JsonConvert.SerializeObject(input);
            return new(Encoding.UTF8.GetBytes(serialized));
        }
    }

    public static UserPass? Credential(string target)
    {
        if (!_backend.FileExists(AppFiles.SecretsStore))
            return null;

        using SecretsManager secretsManager = SecretsManager.LoadStore(AppFiles.SecretsStore);
        secretsManager.DefaultSerializer = new SecretSerializer();
        secretsManager.LoadKeyFromFile(AppFiles.SecretsKey);

        if (secretsManager.TryGetValue(target, out UserPass? output))
            return output;

        return null;
    }

    public static void SetCredentials(
        string target,
        string username,
        string password,
        string apiKey
    )
    {
        bool exists = _backend.FileExists(AppFiles.SecretsStore);

        using SecretsManager secretsManager = exists
            ? SecretsManager.LoadStore(AppFiles.SecretsStore)
            : SecretsManager.CreateStore();
        secretsManager.DefaultSerializer = new SecretSerializer();
        if (!exists)
        {
            secretsManager.GenerateKey();
            secretsManager.ExportKey(AppFiles.SecretsKey);
        }
        else
        {
            secretsManager.LoadKeyFromFile(AppFiles.SecretsKey);
        }

        secretsManager.Set(target, new UserPass(username, password, apiKey));

        secretsManager.SaveStore(AppFiles.SecretsStore);
    }

    public static bool RemoveCredentials(string target)
    {
        if (!_backend.FileExists(AppFiles.SecretsStore))
            return false;

        using SecretsManager secretsManager = SecretsManager.LoadStore(AppFiles.SecretsStore);
        secretsManager.LoadKeyFromFile(AppFiles.SecretsKey);
        return secretsManager.Delete(target);
    }

    public static SecureString ConvertToSecureString(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        SecureString securePassword = new();

        foreach (char c in password)
            securePassword.AppendChar(c);

        securePassword.MakeReadOnly();
        return securePassword;
    }
}
