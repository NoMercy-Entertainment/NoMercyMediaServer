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

using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using NoMercy.Database;
using NoMercy.Database.Models.Storage;
using NoMercy.NmSystem.SystemCalls;
using NoMercy.Storage.DriverGrouping;
using Serilog.Events;

namespace NoMercy.Service.Seeds;

/// <summary>
/// One-time boot seed that promotes the per-folder 1:1 local drivers created by
/// migration <c>MakeDriverIdRequiredAndSeedLocalDrivers</c> into shared-root
/// drivers grouped by storage endpoint (drive letter, UNC share, or POSIX mount).
///
/// Safety gate: only folders where <c>DriverId == Id</c> are eligible — that
/// pattern is the exact fingerprint left by the migration's auto-seed SQL
/// (<c>INSERT INTO Drivers … SELECT f.Id …</c>). Any folder already carrying a
/// real driver (nfs, s3, r2, webdav, or a hand-configured local) has
/// <c>DriverId != Id</c> and is provably untouched.
///
/// Idempotent: after one successful run every eligible folder has its DriverId
/// updated to the shared driver's Id (which differs from the folder's own Id), so
/// the gate matches no rows on every subsequent boot and the step is a no-op.
/// </summary>
public static class V1DriverBridgeSeed
{
    private sealed record AutoSeededFolder(string FolderIdStr, string AbsoluteRootPath);

    private sealed record LocalDriverConfig([property: JsonProperty(propertyName: "rootPath")] string RootPath);

    public static async Task RunAsync(MediaContext context)
    {
        DbConnection connection = context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        List<AutoSeededFolder> autoSeededFolders = await LoadAutoSeededFoldersRaw(connection: connection);

        if (autoSeededFolders.Count == 0)
            return;

        Logger.Setup(
            message: $"V1DriverBridge: found {autoSeededFolders.Count} auto-seeded folder(s) to regroup",
            level: LogEventLevel.Verbose
        );

        List<FolderRootInput> inputs = autoSeededFolders
            .Select(selector: f => new FolderRootInput(FolderId: Ulid.Parse(base32: f.FolderIdStr), AbsoluteRootPath: f.AbsoluteRootPath))
            .ToList();

        IReadOnlyList<DriverGroup> groups = StorageDriverGrouper.Group(inputs: inputs);

        foreach (DriverGroup group in groups)
        {
            await ApplyGroupRaw(connection: connection, group: group);
        }

        Logger.Setup(
            message: $"V1DriverBridge: regrouped {autoSeededFolders.Count} folder(s) into {groups.Count} shared driver(s)",
            level: LogEventLevel.Verbose
        );
    }

    /// <summary>
    /// Reads all self-referential (DriverId = Id) folders and their corresponding
    /// driver rootPath values using raw ADO.NET — no EF tracking, no retry strategy.
    /// </summary>
    private static async Task<List<AutoSeededFolder>> LoadAutoSeededFoldersRaw(
        DbConnection connection
    )
    {
        List<(string FolderId, string DriverId)> selfFolders = [];

        await using (DbCommand cmd = connection.CreateCommand())
        {
            cmd.CommandText = "SELECT Id, DriverId FROM Folders WHERE Id = DriverId";
            await using DbDataReader reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string folderId = reader.GetString(ordinal: 0);
                string driverId = reader.GetString(ordinal: 1);
                selfFolders.Add(item: (folderId, driverId));
            }
        }

        if (selfFolders.Count == 0)
            return [];

        List<AutoSeededFolder> result = [];

        foreach ((string folderId, string driverId) in selfFolders)
        {
            string? rootPath = await ReadDriverRootPath(connection: connection, driverId: driverId);
            if (!string.IsNullOrEmpty(value: rootPath))
                result.Add(item: new(FolderIdStr: folderId, AbsoluteRootPath: rootPath));
        }

        return result;
    }

    private static async Task<string?> ReadDriverRootPath(DbConnection connection, string driverId)
    {
        await using DbCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Config FROM Drivers WHERE Id = @id AND Type = 'local'";
        DbParameter param = cmd.CreateParameter();
        param.ParameterName = "@id";
        param.Value = driverId;
        cmd.Parameters.Add(value: param);

        object? result = await cmd.ExecuteScalarAsync();
        if (result is null or DBNull)
            return null;

        try
        {
            LocalDriverConfig? parsed = JsonConvert.DeserializeObject<LocalDriverConfig>(
                value: result.ToString()!
            );
            return parsed?.RootPath;
        }
        catch
        {
            return null;
        }
    }

    private static async Task ApplyGroupRaw(DbConnection connection, DriverGroup group)
    {
        string? existingSharedDriverId = await FindExistingSharedDriverId(
            connection: connection,
            driverRoot: group.DriverRoot
        );

        string sharedDriverIdStr;
        if (existingSharedDriverId is not null)
        {
            sharedDriverIdStr = existingSharedDriverId;
        }
        else
        {
            sharedDriverIdStr = Ulid.NewUlid().ToString();
            string config = JsonConvert.SerializeObject(value: new LocalDriverConfig(RootPath: group.DriverRoot));

            await using DbCommand insertCmd = connection.CreateCommand();
            insertCmd.CommandText =
                "INSERT OR IGNORE INTO Drivers (Id, Name, Type, Config, CreatedAt, UpdatedAt) "
                + "VALUES (@id, @name, @type, @config, @now, @now)";
            AddParam(cmd: insertCmd, name: "@id", value: sharedDriverIdStr);
            AddParam(cmd: insertCmd, name: "@name", value: group.DriverRoot);
            AddParam(cmd: insertCmd, name: "@type", value: group.DriverType);
            AddParam(cmd: insertCmd, name: "@config", value: config);
            AddParam(cmd: insertCmd, name: "@now", value: DateTimeOffset.UtcNow.ToString(format: "O"));
            await insertCmd.ExecuteNonQueryAsync();
        }

        foreach (FolderAssignment assignment in group.Folders)
        {
            string folderIdStr = assignment.FolderId.ToString();
            string subPath = assignment.SubPath.Replace(oldChar: '\\', newChar: '/');

            await using DbCommand updateCmd = connection.CreateCommand();
            updateCmd.CommandText =
                "UPDATE Folders SET DriverId = @driverId, Path = @path WHERE Id = @folderId";
            AddParam(cmd: updateCmd, name: "@driverId", value: sharedDriverIdStr);
            AddParam(cmd: updateCmd, name: "@path", value: subPath);
            AddParam(cmd: updateCmd, name: "@folderId", value: folderIdStr);
            await updateCmd.ExecuteNonQueryAsync();
        }

        foreach (FolderAssignment assignment in group.Folders)
        {
            string obsoleteIdStr = assignment.FolderId.ToString();
            if (obsoleteIdStr == sharedDriverIdStr)
                continue;

            await using DbCommand deleteCmd = connection.CreateCommand();
            deleteCmd.CommandText = "DELETE FROM Drivers WHERE Id = @id";
            AddParam(cmd: deleteCmd, name: "@id", value: obsoleteIdStr);
            await deleteCmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task<string?> FindExistingSharedDriverId(
        DbConnection connection,
        string driverRoot
    )
    {
        await using DbCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT Id, Config FROM Drivers WHERE Type = 'local'";
        await using DbDataReader reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            string driverId = reader.GetString(ordinal: 0);
            string? configJson = reader.IsDBNull(ordinal: 1) ? null : reader.GetString(ordinal: 1);

            if (string.IsNullOrEmpty(value: configJson))
                continue;

            try
            {
                LocalDriverConfig? parsed = JsonConvert.DeserializeObject<LocalDriverConfig>(
                    value: configJson
                );
                if (
                    parsed is not null
                    && string.Equals(
                        a: parsed.RootPath,
                        b: driverRoot,
                        comparisonType: StringComparison.OrdinalIgnoreCase
                    )
                    && driverId != Driver.SystemLocalDriverId.ToString()
                )
                    return driverId;
            }
            catch
            {
                // malformed config — skip
            }
        }

        return null;
    }

    private static void AddParam(DbCommand cmd, string name, string value)
    {
        DbParameter param = cmd.CreateParameter();
        param.ParameterName = name;
        param.Value = value;
        cmd.Parameters.Add(value: param);
    }
}
