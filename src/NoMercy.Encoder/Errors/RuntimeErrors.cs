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

namespace NoMercy.Encoder.Errors;

/// <summary>
/// Factory for the <see cref="EncoderErrorShape"/> records that
/// controllers serialise when a pipeline operation fails. Each factory
/// pins the <see cref="EncoderRuleId"/> so the dashboard can deep-link
/// to docs, supplies the suggestion text, and (via the
/// <see cref="HttpStatusCode"/> property on the wrapping
/// <see cref="EncoderRuntimeException"/>) tells the controller layer
/// what HTTP code to return.
///
/// <para>Always go through these factories — never construct
/// <see cref="EncoderErrorShape"/> directly. The factory is the only
/// place the ID + status mapping lives, so a single edit there keeps
/// the docs site, dashboard chip renderer, and HTTP layer aligned.</para>
/// </summary>
public static class RuntimeErrors
{
    /// <summary>
    /// All NVENC slots on the GPU are in use. The dashboard treats this
    /// as 409 Conflict so the caller knows it's a transient capacity
    /// issue and not a config error.
    /// </summary>
    public static EncoderRuntimeException GpuCapacityExhausted(
        string gpu,
        int sessions,
        string? suggestion = null
    )
    {
        EncoderErrorShape shape = new(
            EncoderRuleId.GpuCapacityExhausted,
            $"All NVENC sessions on '{gpu}' are in use ({sessions} active). New encode held until a slot frees.",
            suggestion
                ?? "Set hardware_preference=force_software for this profile to encode on CPU instead, or wait for a slot to free.",
            new GpuCapacityDetails(gpu, sessions)
        );
        return new EncoderRuntimeException(shape, 409);
    }

    public static EncoderRuntimeException EncoderInitFailed(string handle, string reason) =>
        new(
            new EncoderErrorShape(
                EncoderRuleId.EncoderInitFailed,
                $"Encoder '{handle}' failed to initialise: {reason}",
                "Check the ffmpeg capability probe at /api/v1/encoder/capabilities — the encoder may be missing from this build.",
                new EncoderInitDetails(handle, reason)
            ),
            500
        );

    public static EncoderRuntimeException SourceNotAccessible(string path) =>
        new(
            new EncoderErrorShape(
                EncoderRuleId.SourceNotAccessible,
                $"Source file is not accessible: {path}",
                "Verify the file exists and the encoder process has read permission. For network shares, check the mount is active.",
                new SourcePathDetails(path)
            ),
            404
        );

    public static EncoderRuntimeException SourceReadError(string path, string detail) =>
        new(
            new EncoderErrorShape(
                EncoderRuleId.SourceReadError,
                $"Source read failed: {path} — {detail}",
                "If this is a network share, verify connectivity. For local files, run a filesystem check.",
                new SourcePathDetails(path)
            ),
            500
        );

    public static EncoderRuntimeException OutputWriteError(string path, string detail) =>
        new(
            new EncoderErrorShape(
                EncoderRuleId.OutputWriteError,
                $"Output write failed: {path} — {detail}",
                "Check disk space and that the output directory has write permission for the encoder process.",
                new OutputPathDetails(path)
            ),
            500
        );

    public static EncoderRuntimeException OutputPathNotAllowed(string path, string reason) =>
        new(
            new EncoderErrorShape(
                EncoderRuleId.OutputPathNotAllowed,
                $"Output path rejected: {reason} (path={path})",
                "Add the parent directory to EncoderOptions.Storage.AllowedRoots, or write the output to an existing allowed root.",
                new OutputPathDetails(path)
            ),
            403
        );

    /// <summary>
    /// The cluster license server returned 403 — the worker is no
    /// longer entitled to participate in distributed encoding. Free-tier
    /// installs land here on the first heartbeat after the entitlement
    /// is revoked.
    /// </summary>
    public static EncoderRuntimeException LicenseRevoked(string reason) =>
        new(
            new EncoderErrorShape(
                EncoderRuleId.LicenseRevoked,
                $"Cluster license revoked: {reason}",
                "Re-link the server to your NoMercy account at https://nomercy.tv/dashboard/devices, or downgrade to standalone mode.",
                null
            ),
            403
        );

    public static EncoderRuntimeException LicenseUnreachable(string url) =>
        new(
            new EncoderErrorShape(
                EncoderRuleId.LicenseUnreachable,
                $"Cluster license server is unreachable at {url}.",
                "Check this server's outbound connectivity to api.nomercy.tv. Distributed encoding will resume automatically when the server is reachable again.",
                null
            ),
            503
        );

    public static EncoderRuntimeException HardwareForcedButUnavailable(string requested) =>
        new(
            new EncoderErrorShape(
                EncoderRuleId.HardwareForcedButUnavailable,
                $"hardware_preference=force_hardware was set but no compatible hardware encoder is available for '{requested}'.",
                "Switch hardware_preference to prefer_hardware (auto-fallback to software), or install the missing GPU drivers and run /api/v1/encoder/hardware/benchmark.",
                new HardwareDetails(requested)
            ),
            422
        );

    public static EncoderRuntimeException JobInterruptedNoCheckpoint(string jobId) =>
        new(
            new EncoderErrorShape(
                EncoderRuleId.JobInterruptedNoCheckpoint,
                $"Job {jobId} was interrupted by shutdown and has no checkpoint to resume from.",
                "Re-dispatch the job — it will start from the beginning.",
                new JobDetails(jobId)
            ),
            500
        );

    public static EncoderRuntimeException DiscDriveBusy(string drivePath) =>
        new(
            new EncoderErrorShape(
                EncoderRuleId.DiscDriveBusy,
                $"Drive {drivePath} is already busy with an active rip.",
                "Wait for the active rip to finish, or insert the disc in a different drive.",
                new DriveDetails(drivePath)
            ),
            409
        );

    public static EncoderRuntimeException DiscAacsCertMissing(string volumeId) =>
        new(
            new EncoderErrorShape(
                EncoderRuleId.DiscAacsCertMissing,
                $"AACS: no matching certificate for volume {volumeId}.",
                "Add the volume key to KEYDB.cfg or point EncoderOptions.BluRay.KeyDbOverridePath at your own KEYDB.",
                new DiscDetails(volumeId)
            ),
            409
        );

    public static EncoderRuntimeException DiscBdplusConverterMissing(string volumeId) =>
        new(
            new EncoderErrorShape(
                EncoderRuleId.DiscBdplusConverterMissing,
                $"BD+: no matching converter for volume {volumeId}.",
                "Update libbdplus / the BDSVM converter database, or rip the disc on a system that has it.",
                new DiscDetails(volumeId)
            ),
            409
        );

    public static EncoderRuntimeException DiscReadError(
        string drivePath,
        string ffmpegStderrTail
    ) =>
        new(
            new EncoderErrorShape(
                EncoderRuleId.DiscReadError,
                $"Disc read failed on {drivePath}.",
                "Clean the disc surface and try again. If it persists, the disc may be physically damaged.",
                new DiscReadDetails(drivePath, ffmpegStderrTail)
            ),
            500
        );

    public static EncoderRuntimeException DistributionHmacInvalid() =>
        new(
            new EncoderErrorShape(
                EncoderRuleId.DistributionHmacInvalid,
                "HMAC signature on the inbound request did not verify.",
                "Confirm both coordinator and worker share the same DistributedEncodingSigningKey (or that the worker's cluster token is fresh).",
                null
            ),
            401
        );

    public static EncoderRuntimeException DistributionTimestampReplay(long ageSeconds) =>
        new(
            new EncoderErrorShape(
                EncoderRuleId.DistributionTimestampReplay,
                $"Inbound request timestamp is {ageSeconds}s old — outside the 300s replay window.",
                "Sync the system clock on the worker (NTP) and retry.",
                new ReplayDetails(ageSeconds)
            ),
            401
        );

    public static EncoderRuntimeException DistributionWorkerNotRegistered(string workerId) =>
        new(
            new EncoderErrorShape(
                EncoderRuleId.DistributionWorkerNotRegistered,
                $"Worker '{workerId}' is not registered with this coordinator.",
                "Set CoordinatorUrl on the worker and let the self-registration service heartbeat once.",
                new WorkerDetails(workerId)
            ),
            404
        );

    // -- Detail records --------------------------------------------------------

    public sealed record GpuCapacityDetails(string Gpu, int Sessions);

    public sealed record EncoderInitDetails(string Handle, string Reason);

    public sealed record SourcePathDetails(string Path);

    public sealed record OutputPathDetails(string Path);

    public sealed record HardwareDetails(string Requested);

    public sealed record JobDetails(string JobId);

    public sealed record DriveDetails(string DrivePath);

    public sealed record DiscDetails(string VolumeId);

    public sealed record DiscReadDetails(string DrivePath, string FfmpegStderrTail);

    public sealed record ReplayDetails(long AgeSeconds);

    public sealed record WorkerDetails(string WorkerId);
}

/// <summary>
/// Throwable wrapper for an <see cref="EncoderErrorShape"/>.
/// Controllers (and the encoder middleware) catch this, serialise the
/// shape, and return <see cref="HttpStatusCode"/>.
/// </summary>
public sealed class EncoderRuntimeException(EncoderErrorShape shape, int httpStatusCode)
    : Exception(shape.Message)
{
    public EncoderErrorShape Shape { get; } = shape;
    public int HttpStatusCode { get; } = httpStatusCode;
}
