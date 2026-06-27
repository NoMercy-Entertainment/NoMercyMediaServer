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

using Microsoft.Extensions.Logging;
using NoMercy.Encoder.Infrastructure;

namespace NoMercy.Encoder.Hardware;

public sealed class NvmlGpuSampler : ProcessResourceMonitor
{
    private static readonly TimeSpan MinSampleInterval = TimeSpan.FromSeconds(2);

    private readonly IProcessRunner _processRunner;
    private readonly ILogger<NvmlGpuSampler> _logger;
    private readonly SemaphoreSlim _sampleLock = new(1, 1);
    private DateTime _lastSampleAt = DateTime.MinValue;
    private IReadOnlyList<GpuProcessSample> _lastSamples = [];

    public NvmlGpuSampler(IProcessRunner processRunner, ILogger<NvmlGpuSampler> logger)
        : base(null)
    {
        _processRunner = processRunner;
        _logger = logger;
    }

    public override async Task<IReadOnlyList<GpuProcessSample>> SampleGpuAsync(
        CancellationToken cancellationToken = default
    )
    {
        await _sampleLock.WaitAsync(cancellationToken);
        try
        {
            if (DateTime.UtcNow - _lastSampleAt < MinSampleInterval)
                return _lastSamples;

            IReadOnlyList<GpuProcessSample> fresh = await RunNvidiaSmiAsync(cancellationToken);
            _lastSamples = fresh;
            _lastSampleAt = DateTime.UtcNow;
            return fresh;
        }
        finally
        {
            _sampleLock.Release();
        }
    }

    private async Task<IReadOnlyList<GpuProcessSample>> RunNvidiaSmiAsync(
        CancellationToken cancellationToken
    )
    {
        try
        {
            using CancellationTokenSource timeoutCts = new(TimeSpan.FromSeconds(5));
            using CancellationTokenSource linkedCts =
                CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken,
                    timeoutCts.Token
                );

            ProcessResult result = await _processRunner.RunAsync(
                "nvidia-smi",
                ["--query-compute-apps=pid,used_gpu_memory", "--format=csv,noheader,nounits"],
                workingDirectory: null,
                cancellationToken: linkedCts.Token
            );

            if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.StdOut))
                return [];

            return ParseNvidiaSmiOutput(result.StdOut);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("nvidia-smi timed out — returning empty GPU sample");
            return [];
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "nvidia-smi unavailable or failed — GPU utilization will read 0");
            return [];
        }
    }

    internal static IReadOnlyList<GpuProcessSample> ParseNvidiaSmiOutput(string stdOut)
    {
        List<GpuProcessSample> samples = [];

        foreach (string line in stdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmedLine = line.Trim();
            if (string.IsNullOrEmpty(trimmedLine))
                continue;

            string[] fields = trimmedLine.Split(',');
            if (fields.Length < 2)
                continue;

            if (!int.TryParse(fields[0].Trim(), out int pid))
                continue;

            if (!long.TryParse(fields[1].Trim(), out long memoryMb))
                continue;

            samples.Add(
                new GpuProcessSample(
                    Pid: pid,
                    GpuIndex: 0,
                    EncoderUtilizationPercent: 0,
                    EncoderMemoryBytes: memoryMb * 1024 * 1024
                )
            );
        }

        return samples;
    }
}
