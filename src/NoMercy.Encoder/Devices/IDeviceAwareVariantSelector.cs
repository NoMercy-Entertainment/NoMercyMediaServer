using NoMercy.Encoder.LiveTranscode;

namespace NoMercy.Encoder.Devices;

public interface IDeviceAwareVariantSelector
{
    VariantSelection Select(IReadOnlyList<VariantDescriptor> variants, DeviceCapabilities? caps);

    ClientCapabilities ApplyDeviceCaps(ClientCapabilities client, DeviceCapabilities? deviceCaps);
}
