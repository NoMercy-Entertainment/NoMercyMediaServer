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

using NoMercy.Service.Hosting;

namespace NoMercy.Tests.Service.Hosting;

/// <summary>
/// <see cref="StartupAbortException"/> is the signal <see cref="PortManager"/> and
/// <see cref="ServerBootstrapper"/> throw to abort a boot that cannot recover
/// (an unresolvable port conflict). Both constructor shapes must actually carry
/// the message/inner exception through to the base <see cref="Exception"/> —
/// a mismatch here would surface as a swallowed or misleading fatal log line.
/// </summary>
[Trait("Category", "Unit")]
public class StartupAbortExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_SetsMessage()
    {
        StartupAbortException exception = new("Port 7626 is in use.");

        exception.Message.Should().Be("Port 7626 is in use.");
        exception.InnerException.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_SetsBoth()
    {
        InvalidOperationException inner = new("socket bind failed");

        StartupAbortException exception = new("Port 7626 is in use.", inner);

        exception.Message.Should().Be("Port 7626 is in use.");
        exception.InnerException.Should().BeSameAs(inner);
    }

    [Fact]
    public void StartupAbortException_IsAnException()
    {
        StartupAbortException exception = new("boom");

        exception.Should().BeAssignableTo<Exception>();
    }
}
