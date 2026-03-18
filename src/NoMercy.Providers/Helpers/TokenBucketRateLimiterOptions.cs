namespace NoMercy.Providers.Helpers;

public enum QueueProcessingOrder
{
    OldestFirst,
    NewestFirst
}

public class TokenBucketRateLimiterOptions
{
    public int TokenLimit = 8;
    public QueueProcessingOrder QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    public int QueueLimit = 3;
    public TimeSpan ReplenishmentPeriod = TimeSpan.FromMilliseconds(1);
    public int TokensPerPeriod = 2;
    public bool AutoReplenishment = true;
}