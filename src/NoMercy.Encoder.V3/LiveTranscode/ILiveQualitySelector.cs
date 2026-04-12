namespace NoMercy.Encoder.V3.LiveTranscode;

using NoMercy.Encoder.V3.Analysis;
using NoMercy.Encoder.V3.Hardware;

public interface ILiveQualitySelector
{
    LiveQuality[] GetAvailableQualities(
        MediaInfo input,
        ClientCapabilities client,
        SpeedIndex speeds,
        IHardwareCapabilities hardware
    );

    LiveQuality SelectOptimal(
        MediaInfo input,
        ClientCapabilities client,
        SpeedIndex speeds,
        IHardwareCapabilities hardware
    );
}
