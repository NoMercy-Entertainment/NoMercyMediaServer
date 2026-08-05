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

[Trait("Category", "Characterization")]
public class QueueJobPayloadMaxLengthTests
{
    // The payload used to declare MaxLength(4096) and these pinned it there.
    //
    // That number was never true and never enforced: SQLite ignores a declared
    // length, and real music encode payloads ran past a megabyte — the queue
    // database reached 23.6GB carrying them. All the declaration did was tell
    // anyone sizing the table off the model a figure three orders of magnitude
    // out. It is now unbounded, which is what the column has always been, and
    // these pin that instead so a length creeping back in gets noticed.

    [Fact]
    public void QueueJob_Payload_DeclaresNoMaxLength()
    {
        PropertyInfo? prop = typeof(QueueJob).GetProperty("Payload");
        Assert.NotNull(prop);

        MaxLengthAttribute? attr = prop.GetCustomAttribute<MaxLengthAttribute>();
        Assert.Null(attr);
    }

    [Fact]
    public void QueueJob_PayloadHash_IsTheFixedWidthColumn_ThatCarriesTheIndex()
    {
        PropertyInfo? prop = typeof(QueueJob).GetProperty("PayloadHash");
        Assert.NotNull(prop);

        MaxLengthAttribute? attr = prop.GetCustomAttribute<MaxLengthAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(64, attr.Length);
    }

    [Theory]
    [InlineData([typeof(Movie), "Overview"])]
    [InlineData([typeof(Episode), "Overview"])]
    [InlineData([typeof(Tv), "Overview"])]
    [InlineData([typeof(Season), "Overview"])]
    [InlineData([typeof(Collection), "Overview"])]
    [InlineData([typeof(Similar), "Overview"])]
    [InlineData([typeof(Recommendation), "Overview"])]
    [InlineData([typeof(Special), "Overview"])]
    [InlineData([typeof(Translation), "Overview"])]
    [InlineData([typeof(Translation), "Description"])]
    [InlineData([typeof(Translation), "Biography"])]
    [InlineData([typeof(Person), "Biography"])]
    [InlineData([typeof(Network), "Description"])]
    [InlineData([typeof(Company), "Description"])]
    [InlineData([typeof(Artist), "Description"])]
    [InlineData([typeof(Album), "Description"])]
    [InlineData([typeof(ReleaseGroup), "Description"])]
    [InlineData([typeof(Playlist), "Description"])]
    public void LargeTextField_HasMaxLength4096(Type modelType, string propertyName)
    {
        PropertyInfo? prop = modelType.GetProperty(propertyName);
        Assert.NotNull(prop);
        MaxLengthAttribute? attr = prop.GetCustomAttribute<MaxLengthAttribute>();
        Assert.NotNull(attr);
        Assert.Equal(4096, attr.Length);
    }

    [Fact]
    public void QueueContext_LeavesThePayloadUnbounded_NotOnThe256Convention()
    {
        DbContextOptionsBuilder<QueueContext> optionsBuilder = new();
        optionsBuilder.UseSqlite("Data Source=:memory:");
        using QueueContext context = new(optionsBuilder.Options);
        context.Database.EnsureCreated();

        IEntityType? entityType = context.Model.FindEntityType(typeof(QueueJob));
        Assert.NotNull(entityType);

        IProperty? payloadProp = entityType.FindProperty("Payload");
        Assert.NotNull(payloadProp);
        Assert.Equal(int.MaxValue, payloadProp.GetMaxLength());
    }

    [Fact]
    public void QueueContext_QueueName_StillHas256MaxLength()
    {
        DbContextOptionsBuilder<QueueContext> optionsBuilder = new();
        optionsBuilder.UseSqlite("Data Source=:memory:");
        using QueueContext context = new(optionsBuilder.Options);
        context.Database.EnsureCreated();

        IEntityType? entityType = context.Model.FindEntityType(typeof(QueueJob));
        Assert.NotNull(entityType);

        IProperty? queueProp = entityType.FindProperty("Queue");
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
