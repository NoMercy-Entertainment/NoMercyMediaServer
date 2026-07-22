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

using Microsoft.Data.Sqlite;
using NoMercy.Database.Maintenance;

namespace NoMercy.Tests.Database;

public class ForeignKeyOrphanCleanerTests
{
    [Fact]
    public void Clean_RemovesRowWhoseParentIsMissing()
    {
        using SqliteConnection connection = NewDatabase();
        Execute(connection: connection, sql: "INSERT INTO Parent (Id) VALUES (1);");
        Execute(connection: connection, sql: "INSERT INTO Child (Id, ParentId) VALUES (1, 1);");
        Execute(connection: connection, sql: "INSERT INTO Child (Id, ParentId) VALUES (2, 999);");

        IReadOnlyDictionary<string, int> removed = ForeignKeyOrphanCleaner.Clean(
            connection: connection,
            contextName: "Test"
        );

        Assert.Equal(expected: 1, actual: removed[key: "Child"]);
        Assert.Equal(expected: 0, actual: CountViolations(connection: connection));
        Assert.Equal(expected: 1, actual: Scalar(connection: connection, sql: "SELECT COUNT(*) FROM Child;"));
        Assert.Equal(expected: 1, actual: Scalar(connection: connection, sql: "SELECT Id FROM Child;"));
    }

    [Fact]
    public void Clean_CascadesThroughChainedOrphans()
    {
        using SqliteConnection connection = new(connectionString: "Data Source=:memory:");
        connection.Open();
        Execute(connection: connection, sql: "PRAGMA foreign_keys = OFF;");
        Execute(connection: connection, sql: "CREATE TABLE A (Id INTEGER PRIMARY KEY);");
        Execute(
            connection: connection,
            sql: "CREATE TABLE B (Id INTEGER PRIMARY KEY, AId INTEGER REFERENCES A(Id));"
        );
        Execute(
            connection: connection,
            sql: "CREATE TABLE C (Id INTEGER PRIMARY KEY, BId INTEGER REFERENCES B(Id));"
        );
        Execute(connection: connection, sql: "INSERT INTO B (Id, AId) VALUES (1, 999);");
        Execute(connection: connection, sql: "INSERT INTO C (Id, BId) VALUES (1, 1);");

        ForeignKeyOrphanCleaner.Clean(connection: connection, contextName: "Test");

        Assert.Equal(expected: 0, actual: CountViolations(connection: connection));
        Assert.Equal(expected: 0, actual: Scalar(connection: connection, sql: "SELECT COUNT(*) FROM B;"));
        Assert.Equal(expected: 0, actual: Scalar(connection: connection, sql: "SELECT COUNT(*) FROM C;"));
    }

    [Fact]
    public void Clean_LeavesCleanDatabaseUntouched()
    {
        using SqliteConnection connection = NewDatabase();
        Execute(connection: connection, sql: "INSERT INTO Parent (Id) VALUES (1);");
        Execute(connection: connection, sql: "INSERT INTO Child (Id, ParentId) VALUES (1, 1);");

        IReadOnlyDictionary<string, int> removed = ForeignKeyOrphanCleaner.Clean(
            connection: connection,
            contextName: "Test"
        );

        Assert.Empty(collection: removed);
        Assert.Equal(expected: 1, actual: Scalar(connection: connection, sql: "SELECT COUNT(*) FROM Child;"));
    }

    [Fact]
    public void Clean_RestoresForeignKeyEnforcement()
    {
        using SqliteConnection connection = NewDatabase();
        Execute(connection: connection, sql: "PRAGMA foreign_keys = ON;");

        ForeignKeyOrphanCleaner.Clean(connection: connection, contextName: "Test");

        Assert.Equal(expected: 1, actual: Scalar(connection: connection, sql: "PRAGMA foreign_keys;"));
    }

    private static SqliteConnection NewDatabase()
    {
        SqliteConnection connection = new(connectionString: "Data Source=:memory:");
        connection.Open();
        Execute(connection: connection, sql: "PRAGMA foreign_keys = OFF;");
        Execute(connection: connection, sql: "CREATE TABLE Parent (Id INTEGER PRIMARY KEY);");
        Execute(
            connection: connection,
            sql: "CREATE TABLE Child (Id INTEGER PRIMARY KEY, ParentId INTEGER REFERENCES Parent(Id));"
        );
        return connection;
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static long Scalar(SqliteConnection connection, string sql)
    {
        using SqliteCommand command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(value: command.ExecuteScalar());
    }

    private static long CountViolations(SqliteConnection connection) =>
        Scalar(connection: connection, sql: "SELECT COUNT(*) FROM pragma_foreign_key_check;");
}
