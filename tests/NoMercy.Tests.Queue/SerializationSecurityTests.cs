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
using NoMercy.Tests.Queue.TestHelpers;
using NoMercyQueue;
using Xunit;

namespace NoMercy.Tests.Queue;

/// <summary>
/// <see cref="NoMercySerializationBinder"/> is the CWE-502 guard: a queue
/// payload is untrusted the moment it round-trips through the database, so
/// deserialization must refuse any type outside NoMercy's own namespaces
/// even though <c>TypeNameHandling.Objects</c> is enabled. These tests drive
/// the real binder through <see cref="SerializationHelper.Deserialize{T}"/> —
/// if the allow-list regressed to permit an arbitrary type, this goes red.
/// </summary>
[Trait(name: "Category", value: "Unit")]
public class SerializationSecurityTests
{
    [Fact]
    public void Deserialize_PayloadReferencingDisallowedNamespace_Throws()
    {
        string maliciousPayload =
            "{\"$type\":\"System.Diagnostics.Process, System.Diagnostics.Process\"}";

        Action act = () => SerializationHelper.Deserialize<object>(data: maliciousPayload);

        // Newtonsoft wraps whatever the SerializationBinder throws in its own
        // outer JsonSerializationException ("Error resolving type specified
        // in JSON...") and preserves the binder's original message as the
        // InnerException — that inner message is the actual security
        // decision this test pins.
        act.Should()
            .Throw<JsonSerializationException>()
            .WithInnerException<JsonSerializationException>()
            .WithMessage(expectedWildcardPattern: "*not allowed*");
    }

    [Fact]
    public void Deserialize_PayloadReferencingAllowedNamespace_Succeeds()
    {
        TestJob job = new() { Message = "allowed" };
        string payload = SerializationHelper.Serialize(obj: job);

        object deserialized = SerializationHelper.Deserialize<object>(data: payload);

        deserialized.Should().BeOfType<TestJob>();
    }

    [Fact]
    public void Deserialize_GenericCollectionOfDisallowedType_Throws()
    {
        // "NoMercyQueue." prefix check strips generic arguments from the root
        // type name before matching — a disallowed element type smuggled
        // inside an otherwise-allowed generic container must still be caught.
        string payload =
            "{\"$type\":\"System.Collections.Generic.List`1[[System.Diagnostics.Process, System.Diagnostics.Process]], System.Private.CoreLib\"}";

        Action act = () => SerializationHelper.Deserialize<object>(data: payload);

        act.Should().Throw<JsonSerializationException>();
    }

    [Fact]
    public void Populate_AppliesJsonFieldsOntoExistingInstance_LeavesUnlistedFieldsUntouched()
    {
        // Mirrors QueueWorker.ExecuteWithTransientRetry: a DI-constructed
        // instance (constructor-injected services already set) has the
        // serialized job DATA merged on top via Populate — the ctor-supplied
        // state must survive since it never appears in the JSON.
        TestJob rebuilt = new() { Message = "will be overwritten", ShouldFail = true };
        string data = SerializationHelper.Serialize(
            obj: new TestJob { Message = "from payload", ShouldFail = false }
        );

        SerializationHelper.Populate(data: data, target: rebuilt);

        rebuilt.Message.Should().Be(expected: "from payload");
        rebuilt.ShouldFail.Should().BeFalse();
    }
}
