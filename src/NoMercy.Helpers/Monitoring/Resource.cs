using LibreHardwareMonitor.Hardware;
using Newtonsoft.Json;

namespace NoMercy.Helpers.Monitoring;
public class Resource
{
    [JsonProperty("cpu")] public Cpu Cpu { get; set; } = new();
    // ReSharper disable once InconsistentNaming
    internal Dictionary<Identifier, Gpu> _gpu { get; set; } = [];
    [JsonProperty("memory")] public Memory Memory { get; set; } = new();
    [JsonProperty("gpu")] public List<Gpu> Gpu => _gpu.Values.ToList();
}