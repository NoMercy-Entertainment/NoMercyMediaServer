namespace NoMercy.Encoder.V3.Commands;

public class FilterGraphBuilder
{
    private readonly List<string> _chains = [];

    // Add a simple filter: [inputLabel]filter=params[outputLabel]
    public FilterGraphBuilder AddFilter(string inputLabel, string filter, string outputLabel)
    {
        _chains.Add($"[{inputLabel}]{filter}[{outputLabel}]");
        return this;
    }

    // Add a split filter: [inputLabel]split=N[out1][out2]...[outN]
    public FilterGraphBuilder AddSplit(string inputLabel, string[] outputLabels)
    {
        string outputs = string.Join("", outputLabels.Select(l => $"[{l}]"));
        _chains.Add($"[{inputLabel}]split={outputLabels.Length}{outputs}");
        return this;
    }

    // Add a scale filter: [inputLabel]scale=W:H[outputLabel]
    public FilterGraphBuilder AddScale(string inputLabel, int width, int height, string outputLabel)
    {
        _chains.Add($"[{inputLabel}]scale={width}:{height}[{outputLabel}]");
        return this;
    }

    // Scale maintaining aspect ratio (height=-2 for even value)
    public FilterGraphBuilder AddScaleWidth(string inputLabel, int width, string outputLabel)
    {
        _chains.Add($"[{inputLabel}]scale={width}:-2[{outputLabel}]");
        return this;
    }

    // Add tonemap filter chain for HDR→SDR
    public FilterGraphBuilder AddTonemap(string inputLabel, string algorithm, string outputLabel)
    {
        // CPU tonemapping: zscale + tonemap chain
        string chain =
            $"[{inputLabel}]zscale=t=linear:npl=100,format=gbrpf32le,zscale=p=bt709,tonemap=tonemap={algorithm}:desat=0,zscale=t=bt709:m=bt709:r=tv,format=yuv420p[{outputLabel}]";
        _chains.Add(chain);
        return this;
    }

    // Add libplacebo tonemap (GPU-accelerated)
    public FilterGraphBuilder AddLibplaceboTonemap(
        string inputLabel,
        string algorithm,
        string outputLabel
    )
    {
        string chain =
            $"[{inputLabel}]libplacebo=tonemapping={algorithm}:color_primaries=bt709:color_trc=bt709:colorspace=bt709:format=yuv420p[{outputLabel}]";
        _chains.Add(chain);
        return this;
    }

    // Add deinterlace filter
    public FilterGraphBuilder AddDeinterlace(
        string inputLabel,
        string outputLabel,
        string method = "yadif"
    )
    {
        _chains.Add($"[{inputLabel}]{method}[{outputLabel}]");
        return this;
    }

    // Add crop filter
    public FilterGraphBuilder AddCrop(
        string inputLabel,
        int width,
        int height,
        int x,
        int y,
        string outputLabel
    )
    {
        _chains.Add($"[{inputLabel}]crop={width}:{height}:{x}:{y}[{outputLabel}]");
        return this;
    }

    // Build the complete filter_complex string
    public string Build()
    {
        if (_chains.Count == 0)
            return "";
        return string.Join(";", _chains);
    }

    // Check if any filters have been added
    public bool HasFilters => _chains.Count > 0;
}
