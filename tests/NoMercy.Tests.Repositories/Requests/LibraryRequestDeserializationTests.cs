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

using FluentAssertions;
using Newtonsoft.Json;
using NoMercy.Data.Requests;

namespace NoMercy.Tests.Repositories;

/// <summary>
/// Regression coverage (BUG-1 / BUG-2): the library request DTOs are bound with
/// Newtonsoft <c>[JsonProperty]</c> keys that do not match the C# property names
/// (snake_case "folder_library", camelCase flags). A drift in those keys — or an
/// accidental switch to System.Text.Json (default camelCase) — would silently
/// drop fields. These tests pin the on-the-wire contract.
/// </summary>
[Trait("Category", "Regression")]
public class LibraryRequestDeserializationTests
{
    [Fact]
    public void LibraryUpdateRequest_maps_every_json_key_to_its_property()
    {
        const string json =
            "{\"title\":\"Movies\",\"image\":\"/poster.jpg\",\"perfectSubtitleMatch\":true,"
            + "\"realtime\":false,\"specialSeasonName\":\"Specials\",\"type\":\"movie\","
            + "\"folder_library\":[],\"subtitles\":[\"en\",\"nl\"]}";

        LibraryUpdateRequest? request = JsonConvert.DeserializeObject<LibraryUpdateRequest>(json);

        request.Should().NotBeNull();
        request!.Title.Should().Be("Movies");
        request.Image.Should().Be("/poster.jpg");
        request.PerfectSubtitleMatch.Should().BeTrue();
        request.Realtime.Should().BeFalse();
        request.SpecialSeasonName.Should().Be("Specials");
        request.Type.Should().Be("movie");
        request.Subtitles.Should().Equal("en", "nl");
        request.FolderLibrary.Should().NotBeNull();
    }

    [Fact]
    public void LibraryUpdateRequest_snake_case_folder_library_key_is_honoured()
    {
        const string json = "{\"folder_library\":[{}]}";

        LibraryUpdateRequest? request = JsonConvert.DeserializeObject<LibraryUpdateRequest>(json);

        request.Should().NotBeNull();
        request!.FolderLibrary.Should().NotBeNull();
        request.FolderLibrary!.Length.Should().Be(1);
    }

    [Fact]
    public void RescanLibraryRequest_maps_force_and_synchronous_flags()
    {
        const string json = "{\"forceUpdate\":true,\"synchronous\":true}";

        RescanLibraryRequest? request = JsonConvert.DeserializeObject<RescanLibraryRequest>(json);

        request.Should().NotBeNull();
        request!.ForceUpdate.Should().BeTrue();
        request.Synchronous.Should().BeTrue();
    }
}
