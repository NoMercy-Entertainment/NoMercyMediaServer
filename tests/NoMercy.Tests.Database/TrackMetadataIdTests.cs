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

using System.Reflection;
using Newtonsoft.Json;
using NoMercy.Database.Models.Media;
using NoMercy.Database.Models.Music;

namespace NoMercy.Tests.Database;

[Trait(name: "Category", value: "Characterization")]
public class TrackMetadataIdTests
{
    [Fact]
    public void MetadataId_PropertyType_IsNullableUlid()
    {
        PropertyInfo? prop = typeof(Track).GetProperty(name: "MetadataId");
        Assert.NotNull(@object: prop);
        Assert.Equal(expected: typeof(Ulid?), actual: prop.PropertyType);
    }

    [Fact]
    public void MetadataId_MatchesMetadataIdType()
    {
        Type trackMetadataIdType = typeof(Track).GetProperty(name: "MetadataId")!.PropertyType;
        Type metadataIdType = typeof(Metadata).GetProperty(name: "Id")!.PropertyType;

        Type trackFkUnderlyingType =
            Nullable.GetUnderlyingType(nullableType: trackMetadataIdType) ?? trackMetadataIdType;
        Assert.Equal(expected: metadataIdType, actual: trackFkUnderlyingType);
    }

    [Fact]
    public void MetadataId_ConsistentWithVideoFileMetadataId()
    {
        Type trackType = typeof(Track).GetProperty(name: "MetadataId")!.PropertyType;
        Type videoFileType = typeof(VideoFile).GetProperty(name: "MetadataId")!.PropertyType;
        Assert.Equal(expected: videoFileType, actual: trackType);
    }

    [Fact]
    public void MetadataId_ConsistentWithAlbumMetadataId()
    {
        Type trackType = typeof(Track).GetProperty(name: "MetadataId")!.PropertyType;
        Type albumType = typeof(Album).GetProperty(name: "MetadataId")!.PropertyType;
        Assert.Equal(expected: albumType, actual: trackType);
    }

    [Fact]
    public void MetadataId_HasCorrectJsonProperty()
    {
        PropertyInfo? prop = typeof(Track).GetProperty(name: "MetadataId");
        JsonPropertyAttribute? attr = prop?.GetCustomAttribute<JsonPropertyAttribute>();
        Assert.NotNull(@object: attr);
        Assert.Equal(expected: "metadata_id", actual: attr.PropertyName);
    }

    [Fact]
    public void MetadataId_DefaultValue_IsNull()
    {
        Track track = new();
        Assert.Null(value: track.MetadataId);
    }

    [Fact]
    public void MetadataId_CanBeAssignedUlid()
    {
        Ulid testId = Ulid.NewUlid();
        Track track = new() { MetadataId = testId };
        Assert.Equal(expected: testId, actual: track.MetadataId);
    }

    [Fact]
    public void MetadataId_CanBeAssignedNull()
    {
        Track track = new() { MetadataId = Ulid.NewUlid() };
        track.MetadataId = null;
        Assert.Null(value: track.MetadataId);
    }

    [Fact]
    public void MetadataId_SerializesToJson()
    {
        Ulid testId = Ulid.NewUlid();
        Track track = new() { MetadataId = testId };
        string json = JsonConvert.SerializeObject(value: track);
        Assert.Contains(expectedSubstring: "\"metadata_id\"", actualString: json);
        Assert.Contains(expectedSubstring: testId.ToString(), actualString: json);
    }

    [Fact]
    public void MetadataId_IsNotInt()
    {
        PropertyInfo? prop = typeof(Track).GetProperty(name: "MetadataId");
        Assert.NotNull(@object: prop);
        Assert.NotEqual(expected: typeof(int?), actual: prop.PropertyType);
        Assert.NotEqual(expected: typeof(int), actual: prop.PropertyType);
    }

    [Fact]
    public void Metadata_NavigationProperty_Exists()
    {
        PropertyInfo? prop = typeof(Track).GetProperty(name: "Metadata");
        Assert.NotNull(@object: prop);
        Assert.Equal(expected: typeof(Metadata), actual: prop.PropertyType);
    }
}
