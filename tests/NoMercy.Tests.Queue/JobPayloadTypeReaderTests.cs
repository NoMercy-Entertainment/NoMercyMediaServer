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
using NoMercy.Queue.MediaServer;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// <see cref="JobPayloadTypeReader"/> is a best-effort log-message helper —
/// it must never throw regardless of what the queue's Payload column
/// contains, since a malformed/legacy row would otherwise take down the
/// stuck-reservation reaper's log line. Its class doc calls out exactly the
/// contract these tests pin: read the Newtonsoft "$type" discriminator
/// without deserializing, falling back to "unknown" on anything else.
/// (Accessible here via <c>InternalsVisibleTo</c>.)
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class JobPayloadTypeReaderTests
{
    [Fact]
    public void ReadShortTypeName_ValidTypeDiscriminator_ReturnsShortName()
    {
        string payload =
            "{\"$type\":\"NoMercy.MediaProcessing.Jobs.MediaJobs.ShowExtrasJob, NoMercy.MediaProcessing\"}";

        JobPayloadTypeReader.ReadShortTypeName(payload: payload).Should().Be(expected: "ShowExtrasJob");
    }

    [Fact]
    public void ReadShortTypeName_MalformedJson_ReturnsUnknown_DoesNotThrow()
    {
        JobPayloadTypeReader.ReadShortTypeName(payload: "{not valid json").Should().Be(expected: "unknown");
    }

    [Fact]
    public void ReadShortTypeName_MissingTypeProperty_ReturnsUnknown()
    {
        JobPayloadTypeReader.ReadShortTypeName(payload: "{\"someOtherField\":1}").Should().Be(expected: "unknown");
    }

    [Fact]
    public void ReadShortTypeName_TypePropertyIsEmptyString_ReturnsUnknown()
    {
        JobPayloadTypeReader.ReadShortTypeName(payload: "{\"$type\":\"\"}").Should().Be(expected: "unknown");
    }

    [Fact]
    public void ReadShortTypeName_TypePropertyIsNotAString_ReturnsUnknown()
    {
        JobPayloadTypeReader.ReadShortTypeName(payload: "{\"$type\":42}").Should().Be(expected: "unknown");
    }

    [Fact]
    public void ReadShortTypeName_TypeNameWithoutNamespace_ReturnsAsIs()
    {
        string payload = "{\"$type\":\"BareTypeName, SomeAssembly\"}";

        JobPayloadTypeReader.ReadShortTypeName(payload: payload).Should().Be(expected: "BareTypeName");
    }
}
