using NoMercy.NmSystem.Extensions;
using Xunit;

namespace NoMercy.Tests.Api.Media;

[Trait("Category", "Unit")]
public class AlphaBucketTests
{
    // 【Oshi No Ko】 -> TitleSort "oshi.no.ko" -> must land under "O", never "#".
    // This is the exact regression the lolomo controllers had: they bucketed on
    // the raw Title (which starts with "【") instead of the normalized TitleSort.
    [Fact]
    public void Matches_OshiNoKo_LandsUnderO_NotHash()
    {
        Assert.True(AlphaBucket.Matches("oshi.no.ko", "O"));
        Assert.False(AlphaBucket.Matches("oshi.no.ko", "#"));
    }

    [Theory]
    [InlineData("matrix", "M")]
    [InlineData("inception", "I")]
    [InlineData("zelda", "Z")]
    public void Matches_LetterBucket_IsCaseInsensitive(string titleSort, string bucket)
    {
        Assert.True(AlphaBucket.Matches(titleSort, bucket));
        Assert.True(AlphaBucket.Matches(titleSort.ToUpperInvariant(), bucket.ToLowerInvariant()));
    }

    [Theory]
    [InlineData("1408")] // starts with a digit
    [InlineData("3.body.problem")]
    [InlineData("...and.justice.for.all")] // starts with punctuation
    public void Matches_NonLetterPrefix_LandsUnderHash(string titleSort)
    {
        Assert.True(AlphaBucket.Matches(titleSort, "#"));

        foreach (string bucket in AlphaBucket.Buckets)
        {
            if (bucket == "#")
                continue;

            Assert.False(AlphaBucket.Matches(titleSort, bucket));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Matches_EmptyTitleSort_LandsUnderHashOnly(string? titleSort)
    {
        Assert.True(AlphaBucket.Matches(titleSort, "#"));
        Assert.False(AlphaBucket.Matches(titleSort, "A"));
    }

    [Fact]
    public void Matches_EveryTitleSort_LandsInExactlyOneBucket()
    {
        string[] titleSorts =
        [
            "oshi.no.ko",
            "matrix",
            "1408",
            "...and.justice.for.all",
            "zelda",
            "",
        ];

        foreach (string titleSort in titleSorts)
        {
            int hits = 0;
            foreach (string bucket in AlphaBucket.Buckets)
            {
                if (AlphaBucket.Matches(titleSort, bucket))
                    hits++;
            }

            Assert.Equal(1, hits);
        }
    }
}
