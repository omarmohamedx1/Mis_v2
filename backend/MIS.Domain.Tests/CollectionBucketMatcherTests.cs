using MIS.Domain.Services;
using Xunit;

namespace MIS.Domain.Tests;

public sealed class CollectionBucketMatcherTests
{
    private static readonly CollectionBucketMatcher.BucketCandidate[] Buckets =
    [
        new(0, "CURRENT", "منتظم", "Current", 0, 0, 0),
        new(1, "1_29", "1-29", "1-29", 1, 29, 1),
        new(2, "30_59", "30-59", "30-59", 30, 59, 2),
        new(3, "90_119", "90-119", "90-119", 90, 119, 3),
        new(4, "180_PLUS", "180 فأكثر", "180+", 180, null, 4)
    ];

    [Fact]
    public void Days_past_due_takes_priority()
    {
        var bucket = CollectionBucketMatcher.Resolve(Buckets, 45, null);
        Assert.Equal("30_59", bucket!.Code);
    }

    [Fact]
    public void Exact_label_matches_bucket_name()
    {
        var bucket = CollectionBucketMatcher.Resolve(Buckets, null, "90-119");
        Assert.Equal("90_119", bucket!.Code);
    }

    [Fact]
    public void Severe_label_more_maps_to_most_delinquent_bucket()
    {
        var bucket = CollectionBucketMatcher.Resolve(Buckets, null, "BKT More");
        Assert.Equal("180_PLUS", bucket!.Code);
    }

    [Fact]
    public void Numeric_label_matches_day_range()
    {
        var bucket = CollectionBucketMatcher.Resolve(Buckets, null, "BKT 95");
        Assert.Equal("90_119", bucket!.Code);
    }

    [Fact]
    public void Unknown_label_falls_back_without_throwing()
    {
        var bucket = CollectionBucketMatcher.Resolve(Buckets, null, "???");
        Assert.NotNull(bucket);
    }

    [Fact]
    public void No_buckets_returns_null()
    {
        var bucket = CollectionBucketMatcher.Resolve([], 10, "BKT More");
        Assert.Null(bucket);
    }
}
