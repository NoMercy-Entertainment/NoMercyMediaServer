using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using NoMercy.Database;
using NoMercy.Database.Models;

namespace NoMercy.Tests.Database;

[Trait("Category", "Characterization")]
public class QueueJobPayloadMaxLengthTests
{
    [Fact]
    public void QueueJob_Payload_HasMaxLengthAttribute()
    {
        PropertyInfo? prop = typeof(QueueJob).GetProperty("Payload");
        Assert.NotNull(prop);
        MaxLengthAttribute? attr = prop.GetCustomAttribute<MaxLengthAttribute>();
        Assert.NotNull(attr);
    }

    [Fact]
    public void QueueJob_Payload_MaxLengthIs4096()
    {
        PropertyInfo? prop = typeof(QueueJob).GetProperty("Payload");
        Assert.NotNull(prop);
        MaxLengthAttribute? attr = prop.GetCustomAttribute<MaxLengthAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(4096, attr.Length);
    }

    [Fact]
    public void QueueJob_Payload_MaxLengthIsNotDefault256()
    {
        PropertyInfo? prop = typeof(QueueJob).GetProperty("Payload");
        Assert.NotNull(prop);
        MaxLengthAttribute? attr = prop.GetCustomAttribute<MaxLengthAttribute>();
        Assert.NotNull(attr);
        Assert.NotEqual(256, attr.Length);
    }

    [Fact]
    public void QueueJob_Payload_ExceedsDefaultConvention()
    {
        MaxLengthAttribute? queueAttr = typeof(QueueJob)
            .GetProperty("Payload")!
            .GetCustomAttribute<MaxLengthAttribute>();

        Assert.NotNull(queueAttr);
        Assert.True(queueAttr.Length > 256,
            $"QueueJob.Payload MaxLength ({queueAttr.Length}) must exceed the 256-char convention");
    }

    [Theory]
    [InlineData(typeof(Movie), "Overview")]
    [InlineData(typeof(Episode), "Overview")]
    [InlineData(typeof(Tv), "Overview")]
    [InlineData(typeof(Season), "Overview")]
    [InlineData(typeof(Collection), "Overview")]
    [InlineData(typeof(Similar), "Overview")]
    [InlineData(typeof(Recommendation), "Overview")]
    [InlineData(typeof(Special), "Overview")]
    [InlineData(typeof(Translation), "Overview")]
    [InlineData(typeof(Translation), "Description")]
    [InlineData(typeof(Translation), "Biography")]
    [InlineData(typeof(Person), "Biography")]
    [InlineData(typeof(Network), "Description")]
    [InlineData(typeof(Company), "Description")]
    [InlineData(typeof(Artist), "Description")]
    [InlineData(typeof(Album), "Description")]
    [InlineData(typeof(ReleaseGroup), "Description")]
    [InlineData(typeof(Playlist), "Description")]
    public void LargeTextField_HasMaxLength4096(Type modelType, string propertyName)
    {
        PropertyInfo? prop = modelType.GetProperty(propertyName);
        Assert.NotNull(prop);
        MaxLengthAttribute? attr = prop.GetCustomAttribute<MaxLengthAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(4096, attr.Length);
    }

    [Fact]
    public void QueueContext_ConfiguresMaxLength256_AsConvention()
    {
        DbContextOptionsBuilder<QueueContext> optionsBuilder = new();
        optionsBuilder.UseSqlite("Data Source=:memory:");
        using QueueContext context = new(optionsBuilder.Options);
        context.Database.EnsureCreated();

        Microsoft.EntityFrameworkCore.Metadata.IEntityType? entityType =
            context.Model.FindEntityType(typeof(QueueJob));
        Assert.NotNull(entityType);

        Microsoft.EntityFrameworkCore.Metadata.IProperty? payloadProp =
            entityType.FindProperty("Payload");
        Assert.NotNull(payloadProp);
        Assert.Equal(4096, payloadProp.GetMaxLength());
    }

    [Fact]
    public void QueueContext_QueueName_StillHas256MaxLength()
    {
        DbContextOptionsBuilder<QueueContext> optionsBuilder = new();
        optionsBuilder.UseSqlite("Data Source=:memory:");
        using QueueContext context = new(optionsBuilder.Options);
        context.Database.EnsureCreated();

        Microsoft.EntityFrameworkCore.Metadata.IEntityType? entityType =
            context.Model.FindEntityType(typeof(QueueJob));
        Assert.NotNull(entityType);

        Microsoft.EntityFrameworkCore.Metadata.IProperty? queueProp =
            entityType.FindProperty("Queue");
        Assert.NotNull(queueProp);
        Assert.Equal(256, queueProp.GetMaxLength());
    }

    [Fact]
    public void QueueJob_Payload_CanStoreMoreThan256Characters()
    {
        string longPayload = new('x', 1000);
        QueueJob job = new() { Payload = longPayload };
        Assert.Equal(1000, job.Payload.Length);
    }

    [Fact]
    public void QueueJob_Payload_CanStore4096Characters()
    {
        string maxPayload = new('x', 4096);
        QueueJob job = new() { Payload = maxPayload };
        Assert.Equal(4096, job.Payload.Length);
    }
}
