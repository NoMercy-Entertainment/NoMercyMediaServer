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

using NoMercy.Service.Seeds;
using NoMercy.Service.Seeds.Dto;

namespace NoMercy.Tests.Setup.Seeds;

/// <summary>
/// A 200 OK with an empty body, malformed JSON, or a shape mismatch used to be
/// silently treated as "authoritative zero users" (SendAndReadAsync never returns
/// null for an empty body, and JsonHelper.FromJson is a forgiving parser that
/// returns null rather than throwing on bad JSON). <see cref="ServerUserApiClient.ParseResponse"/>
/// is the exact boundary that regression sits on — these tests exercise the
/// parsing rule directly, without a live HTTP round trip.
/// </summary>
[Trait("Category", "Unit")]
public class ServerUserApiClientTests
{
    [Fact]
    public void ParseResponse_EmptyBody_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => ServerUserApiClient.ParseResponse(""));
    }

    [Fact]
    public void ParseResponse_WhitespaceOnlyBody_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => ServerUserApiClient.ParseResponse("   "));
    }

    [Fact]
    public void ParseResponse_MalformedJson_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ServerUserApiClient.ParseResponse("{not valid json")
        );
    }

    [Fact]
    public void ParseResponse_ValidJsonWithData_ReturnsUsers()
    {
        string json =
            "{\"data\":[{\"user_id\":\"11111111-1111-1111-1111-111111111111\",\"name\":\"Owner\",\"email\":\"owner@example.com\",\"enabled\":true,\"is_owner\":true}]}";

        ServerUserDtoData[] result = ServerUserApiClient.ParseResponse(json);

        Assert.Single(result);
        Assert.Equal("11111111-1111-1111-1111-111111111111", result[0].UserId);
        Assert.True(result[0].IsOwner);
    }

    [Fact]
    public void ParseResponse_ValidJsonMissingDataKey_ReturnsEmptyArray_DoesNotThrow()
    {
        // A well-formed but shape-mismatched body ("{}" instead of {"data":[...]})
        // parses successfully into a ServerUserDto with its default empty Data —
        // this is NOT a parse failure, so it must not throw here. It is instead
        // the exact case ServerUserSyncService's self-floor check must catch.
        ServerUserDtoData[] result = ServerUserApiClient.ParseResponse("{}");

        Assert.Empty(result);
    }
}
