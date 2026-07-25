// -----------------------------------------------------------------------------
//  Copyright (c) 2024-present NoMercy Entertainment. All rights reserved.
//
//  This file is part of NoMercy MediaServer, source-available software (NOT open
//  source). Personal use and contributions are welcome; distribution, resale,
//  relicensing, and commercial exploitation are prohibited without explicit
//  written consent. See LICENSE for full terms. Distributed WITHOUT ANY WARRANTY.
//
//  SPDX-License-Identifier: LicenseRef-NoMercy-Proprietary
// -----------------------------------------------------------------------------

using NoMercy.Encoder.Hardware;

namespace NoMercy.Encoder.Pipeline.Optimizer;

public class ResourceAllocator(IHardwareCapabilities hardware, IResourceMonitor monitor)
{
    public async Task AllocateResourcesAsync(List<ExecutionGroup> groups)
    {
        IReadOnlyList<GpuProcessSample> gpuSamples = hardware.HasGpu
            ? await monitor.SampleGpuAsync()
            : [];
        int leastLoadedGpuIndex = FindLeastLoadedGpuIndex(gpuSamples);
        int softwareThreadBudget = Math.Max(1, Environment.ProcessorCount / 2);

        for (int groupIndex = 0; groupIndex < groups.Count; groupIndex++)
        {
            ExecutionGroup group = groups[groupIndex];

            if (group.RequiresGpu && hardware.HasGpu)
            {
                string gpuDeviceId =
                    leastLoadedGpuIndex < hardware.Gpus.Count
                        ? hardware.Gpus[leastLoadedGpuIndex].Name
                        : hardware.Gpus[0].Name;

                groups[groupIndex] = group with { DeviceId = gpuDeviceId };
                continue;
            }

            if (group is { RequiresGpu: false, CpuThreadsRequired: 0 })
            {
                groups[groupIndex] = group with { CpuThreadsRequired = softwareThreadBudget };
            }
        }
    }

    private static int FindLeastLoadedGpuIndex(IReadOnlyList<GpuProcessSample> samples)
    {
        if (samples.Count == 0)
            return 0;

        Dictionary<int, int> utilizationByGpu = [];

        foreach (GpuProcessSample sample in samples)
        {
            if (!utilizationByGpu.TryGetValue(sample.GpuIndex, out int current))
                current = 0;

            utilizationByGpu[sample.GpuIndex] = current + sample.EncoderUtilizationPercent;
        }

        int leastLoadedIndex = 0;
        int lowestUtilization = int.MaxValue;

        foreach (KeyValuePair<int, int> entry in utilizationByGpu)
        {
            if (entry.Value < lowestUtilization)
            {
                lowestUtilization = entry.Value;
                leastLoadedIndex = entry.Key;
            }
        }

        return leastLoadedIndex;
    }

    public bool CheckMemoryCeiling(List<ExecutionGroup> groups, long availableMemoryMb)
    {
        long estimatedPeakMb = 0;
        foreach (ExecutionGroup group in groups)
        {
            int encodeCount = group.Nodes.Count(n => n.Operation == OperationType.Encode);
            estimatedPeakMb += encodeCount * 200;
        }

        return estimatedPeakMb < availableMemoryMb * 75 / 100;
    }
}
