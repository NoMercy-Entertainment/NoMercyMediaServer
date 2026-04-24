namespace NoMercy.Encoder.Errors;

/// <summary>
/// Runtime error envelope returned by encoder controllers when a
/// pipeline operation fails. The dashboard renders this as a toast
/// or modal — <see cref="Id"/> looks up the localised template,
/// <see cref="Suggestion"/> drives the action button, and
/// <see cref="Details"/> carries any extra structured context the
/// caller needs (e.g. the GPU device name for
/// <c>gpu_capacity_exhausted</c>).
///
/// <para>Construct via the factories in <c>RuntimeErrors</c> rather
/// than calling this constructor directly — the factories pin the
/// HTTP status code mapping that the controller middleware relies on.</para>
/// </summary>
public sealed record EncoderErrorShape(
    string Id,
    string Message,
    string? Suggestion,
    object? Details
);
