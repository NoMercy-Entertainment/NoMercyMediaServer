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
using NoMercy.Launcher.ViewModels;
using Xunit;

namespace NoMercy.Tests.Launcher.ViewModels;

/// <summary>
/// <c>ServerControlViewModel.ExtractMessage</c> is <c>private static</c> — the
/// only caller is <c>ApplyUpdateAsync</c>'s failure branch, which in turn is
/// only reachable through a real (or fake-pipe) <c>/manage/update</c> POST.
/// Reflection reaches the real method directly to pin its contract: pull a
/// human-readable "message" field out of whatever JSON body the server's
/// error response happens to contain, without ever throwing on malformed or
/// message-less JSON.
/// </summary>
public sealed class ServerControlViewModelExtractMessageTests
{
    private static readonly MethodInfo ExtractMessageMethod =
        typeof(ServerControlViewModel).GetMethod(
            name: "ExtractMessage",
            bindingAttr: BindingFlags.NonPublic | BindingFlags.Static
        )!;

    private static string? ExtractMessage(string? json) =>
        (string?)ExtractMessageMethod.Invoke(obj: null, parameters: [json]);

    [Fact]
    public void ExtractMessage_JsonWithMessageField_ReturnsThatMessage()
    {
        string? result = ExtractMessage(json: """{"message":"disk full"}""");

        result.Should().Be(expected: "disk full");
    }

    [Fact]
    public void ExtractMessage_JsonWithoutMessageField_ReturnsNull()
    {
        string? result = ExtractMessage(json: """{"error":"disk full"}""");

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractMessage_MalformedJson_ReturnsNullInsteadOfThrowing()
    {
        string? result = ExtractMessage(json: "{ not valid json ");

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractMessage_NullInput_ReturnsNull()
    {
        string? result = ExtractMessage(json: null);

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractMessage_EmptyString_ReturnsNull()
    {
        string? result = ExtractMessage(json: string.Empty);

        result.Should().BeNull();
    }

    [Fact]
    public void ExtractMessage_NonObjectJson_ReturnsNullInsteadOfThrowing()
    {
        string? result = ExtractMessage(json: "[1,2,3]");

        result.Should().BeNull();
    }
}
