using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Hardware;

namespace NoMercy.Encoder.LiveTranscode;

public interface ILiveQualitySelector
{
    LiveQuality[] GetAvailableQualities(
        MediaInfo input,
        ClientCapabilities client,
        SpeedIndex speeds,
        IResourceBudget budget
    );

    LiveQuality SelectOptimal(
        MediaInfo input,
        ClientCapabilities client,
        SpeedIndex speeds,
        IResourceBudget budget
    );
}
