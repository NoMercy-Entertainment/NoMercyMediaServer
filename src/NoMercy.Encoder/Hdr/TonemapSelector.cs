namespace NoMercy.Encoder.Hdr;

using NoMercy.Encoder.Hardware;
using NoMercy.Encoder.Pipeline;
using NoMercy.Storage;
using ProfileHdrOptions = NoMercy.Encoder.Profiles.HdrOptions;

public class TonemapSelector : ITonemapSelector
{
    private static readonly HashSet<string> KnownAlgorithms = new(StringComparer.OrdinalIgnoreCase)
    {
        "hable",
        "mobius",
        "reinhard",
        "clip",
        "bt2390",
    };

    public TonemapStrategy SelectBest(
        IHardwareCapabilities hardware,
        IFfmpegCapabilities? ffmpeg = null
    )
    {
        // Priority: libplacebo (Vulkan GPU) → tonemap_opencl (OpenCL) → zscale+tonemap (CPU)
        if (ffmpeg is not null && ffmpeg.HasFilter("libplacebo"))
            return new(
                TonemapMethod.Libplacebo,
                "libplacebo=tonemapping=hable:color_primaries=bt709:color_trc=bt709:colorspace=bt709:format=yuv420p",
                true
            );

        if (ffmpeg is not null && ffmpeg.HasFilter("tonemap_opencl"))
            return new(
                TonemapMethod.TonemapOpencl,
                "tonemap_opencl=tonemap=hable:desat=0:format=nv12",
                true
            );

        return new(
            TonemapMethod.ZscaleTonemap,
            "zscale=t=linear:npl=100,format=gbrpf32le,zscale=p=bt709,tonemap=tonemap=hable:desat=0,zscale=t=bt709:m=bt709:r=tv,format=yuv420p",
            false
        );
    }

    /// <inheritdoc/>
    public TonemapPlan Build(
        ProfileHdrOptions? options,
        string? profileTonemapAlgorithm,
        IDecisionLogSink decisions,
        IStorage? storage = null
    )
    {
        // --- Algorithm resolution -------------------------------------------
        string rawAlgorithm = options?.TonemapAlgorithm ?? profileTonemapAlgorithm ?? "hable";

        string algorithm;
        if (KnownAlgorithms.Contains(rawAlgorithm))
        {
            algorithm = rawAlgorithm.ToLowerInvariant();
        }
        else
        {
            decisions.Add(
                new DecisionLog(
                    "plan",
                    "plan.tonemap_unknown_algorithm_defaulted",
                    $"Unknown tonemap algorithm '{rawAlgorithm}' — falling back to hable",
                    new { requested = rawAlgorithm, fallback = "hable" }
                )
            );
            algorithm = "hable";
        }

        // --- Peak nits -------------------------------------------------------
        int peakNits = options?.TonemapPeakNits ?? 100;

        // --- LUT path --------------------------------------------------------
        string? lutPath = options?.LutPath;
        if (lutPath is not null && storage is not null)
        {
            // Validate through IStorage's path guard (sync lease).
            // AcquireLocalPath throws StoragePathNotAllowedException when the path
            // is outside the configured allowed roots or fails structural checks.
            try
            {
                LocalPathLease lease = storage.AcquireLocalPath(lutPath);
                lease.DisposeAsync().AsTask().GetAwaiter().GetResult();

                string lutFilter = $"lut3d={lutPath}";

                decisions.Add(
                    new DecisionLog(
                        "plan",
                        "plan.tonemap_resolved",
                        $"Tonemap resolved: LUT path '{lutPath}' (algorithm={algorithm}, nits={peakNits}, lut=true)",
                        new
                        {
                            algorithm,
                            peakNits,
                            usedLut = true,
                            lutPath,
                        }
                    )
                );

                return new TonemapPlan(
                    Algorithm: algorithm,
                    PeakNits: peakNits,
                    LutFilterChain: lutFilter,
                    FilterStringFragment: lutFilter
                );
            }
            catch (StoragePathNotAllowedException ex)
            {
                decisions.Add(
                    new DecisionLog(
                        "plan",
                        "plan.tonemap_lut_path_rejected",
                        $"LUT path '{lutPath}' rejected ({ex.Message}) — falling back to algorithm '{algorithm}'",
                        new
                        {
                            lutPath,
                            reason = ex.Message,
                            fallbackAlgorithm = algorithm,
                        }
                    )
                );
                // Fall through to algorithm-based filter.
            }
        }

        // --- Algorithm-based zscale+tonemap chain ----------------------------
        string fragment =
            $"zscale=t=linear:npl={peakNits},format=gbrpf32le,zscale=p=bt709,tonemap=tonemap={algorithm}:desat=0,zscale=t=bt709:m=bt709:r=tv,format=yuv420p";

        decisions.Add(
            new DecisionLog(
                "plan",
                "plan.tonemap_resolved",
                $"Tonemap resolved: algorithm={algorithm}, nits={peakNits}, lut=false",
                new
                {
                    algorithm,
                    peakNits,
                    usedLut = false,
                }
            )
        );

        return new TonemapPlan(
            Algorithm: algorithm,
            PeakNits: peakNits,
            LutFilterChain: null,
            FilterStringFragment: fragment
        );
    }
}
