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

using NoMercy.Setup.Ui;

namespace NoMercy.Tests.Setup.Ui;

/// <summary>
/// Requirement: the startup banner methods must never throw regardless of whether
/// stdout is redirected — <see cref="ConsoleMessages.ServerRunning"/> and
/// <see cref="ConsoleMessages.Logo"/> render the fancy interactive banner only on a
/// real console and no-op under redirection (piped logs, a service manager capturing
/// output); <see cref="ConsoleMessages.Welcome"/> is the deliberate inverse — a
/// plain-text fallback banner that only renders WHEN output is redirected.
/// </summary>
/// <remarks>
/// Because <c>ServerRunning</c>/<c>Logo</c> and <c>Welcome</c> gate on opposite
/// polarities of the same <c>Console.IsOutputRedirected</c> check, calling all three
/// against this test host's actual (uncontrollable) console state guarantees at least
/// one real early-return branch and at least one real body-execution branch are
/// exercised, whichever way that ambient state falls — see
/// <c>SetupTerminalUiTests</c> for the equivalent, environment-dependent situation with
/// <c>SetupTerminalUi</c>.
/// </remarks>
[Trait(name: "Category", value: "Unit")]
public class ConsoleMessagesTests
{
    [Fact]
    public async Task ServerRunning_DoesNotThrow()
    {
        await ConsoleMessages.ServerRunning();
    }

    [Fact]
    public async Task Welcome_DoesNotThrow()
    {
        await ConsoleMessages.Welcome();
    }

    [Fact]
    public void Logo_DoesNotThrow()
    {
        ConsoleMessages.Logo();
    }

    [Fact]
    public async Task ServerRunning_ReturnsCompletedTask()
    {
        Task task = ConsoleMessages.ServerRunning();

        await task;
        Assert.True(condition: task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task Welcome_ReturnsCompletedTask()
    {
        Task task = ConsoleMessages.Welcome();

        await task;
        Assert.True(condition: task.IsCompletedSuccessfully);
    }
}

// NOTE ON RESIDUAL COVERAGE: ConsoleMessages.Logo()'s letter-by-letter rendering body
// picks between ConsoleLetters.Colossal and ConsoleLetters.ColossalXmas based on
// IsXmasTime() (real DateTime.Today, no injectable clock) — only the branch matching
// today's actual calendar date is reachable in a single run. Both letter tables are
// independently and fully locked by ConsoleLettersTests regardless of which one Logo()
// happens to pick, so this is a "which table" selection gap, not an untested-data gap.
