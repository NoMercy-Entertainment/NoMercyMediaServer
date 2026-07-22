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

namespace NoMercy.Tests.Cli.Support;

/// <summary>
/// Redirects <see cref="Console.Out"/> and <see cref="Console.Error"/> for the
/// lifetime of the instance and restores the original writers on dispose, even
/// when the test throws. The CLI commands under test write their user-facing
/// results directly to the console instead of returning them, so capturing that
/// output is the only way to assert on it.
/// </summary>
internal sealed class ConsoleCapture : IDisposable
{
    private readonly TextWriter _originalOut;
    private readonly TextWriter _originalError;
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    public ConsoleCapture()
    {
        _originalOut = Console.Out;
        _originalError = Console.Error;
        Console.SetOut(newOut: _out);
        Console.SetError(newError: _error);
    }

    public string Out => _out.ToString();

    public string Error => _error.ToString();

    public void Dispose()
    {
        Console.SetOut(newOut: _originalOut);
        Console.SetError(newError: _originalError);
        _out.Dispose();
        _error.Dispose();
    }
}
