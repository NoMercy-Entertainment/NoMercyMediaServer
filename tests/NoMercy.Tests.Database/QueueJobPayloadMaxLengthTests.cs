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

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using NoMercy.Database;
using NoMercy.Database.Models.Common;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Movies;
using NoMercy.Database.Models.Music;
using NoMercy.Database.Models.People;
using NoMercy.Database.Models.Queue;
using NoMercy.Database.Models.TvShows;

namespace NoMercy.Tests.Database;

[Trait(name: "Category", value: "Characterization")]
public class QueueJobPayloadMaxLengthTests
{
    [Fact]
    public void QueueJob_Payload_HasMaxLengthAttribute()
    {
        PropertyInfo? prop = typeof(QueueJob).GetProperty(name: "Payload");
        Assert.NotNull(@object: prop);
        MaxLengthAttribute? attr = prop.GetCustomAttribute<MaxLengthAttribute>();
        Assert.NotNull(@object: attr);
    }

    [Fact]
    public void QueueJob_Payload_MaxLengthIs4096()
    {
        PropertyInfo? prop = typeof(QueueJob).GetProperty(name: "Payload");
        Assert.NotNull(@object: prop);
        MaxLengthAttribute? attr = prop.GetCustomAttribute<MaxLengthAttribute>();
        Assert.NotNull(@object: attr);
        Assert.Equal(expected: 4096, actual: attr.Length);
    }

    [Fact]
    public void QueueJob_Payload_MaxLengthIsNotDefault256()
    {
        PropertyInfo? prop = typeof(QueueJob).GetProperty(name: "Payload");
        Assert.NotNull(@object: prop);
        MaxLengthAttribute? attr = prop.GetCustomAttribute<MaxLengthAttribute>();
        Assert.NotNull(@object: attr);
        Assert.NotEqual(expected: 256, actual: attr.Length);
    }

    [Fact]
    public void QueueJob_Payload_ExceedsDefaultConvention()
    {
        MaxLengthAttribute? queueAttr = typeof(QueueJob)
            .GetProperty(name: "Payload")!
            .GetCustomAttribute<MaxLengthAttribute>();

        Assert.NotNull(@object: queueAttr);
        Assert.True(
            condition: queueAttr.Length > 256,
            userMessage: $"QueueJob.Payload MaxLength ({queueAttr.Length}) must exceed the 256-char convention"
        );
    }

    [Theory]
    [InlineData(data: [typeof(Movie), "Overview"])]
    [InlineData(data: [typeof(Episode), "Overview"])]
    [InlineData(data: [typeof(Tv), "Overview"])]
    [InlineData(data: [typeof(Season), "Overview"])]
    [InlineData(data: [typeof(Collection), "Overview"])]
    [InlineData(data: [typeof(Similar), "Overview"])]
    [InlineData(data: [typeof(Recommendation), "Overview"])]
    [InlineData(data: [typeof(Special), "Overview"])]
    [InlineData(data: [typeof(Translation), "Overview"])]
    [InlineData(data: [typeof(Translation), "Description"])]
    [InlineData(data: [typeof(Translation), "Biography"])]
    [InlineData(data: [typeof(Person), "Biography"])]
    [InlineData(data: [typeof(Network), "Description"])]
    [InlineData(data: [typeof(Company), "Description"])]
    [InlineData(data: [typeof(Artist), "Description"])]
    [InlineData(data: [typeof(Album), "Description"])]
    [InlineData(data: [typeof(ReleaseGroup), "Description"])]
    [InlineData(data: [typeof(Playlist), "Description"])]
    public void LargeTextField_HasMaxLength4096(Type modelType, string propertyName)
    {
        PropertyInfo? prop = modelType.GetProperty(name: propertyName);
        Assert.NotNull(@object: prop);
        MaxLengthAttribute? attr = prop.GetCustomAttribute<MaxLengthAttribute>();
        Assert.NotNull(@object: attr);
        Assert.Equal(expected: 4096, actual: attr.Length);
    }

    [Fact]
    public void QueueContext_ConfiguresMaxLength256_AsConvention()
    {
        DbContextOptionsBuilder<QueueContext> optionsBuilder = new();
        optionsBuilder.UseSqlite(connectionString: "Data Source=:memory:");
        using QueueContext context = new(options: optionsBuilder.Options);
        context.Database.EnsureCreated();

        IEntityType? entityType = context.Model.FindEntityType(type: typeof(QueueJob));
        Assert.NotNull(@object: entityType);

        IProperty? payloadProp = entityType.FindProperty(name: "Payload");
        Assert.NotNull(@object: payloadProp);
        Assert.Equal(expected: 4096, actual: payloadProp.GetMaxLength());
    }

    [Fact]
    public void QueueContext_QueueName_StillHas256MaxLength()
    {
        DbContextOptionsBuilder<QueueContext> optionsBuilder = new();
        optionsBuilder.UseSqlite(connectionString: "Data Source=:memory:");
        using QueueContext context = new(options: optionsBuilder.Options);
        context.Database.EnsureCreated();

        IEntityType? entityType = context.Model.FindEntityType(type: typeof(QueueJob));
        Assert.NotNull(@object: entityType);

        IProperty? queueProp = entityType.FindProperty(name: "Queue");
        Assert.NotNull(@object: queueProp);
        Assert.Equal(expected: 256, actual: queueProp.GetMaxLength());
    }

    [Fact]
    public void QueueJob_Payload_CanStoreMoreThan256Characters()
    {
        string longPayload = new(c: 'x', count: 1000);
        QueueJob job = new() { Payload = longPayload };
        Assert.Equal(expected: 1000, actual: job.Payload.Length);
    }

    [Fact]
    public void QueueJob_Payload_CanStore4096Characters()
    {
        string maxPayload = new(c: 'x', count: 4096);
        QueueJob job = new() { Payload = maxPayload };
        Assert.Equal(expected: 4096, actual: job.Payload.Length);
    }
}
