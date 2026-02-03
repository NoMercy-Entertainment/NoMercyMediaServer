using Microsoft.Extensions.DependencyInjection;

namespace NoMercy.EncoderV2.Shared.Resilience;

/// <summary>
/// Retry policy configuration for resilient encoder operations
/// </summary>
public static class RetryPolicies
{
    public const int DefaultMaxRetries = 3;
    public const int DefaultRetryDelayMs = 1000;
    public const int DefaultTimeoutMs = 300000;

    public static RetryPolicyConfig FFmpegRetryPolicy => new()
    {
        MaxRetries = 2,
        InitialDelayMs = 2000,
        MaxDelayMs = 10000,
        BackoffMultiplier = 2.0,
        TimeoutMs = 600000
    };

    public static RetryPolicyConfig FileOperationRetryPolicy => new()
    {
        MaxRetries = 5,
        InitialDelayMs = 500,
        MaxDelayMs = 5000,
        BackoffMultiplier = 1.5,
        TimeoutMs = 30000
    };

    public static RetryPolicyConfig NetworkRetryPolicy => new()
    {
        MaxRetries = 3,
        InitialDelayMs = 1000,
        MaxDelayMs = 8000,
        BackoffMultiplier = 2.0,
        TimeoutMs = 60000
    };

    public static async Task<T> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        RetryPolicyConfig? config = null,
        Func<Exception, bool>? shouldRetry = null)
    {
        RetryPolicyConfig policy = config ?? new RetryPolicyConfig();
        int attempt = 0;
        int delayMs = policy.InitialDelayMs;

        while (true)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                attempt++;

                bool canRetry = attempt < policy.MaxRetries;
                bool shouldRetryException = shouldRetry == null || shouldRetry(ex);

                if (!canRetry || !shouldRetryException)
                {
                    throw;
                }

                Console.WriteLine($"Operation failed (attempt {attempt}/{policy.MaxRetries}): {ex.Message}. Retrying in {delayMs}ms...");

                await Task.Delay(delayMs);

                delayMs = (int)Math.Min(delayMs * policy.BackoffMultiplier, policy.MaxDelayMs);
            }
        }
    }

    public static async Task ExecuteWithRetryAsync(
        Func<Task> operation,
        RetryPolicyConfig? config = null,
        Func<Exception, bool>? shouldRetry = null)
    {
        await ExecuteWithRetryAsync(async () =>
        {
            await operation();
            return true;
        }, config, shouldRetry);
    }
}

public class RetryPolicyConfig
{
    public int MaxRetries { get; set; } = RetryPolicies.DefaultMaxRetries;
    public int InitialDelayMs { get; set; } = RetryPolicies.DefaultRetryDelayMs;
    public int MaxDelayMs { get; set; } = 30000;
    public double BackoffMultiplier { get; set; } = 2.0;
    public int TimeoutMs { get; set; } = RetryPolicies.DefaultTimeoutMs;
}

