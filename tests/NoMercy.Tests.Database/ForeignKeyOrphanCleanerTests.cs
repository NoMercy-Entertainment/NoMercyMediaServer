using Microsoft.Data.Sqlite;
using NoMercy.Database.Maintenance;

namespace NoMercy.Tests.Database;

public class ForeignKeyOrphanCleanerTests
{
    [Fact]
    public void Clean_RemovesRowWhoseParentIsMissing()
    {
        using SqliteConnection connection = NewDatabase();
        Execute(connection, "INSERT INTO Parent (Id) VALUES (1);");
        Execute(connection, "INSERT INTO Child (Id, ParentId) VALUES (1, 1);");
        Execute(connection, "INSERT INTO Child (Id, ParentId) VALUES (2, 999);");

        IReadOnlyDictionary<string, int> removed = ForeignKeyOrphanCleaner.Clean(
            connection,
            "Test"
        );

        Assert.Equal(1, removed["Child"]);
        Assert.Equal(0, CountViolations(connection));
        Assert.Equal(1, Scalar(connection, "SELECT COUNT(*) FROM Child;"));
        Assert.Equal(1, Scalar(connection, "SELECT Id FROM Child;"));
    }

    [Fact]
    public void Clean_CascadesThroughChainedOrphans()
    {
        using SqliteConnection connection = new("Data Source=:memory:");
        connection.Open();
        Execute(connection, "PRAGMA foreign_keys = OFF;");
        Execute(connection, "CREATE TABLE A (Id INTEGER PRIMARY KEY);");
        Execute(
            connection,
            "CREATE TABLE B (Id INTEGER PRIMARY KEY, AId INTEGER REFERENCES A(Id));"
        );
        Execute(
            connection,
            "CREATE TABLE C (Id INTEGER PRIMARY KEY, BId INTEGER REFERENCES B(Id));"
        );
        Execute(connection, "INSERT INTO B (Id, AId) VALUES (1, 999);");
        Execute(connection, "INSERT INTO C (Id, BId) VALUES (1, 1);");

        ForeignKeyOrphanCleaner.Clean(connection, "Test");

        Assert.Equal(0, CountViolations(connection));
        Assert.Equal(0, Scalar(connection, "SELECT COUNT(*) FROM B;"));
        Assert.Equal(0, Scalar(connection, "SELECT COUNT(*) FROM C;"));
    }

    [Fact]
    public void Clean_LeavesCleanDatabaseUntouched()
    {
        using SqliteConnection connection = NewDatabase();
        Execute(connection, "INSERT INTO Parent (Id) VALUES (1);");
        Execute(connection, "INSERT INTO Child (Id, ParentId) VALUES (1, 1);");

        IReadOnlyDictionary<string, int> removed = ForeignKeyOrphanCleaner.Clean(
            connection,
            "Test"
        );

        Assert.Empty(removed);
        Assert.Equal(1, Scalar(connection, "SELECT COUNT(*) FROM Child;"));
    }

    [Fact]
    public void Clean_RestoresForeignKeyEnforcement()
    {
        using SqliteConnection connection = NewDatabase();
        Execute(connection, "PRAGMA foreign_keys = ON;");

        ForeignKeyOrphanCleaner.Clean(connection, "Test");

        Assert.Equal(1, Scalar(connection, "PRAGMA foreign_keys;"));
    }

    private static SqliteConnection NewDatabase()
    {
        SqliteConnection connection = new("Data Source=:memory:");
        connection.Open();
        Execute(connection, "PRAGMA foreign_keys = OFF;");
        Execute(connection, "CREATE TABLE Parent (Id INTEGER PRIMARY KEY);");
        Execute(
            connection,
            "CREATE TABLE Child (Id INTEGER PRIMARY KEY, ParentId INTEGER REFERENCES Parent(Id));"
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
        return Convert.ToInt64(command.ExecuteScalar());
    }

    private static long CountViolations(SqliteConnection connection) =>
        Scalar(connection, "SELECT COUNT(*) FROM pragma_foreign_key_check;");
}
