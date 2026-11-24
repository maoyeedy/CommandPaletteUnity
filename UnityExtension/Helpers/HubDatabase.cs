using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace UnityExtension;

internal enum HubDbStatus
{
    /// <summary>No meta table or projects_json_migrated != '1': use projects-v1.json.</summary>
    NotMigrated,

    /// <summary>Rows were read.</summary>
    Ok,

    /// <summary>The database could not be opened or queried (corrupt, locked, missing table).</summary>
    Failed,
}

/// <summary>
/// Access to Unity Hub 4's hub.db. Reads never write or lock. Writes are
/// <see cref="TryAddProjects"/> (inserts new rows without modifying existing ones),
/// <see cref="TryRemoveProject"/>, and <see cref="TryRemoveAllProjects"/>.
/// </summary>
internal static class HubDatabase
{
    static HubDatabase()
    {
        // Microsoft.Data.Sqlite finds the bundled provider through reflection, which trimming
        // can remove; registering it directly is trim-safe.
        SQLitePCL.Batteries_V2.Init();
    }

    internal static HubDbStatus TryReadProjects(
        string dbPath,
        out List<KeyValuePair<string, string>> rows
    )
    {
        rows = [];
        try
        {
            using var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder
                {
                    DataSource = ReadOnlyUri(dbPath),
                    Mode = SqliteOpenMode.ReadOnly,
                    // A pooled connection would keep hub.db open after the read.
                    Pooling = false,
                    DefaultTimeout = 2,
                }.ToString()
            );
            connection.Open();

            if (
                Scalar(
                    connection,
                    "SELECT count(*) FROM sqlite_master WHERE type = 'table' AND name = 'meta'"
                )
                    is not long metaTables
                || metaTables == 0
            )
            {
                return HubDbStatus.NotMigrated;
            }

            if (
                Scalar(connection, "SELECT value FROM meta WHERE key = 'projects_json_migrated'")
                is not "1"
            )
            {
                return HubDbStatus.NotMigrated;
            }

            var result = new List<KeyValuePair<string, string>>();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT path, data FROM projects";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (!reader.IsDBNull(0) && !reader.IsDBNull(1))
                {
                    result.Add(new(reader.GetString(0), reader.GetString(1)));
                }
            }

            rows = result;
            return HubDbStatus.Ok;
        }
        catch (Exception)
        {
            // SqliteException (corrupt, missing table, busy), IO errors, ...
            return HubDbStatus.Failed;
        }
    }

    /// <summary>
    /// Deletes the projects row whose key is exactly <paramref name="projectPath"/>, the same
    /// write `unity projects remove` makes. Writes nothing unless the db exists and is migrated.
    /// Never throws; returns true only when one row was deleted.
    /// </summary>
    internal static bool TryRemoveProject(string dbPath, string projectPath)
    {
        try
        {
            if (!File.Exists(dbPath))
            {
                return false;
            }

            using var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder
                {
                    DataSource = dbPath,
                    // ReadWrite, not ReadWriteCreate: never create a hub.db.
                    Mode = SqliteOpenMode.ReadWrite,
                    Pooling = false,
                    DefaultTimeout = 2,
                }.ToString()
            );
            connection.Open();
            using var transaction = connection.BeginTransaction();

            using var check = connection.CreateCommand();
            check.Transaction = transaction;
            check.CommandText = "SELECT value FROM meta WHERE key = 'projects_json_migrated'";
            if (check.ExecuteScalar() is not "1")
            {
                return false;
            }

            using var delete = connection.CreateCommand();
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM projects WHERE path = $path";
            delete.Parameters.AddWithValue("$path", projectPath);
            if (delete.ExecuteNonQuery() != 1)
            {
                return false;
            }

            transaction.Commit();
            return true;
        }
        catch (Exception)
        {
            // Busy (Hub mid-write), missing table, read-only file, ...
            return false;
        }
    }

    /// <summary>
    /// Deletes every <c>projects</c> row, the bulk version of <see cref="TryRemoveProject"/>.
    /// Writes nothing unless the db exists and is migrated. Never throws; returns the number of
    /// rows deleted, or 0 when nothing could be removed.
    /// </summary>
    internal static int TryRemoveAllProjects(string dbPath)
    {
        try
        {
            if (!File.Exists(dbPath))
            {
                return 0;
            }

            using var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder
                {
                    DataSource = dbPath,
                    Mode = SqliteOpenMode.ReadWrite,
                    Pooling = false,
                    DefaultTimeout = 2,
                }.ToString()
            );
            connection.Open();
            using var transaction = connection.BeginTransaction();

            using var check = connection.CreateCommand();
            check.Transaction = transaction;
            check.CommandText = "SELECT value FROM meta WHERE key = 'projects_json_migrated'";
            if (check.ExecuteScalar() is not "1")
            {
                return 0;
            }

            using var delete = connection.CreateCommand();
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM projects";
            var removed = delete.ExecuteNonQuery();
            if (removed == 0)
            {
                return 0;
            }

            transaction.Commit();
            return removed;
        }
        catch (Exception)
        {
            // Busy (Hub mid-write), missing table, read-only file, ...
            return 0;
        }
    }

    /// <summary>
    /// Inserts new <c>projects</c> rows without overwriting existing ones (<c>INSERT OR IGNORE</c>).
    /// Writes nothing unless the db exists and is migrated. Never throws; returns the number of
    /// rows inserted.
    /// </summary>
    internal static int TryAddProjects(string dbPath, IReadOnlyList<UnityProjectCandidate> projects)
    {
        if (projects.Count == 0 || !File.Exists(dbPath))
        {
            return 0;
        }

        try
        {
            using var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder
                {
                    DataSource = dbPath,
                    Mode = SqliteOpenMode.ReadWrite,
                    Pooling = false,
                    DefaultTimeout = 2,
                }.ToString()
            );
            connection.Open();
            using var transaction = connection.BeginTransaction();

            using var check = connection.CreateCommand();
            check.Transaction = transaction;
            check.CommandText = "SELECT value FROM meta WHERE key = 'projects_json_migrated'";
            if (check.ExecuteScalar() is not "1")
            {
                return 0;
            }

            var architecture =
                RuntimeInformation.OSArchitecture == Architecture.Arm64 ? "arm64" : "x86_64";
            var updatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText =
                "INSERT OR IGNORE INTO projects (path, data, updated_at) VALUES ($path, $data, $updatedAt)";
            var pathParam = insert.Parameters.Add("$path", SqliteType.Text);
            var dataParam = insert.Parameters.Add("$data", SqliteType.Text);
            var updatedAtParam = insert.Parameters.Add("$updatedAt", SqliteType.Integer);
            updatedAtParam.Value = updatedAt;

            var added = 0;
            foreach (var project in projects)
            {
                pathParam.Value = project.Path;
                dataParam.Value = BuildProjectDataJson(project, architecture);
                added += insert.ExecuteNonQuery();
            }

            transaction.Commit();
            return added;
        }
        catch (Exception)
        {
            // Busy (Hub mid-write), missing table, read-only file, ...
            return 0;
        }
    }

    private static string BuildProjectDataJson(UnityProjectCandidate project, string architecture)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            writer.WriteString("title", project.Title);
            writer.WriteString("path", project.Path);
            writer.WriteString(
                "containingFolderPath",
                Path.GetDirectoryName(project.Path) ?? string.Empty
            );
            writer.WriteString("version", project.Version);
            writer.WriteString("changeset", project.Changeset);
            writer.WriteString("architecture", architecture);
            writer.WriteBoolean("isCustomEditor", false);
            writer.WriteBoolean("isFavorite", false);
            writer.WriteNumber("lastModified", project.LastModified);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    // A plain read-only open of a WAL database still creates hub.db-wal and hub.db-shm when
    // they are missing, and writes read-marks into -shm. Without a WAL file (Hub not running)
    // the db file is complete, so immutable=1 reads it with no locks and no side files. With
    // one (Hub running), readonly_shm=1 maps the existing -shm read-only.
    internal static string ReadOnlyUri(string dbPath) =>
        new Uri(dbPath).AbsoluteUri
        + (File.Exists(dbPath + "-wal") ? "?mode=ro&readonly_shm=1" : "?immutable=1");

    private static object? Scalar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return command.ExecuteScalar();
    }
}
