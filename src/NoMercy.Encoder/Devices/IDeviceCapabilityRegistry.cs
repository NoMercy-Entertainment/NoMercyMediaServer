namespace NoMercy.Encoder.Devices;

public interface IDeviceCapabilityRegistry
{
    DeviceCapabilities? Get(string deviceId);
    void Set(string deviceId, DeviceCapabilities capabilities);
    void Invalidate(string deviceId);
    Task<DeviceCapabilities?> LoadFromDbAsync(string deviceId, CancellationToken ct);
}
