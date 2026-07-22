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

using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace NoMercy.Tests.Repositories.Infrastructure;

public class SqlCaptureInterceptor : DbCommandInterceptor
{
    private readonly List<string> _capturedSql = [];

    public IReadOnlyList<string> CapturedSql => _capturedSql;

    public void Clear() => _capturedSql.Clear();

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result
    )
    {
        _capturedSql.Add(item: command.CommandText);
        return base.ReaderExecuting(command: command, eventData: eventData, result: result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default
    )
    {
        _capturedSql.Add(item: command.CommandText);
        return base.ReaderExecutingAsync(command: command, eventData: eventData, result: result, cancellationToken: cancellationToken);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result
    )
    {
        _capturedSql.Add(item: command.CommandText);
        return base.ScalarExecuting(command: command, eventData: eventData, result: result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default
    )
    {
        _capturedSql.Add(item: command.CommandText);
        return base.ScalarExecutingAsync(command: command, eventData: eventData, result: result, cancellationToken: cancellationToken);
    }

    public override InterceptionResult<int> NonQueryExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result
    )
    {
        _capturedSql.Add(item: command.CommandText);
        return base.NonQueryExecuting(command: command, eventData: eventData, result: result);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default
    )
    {
        _capturedSql.Add(item: command.CommandText);
        return base.NonQueryExecutingAsync(command: command, eventData: eventData, result: result, cancellationToken: cancellationToken);
    }
}
