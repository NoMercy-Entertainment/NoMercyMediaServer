namespace NoMercy.Encoder.LiveTranscode;

using NoMercy.Encoder.Analysis;
using NoMercy.Encoder.Hardware;

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
