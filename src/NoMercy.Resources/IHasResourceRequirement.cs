namespace NoMercy.Resources;

/// <summary>
/// Implemented by queue jobs that carry an explicit <see cref="ResourceRequirement"/>.
/// <see cref="QueueWorker"/> reads this before executing the job to gate dispatch
/// against the in-flight resource budget.
/// </summary>
public interface IHasResourceRequirement
{
    ResourceRequirement? ResourceRequirement { get; }
}
