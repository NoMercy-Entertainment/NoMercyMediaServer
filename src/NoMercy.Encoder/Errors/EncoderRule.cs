namespace NoMercy.Encoder.Errors;

/// <summary>
/// A single validator / preview / runtime finding emitted with a stable
/// rule ID so the dashboard can deep-link to docs and surface a
/// suggested fix. Replaces the older <c>ValidationError</c> shape over
/// the Phase 1.2 sweep — both shapes coexist for one release window
/// while consumers migrate.
///
/// <para><see cref="Id"/> values are catalogued in
/// <see cref="EncoderRuleId"/> — never construct a literal string.</para>
/// </summary>
/// <param name="Id">Stable identifier from <see cref="EncoderRuleId"/>.</param>
/// <param name="Severity">Display severity. <see cref="EncoderRuleSeverity.Error"/> fails validation.</param>
/// <param name="Field">JSON-pointer-style field reference for the dashboard editor (e.g. <c>video_profiles[0].rate_control</c>).</param>
/// <param name="Message">Human-readable description of what's wrong.</param>
/// <param name="Fix">Concrete, copy-pasteable suggestion the user can act on.</param>
public sealed record EncoderRule(
    string Id,
    EncoderRuleSeverity Severity,
    string Field,
    string Message,
    string Fix
);
