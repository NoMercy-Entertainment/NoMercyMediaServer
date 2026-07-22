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
            Id: EncoderRuleId.GpuCapacityExhausted,
            Message: $"All NVENC sessions on '{gpu}' are in use ({sessions} active). New encode held until a slot frees.",
            Suggestion: suggestion
                        ?? "Set hardware_preference=force_software for this profile to encode on CPU instead, or wait for a slot to free.",
            Details: new GpuCapacityDetails(Gpu: gpu, Sessions: sessions)
        );
        return new(shape: shape, httpStatusCode: 409);
    }

    public static EncoderRuntimeException EncoderInitFailed(string handle, string reason) =>
        new(
            shape: new(
                Id: EncoderRuleId.EncoderInitFailed,
                Message: $"Encoder '{handle}' failed to initialise: {reason}",
                Suggestion: "Check the ffmpeg capability probe at /api/v1/encoder/capabilities — the encoder may be missing from this build.",
                Details: new EncoderInitDetails(Handle: handle, Reason: reason)
            ),
            httpStatusCode: 500
        );

    public static EncoderRuntimeException SourceNotAccessible(string path) =>
        new(
            shape: new(
                Id: EncoderRuleId.SourceNotAccessible,
                Message: $"Source file is not accessible: {path}",
                Suggestion: "Verify the file exists and the encoder process has read permission. For network shares, check the mount is active.",
                Details: new SourcePathDetails(Path: path)
            ),
            httpStatusCode: 404
        );

    public static EncoderRuntimeException SourceReadError(string path, string detail) =>
        new(
            shape: new(
                Id: EncoderRuleId.SourceReadError,
                Message: $"Source read failed: {path} — {detail}",
                Suggestion: "If this is a network share, verify connectivity. For local files, run a filesystem check.",
                Details: new SourcePathDetails(Path: path)
            ),
            httpStatusCode: 500
        );

    public static EncoderRuntimeException OutputWriteError(string path, string detail) =>
        new(
            shape: new(
                Id: EncoderRuleId.OutputWriteError,
                Message: $"Output write failed: {path} — {detail}",
                Suggestion: "Check disk space and that the output directory has write permission for the encoder process.",
                Details: new OutputPathDetails(Path: path)
            ),
            httpStatusCode: 500
        );

    public static EncoderRuntimeException OutputPathNotAllowed(string path, string reason) =>
        new(
            shape: new(
                Id: EncoderRuleId.OutputPathNotAllowed,
                Message: $"Output path rejected: {reason} (path={path})",
                Suggestion: "Add the parent directory to EncoderOptions.Storage.AllowedRoots, or write the output to an existing allowed root.",
                Details: new OutputPathDetails(Path: path)
            ),
            httpStatusCode: 403
        );

    /// <summary>
    /// The cluster license server returned 403 — the worker is no
    /// longer entitled to participate in distributed encoding. Free-tier
    /// installs land here on the first heartbeat after the entitlement
    /// is revoked.
    /// </summary>
    public static EncoderRuntimeException LicenseRevoked(string reason) =>
        new(
            shape: new(
                Id: EncoderRuleId.LicenseRevoked,
                Message: $"Cluster license revoked: {reason}",
                Suggestion: "Re-link the server to your NoMercy account at https://nomercy.tv/dashboard/devices, or downgrade to standalone mode.",
                Details: null
            ),
            httpStatusCode: 403
        );

    public static EncoderRuntimeException LicenseUnreachable(string url) =>
        new(
            shape: new(
                Id: EncoderRuleId.LicenseUnreachable,
                Message: $"Cluster license server is unreachable at {url}.",
                Suggestion: "Check this server's outbound connectivity to api.nomercy.tv. Distributed encoding will resume automatically when the server is reachable again.",
                Details: null
            ),
            httpStatusCode: 503
        );

    public static EncoderRuntimeException HardwareForcedButUnavailable(string requested) =>
        new(
            shape: new(
                Id: EncoderRuleId.HardwareForcedButUnavailable,
                Message: $"hardware_preference=force_hardware was set but no compatible hardware encoder is available for '{requested}'.",
                Suggestion: "Switch hardware_preference to prefer_hardware (auto-fallback to software), or install the missing GPU drivers and run /api/v1/encoder/hardware/benchmark.",
                Details: new HardwareDetails(Requested: requested)
            ),
            httpStatusCode: 422
        );

    public static EncoderRuntimeException JobInterruptedNoCheckpoint(string jobId) =>
        new(
            shape: new(
                Id: EncoderRuleId.JobInterruptedNoCheckpoint,
                Message: $"Job {jobId} was interrupted by shutdown and has no checkpoint to resume from.",
                Suggestion: "Re-dispatch the job — it will start from the beginning.",
                Details: new JobDetails(JobId: jobId)
            ),
            httpStatusCode: 500
        );

    public static EncoderRuntimeException DiscDriveBusy(string drivePath) =>
        new(
            shape: new(
                Id: EncoderRuleId.DiscDriveBusy,
                Message: $"Drive {drivePath} is already busy with an active rip.",
                Suggestion: "Wait for the active rip to finish, or insert the disc in a different drive.",
                Details: new DriveDetails(DrivePath: drivePath)
            ),
            httpStatusCode: 409
        );

    public static EncoderRuntimeException DiscAacsCertMissing(string volumeId) =>
        new(
            shape: new(
                Id: EncoderRuleId.DiscAacsCertMissing,
                Message: $"AACS: no matching certificate for volume {volumeId}.",
                Suggestion: "Add the volume key to KEYDB.cfg or point EncoderOptions.BluRay.KeyDbOverridePath at your own KEYDB.",
                Details: new DiscDetails(VolumeId: volumeId)
            ),
            httpStatusCode: 409
        );

    public static EncoderRuntimeException DiscBdplusConverterMissing(string volumeId) =>
        new(
            shape: new(
                Id: EncoderRuleId.DiscBdplusConverterMissing,
                Message: $"BD+: no matching converter for volume {volumeId}.",
                Suggestion: "Update libbdplus / the BDSVM converter database, or rip the disc on a system that has it.",
                Details: new DiscDetails(VolumeId: volumeId)
            ),
            httpStatusCode: 409
        );

    public static EncoderRuntimeException DiscReadError(
        string drivePath,
        string ffmpegStderrTail
    ) =>
        new(
            shape: new(
                Id: EncoderRuleId.DiscReadError,
                Message: $"Disc read failed on {drivePath}.",
                Suggestion: "Clean the disc surface and try again. If it persists, the disc may be physically damaged.",
                Details: new DiscReadDetails(DrivePath: drivePath, FfmpegStderrTail: ffmpegStderrTail)
            ),
            httpStatusCode: 500
        );

    public static EncoderRuntimeException DistributionHmacInvalid() =>
        new(
            shape: new(
                Id: EncoderRuleId.DistributionHmacInvalid,
                Message: "HMAC signature on the inbound request did not verify.",
                Suggestion: "Confirm both coordinator and worker share the same DistributedEncodingSigningKey (or that the worker's cluster token is fresh).",
                Details: null
            ),
            httpStatusCode: 401
        );

    public static EncoderRuntimeException DistributionTimestampReplay(long ageSeconds) =>
        new(
            shape: new(
                Id: EncoderRuleId.DistributionTimestampReplay,
                Message: $"Inbound request timestamp is {ageSeconds}s old — outside the 300s replay window.",
                Suggestion: "Sync the system clock on the worker (NTP) and retry.",
                Details: new ReplayDetails(AgeSeconds: ageSeconds)
            ),
            httpStatusCode: 401
        );

    public static EncoderRuntimeException DistributionWorkerNotRegistered(string workerId) =>
        new(
            shape: new(
                Id: EncoderRuleId.DistributionWorkerNotRegistered,
                Message: $"Worker '{workerId}' is not registered with this coordinator.",
                Suggestion: "Set CoordinatorUrl on the worker and let the self-registration service heartbeat once.",
                Details: new WorkerDetails(WorkerId: workerId)
            ),
            httpStatusCode: 404
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
    : Exception(message: shape.Message)
{
    public EncoderErrorShape Shape { get; } = shape;
    public int HttpStatusCode { get; } = httpStatusCode;
}
