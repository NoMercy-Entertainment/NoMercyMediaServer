namespace NoMercyQueue.Core.Interfaces;

public interface IConfigurationStore
{
    string? GetValue(string key);
    void SetValue(string key, string value);
    Task SetValueAsync(string key, string value, Guid? modifiedBy = null);
    bool HasKey(string key);
}
