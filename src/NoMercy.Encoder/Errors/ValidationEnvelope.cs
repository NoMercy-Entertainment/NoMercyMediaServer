namespace NoMercy.Encoder.Errors;

/// <summary>
/// Spec envelope for every validator / preview / plan / live /
/// distribution endpoint. <see cref="Valid"/> is true only when no rule
/// of severity <see cref="EncoderRuleSeverity.Error"/> is present.
/// </summary>
public sealed record ValidationEnvelope(
    bool Valid,
    IReadOnlyList<EncoderRule> Errors,
    IReadOnlyList<EncoderRule> Warnings
)
{
    /// <summary>Empty success envelope — no rules, no errors, no warnings.</summary>
    public static ValidationEnvelope Ok() => new(true, [], []);

    /// <summary>
    /// Bucket a flat list of rules into errors + warnings + info,
    /// computing <see cref="Valid"/> from the absence of error-severity
    /// rules. Info-severity rules are merged into the warnings bucket
    /// for the dashboard's purposes (single non-blocking chip type).
    /// </summary>
    public static ValidationEnvelope FromRules(IEnumerable<EncoderRule> rules)
    {
        List<EncoderRule> errors = [];
        List<EncoderRule> warnings = [];

        foreach (EncoderRule rule in rules)
        {
            if (rule.Severity == EncoderRuleSeverity.Error)
                errors.Add(rule);
            else
                warnings.Add(rule);
        }

        return new(errors.Count == 0, errors, warnings);
    }
}
