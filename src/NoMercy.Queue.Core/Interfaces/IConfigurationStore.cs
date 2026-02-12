namespace NoMercy.Queue.Core.Interfaces;

public interface IConfigurationStore
{
    string? GetValue(string key);
    void SetValue(string key, string value);
    bool HasKey(string key);
}
