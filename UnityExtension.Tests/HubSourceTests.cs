using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using Xunit;

namespace UnityExtension.Tests;

public sealed class HubSourceTests : IDisposable
{
    private const string Corrupt = "<corrupt>";

    private const string Schema = """
        PRAGMA journal_mode = WAL;
        CREATE TABLE meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);
        CREATE TABLE projects (path TEXT PRIMARY KEY, data TEXT NOT NULL, updated_at INTEGER NOT NULL);
        """;

    private const string MigratedDb =
        Schema
        + """
            INSERT INTO meta VALUES ('projects_json_migrated', '1');
            INSERT INTO projects VALUES ('C:\p\DbOnly', '{"title":"DbOnly","version":"6000.0.1f1","lastModified":1}', 1);
            """;

    private const string JsonAB = """
        {"schema_version":"v1","data":{
          "C:\\p\\A": {"title":"A","version":"2022.3.1f1","lastModified":2},
          "C:\\p\\B": {"title":"B","version":"2022.3.1f1","lastModified":3}
        }}
        """;

    // Non-ASCII path, title and tag, written as escapes to keep the file ASCII.
    private const string Cafe = "Caf\u00e9";
    private const string JapaneseTag = "\u30c6\u30b9\u30c8";

    // Space and non-ASCII in the folder name exercise the file: URI encoding.
    private readonly string _root = Directory.CreateTempSubdirectory("hubtest").FullName;
    private readonly string _hubDir;

    public HubSourceTests()
    {
        _hubDir = Directory.CreateDirectory(Path.Combine(_root, "Unity Hub " + Cafe)).FullName;
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    public static TheoryData<string, string?, string?, string?, bool, string[]> Sources =>
        new()
        {
            { "migrated db is the source", MigratedDb, JsonAB, null, false, ["DbOnly"] },
            {
                "no meta table -> json",
                "CREATE TABLE projects (path TEXT PRIMARY KEY, data TEXT NOT NULL, updated_at INTEGER NOT NULL);",
                JsonAB,
                null,
                false,
                ["A", "B"]
            },
            { "meta table without rows -> json", Schema, JsonAB, null, false, ["A", "B"] },
            {
                "meta without flag -> json",
                Schema + "INSERT INTO meta VALUES ('other', '1');",
                JsonAB,
                null,
                false,
                ["A", "B"]
            },
            {
                "flag not '1' -> json",
                Schema + "INSERT INTO meta VALUES ('projects_json_migrated', '0');",
                JsonAB,
                null,
                false,
                ["A", "B"]
            },
            {
                "json newer than db (downgrade) -> json",
                MigratedDb,
                JsonAB,
                null,
                true,
                ["A", "B"]
            },
            { "no db -> json", null, JsonAB, null, false, ["A", "B"] },
            {
                "excluded paths filtered, any case or slash",
                null,
                JsonAB,
                """{"schema_version":"1","data":["c:/P/b/"]}""",
                false,
                ["A"]
            },
            {
                "excluded matches a forward-slash json key",
                null,
                """{"data":{"C:/p/A/":{"title":"A"},"C:/p/B":{"title":"B"}}}""",
                """{"schema_version":"1","data":["C:\\p\\a"]}""",
                false,
                ["B"]
            },
            { "corrupt excluded file ignored", null, JsonAB, "not json", false, ["A", "B"] },
            { "corrupt db -> empty", Corrupt, JsonAB, null, false, [] },
            {
                "migrated db without projects table -> empty",
                "CREATE TABLE meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);"
                    + "INSERT INTO meta VALUES ('projects_json_migrated', '1');",
                JsonAB,
                null,
                false,
                []
            },
            {
                "malformed db row skipped",
                MigratedDb + "INSERT INTO projects VALUES ('C:\\p\\Bad', 'not json', 1);",
                null,
                null,
                false,
                ["DbOnly"]
            },
            { "nothing -> empty", null, null, null, false, [] },
        };

    [Theory]
    [MemberData(nameof(Sources))]
    public void Source_selection(
        string name,
        string? dbSql,
        string? projectsJson,
        string? excludedJson,
        bool jsonNewer,
        string[] expectedTitles
    )
    {
        _ = name;
        Setup(dbSql, projectsJson, excludedJson, jsonNewer);

        var titles = ProjectParser.LoadProjects(_hubDir).Select(p => p.Title).Order();

        Assert.Equal(expectedTitles, titles);
    }

    [Fact]
    public void Db_row_fields_including_tags_and_null_last_modified()
    {
        Setup(
            Schema
                + $$"""
                INSERT INTO meta VALUES ('projects_json_migrated', '1');
                INSERT INTO projects VALUES ('C:\p\{{Cafe}}', '{"title":"{{Cafe}}","path":"C:\\p\\{{Cafe}}","version":"6000.2.0f1","changeset":"7f159b6136da","lastModified":null,"isFavorite":true,"tags":["ui","{{JapaneseTag}}"]}', 1);
                INSERT INTO projects VALUES ('C:\p\Plain', '{"title":"Plain","version":"2022.3.1f1","lastModified":42,"isFavorite":false}', 1);
                """,
            null,
            null,
            false
        );

        var projects = ProjectParser.LoadProjects(_hubDir);

        var tagged = projects.Single(p => p.Title == Cafe);
        Assert.Equal("C:\\p\\" + Cafe, tagged.Path);
        Assert.Equal("6000.2.0f1", tagged.Version);
        Assert.Equal("7f159b6136da", tagged.Changeset);
        Assert.Equal(0, tagged.LastModified);
        Assert.True(tagged.IsFavorite);
        Assert.Equal(["ui", JapaneseTag], tagged.Tags);

        var plain = projects.Single(p => p.Title == "Plain");
        Assert.Equal("", plain.Changeset);
        Assert.Equal(42, plain.LastModified);
        Assert.False(plain.IsFavorite);
        Assert.Empty(plain.Tags);
    }

    [Fact]
    public void Reading_the_db_writes_nothing_in_the_hub_folder()
    {
        Setup(MigratedDb, JsonAB, null, false);
        var before = Snapshot();

        Assert.NotEmpty(ProjectParser.LoadProjects(_hubDir));

        Assert.Equal(before, Snapshot());
    }

    [Fact]
    public void Running_hub_wal_rows_are_read_without_writing()
    {
        var dbPath = Path.Combine(_hubDir, "hub.db");

        // Another process holds the db open and never checkpoints, like a running Hub: the
        // second row lives only in hub.db-wal. It must be another process because SQLite
        // shares one -shm mapping per process.
        using var writer = ExternalWriter.Start(
            _root,
            dbPath,
            MigratedDb
                + "PRAGMA wal_autocheckpoint = 0;"
                + """INSERT INTO projects VALUES ('C:\\p\\InWal', '{"title":"InWal"}', 2);"""
        );
        Assert.True(new FileInfo(dbPath + "-wal").Length > 0);
        var before = Snapshot();

        var titles = ProjectParser.LoadProjects(_hubDir).Select(p => p.Title).Order();

        Assert.Equal(["DbOnly", "InWal"], titles);
        Assert.Equal(before, Snapshot());
    }

    // Remove mirrors `unity projects remove`: deletes one hub.db row, writes nothing else.
    // Expected rows null = no hub.db file may exist afterwards.
    public static TheoryData<string, string?, string, bool, string[]?> Removals =>
        new()
        {
            {
                "migrated db: only the named row is deleted",
                MigratedDb
                    + """INSERT INTO projects VALUES ('C:\p\Keep', '{"title":"Keep"}', 1);""",
                @"C:\p\DbOnly",
                true,
                [@"C:\p\Keep"]
            },
            { "unknown path: nothing deleted", MigratedDb, @"C:\p\Other", false, [@"C:\p\DbOnly"] },
            { "key match is exact (case)", MigratedDb, @"c:\P\dbonly", false, [@"C:\p\DbOnly"] },
            {
                "not migrated: no write",
                Schema + """INSERT INTO projects VALUES ('C:\p\DbOnly', '{}', 1);""",
                @"C:\p\DbOnly",
                false,
                [@"C:\p\DbOnly"]
            },
            { "no db: none is created", null, @"C:\p\DbOnly", false, null },
        };

    [Theory]
    [MemberData(nameof(Removals))]
    public void Remove_from_list(
        string name,
        string? dbSql,
        string path,
        bool expectedRemoved,
        string[]? expectedRows
    )
    {
        _ = name;
        Setup(dbSql, JsonAB, """{"schema_version":"1","data":[]}""", false);
        var dbPath = Path.Combine(_hubDir, "hub.db");
        var otherFiles = Snapshot()
            .Where(f => !f.StartsWith("hub.db", StringComparison.Ordinal))
            .ToArray();

        var removed = HubDatabase.TryRemoveProject(dbPath, path);

        Assert.Equal(expectedRemoved, removed);
        if (expectedRows is null)
        {
            Assert.False(File.Exists(dbPath));
        }
        else
        {
            Assert.Equal(expectedRows, TestSqlite.Paths(dbPath));
        }

        Assert.Equal(
            otherFiles,
            Snapshot().Where(f => !f.StartsWith("hub.db", StringComparison.Ordinal))
        );
    }

    public static TheoryData<string, string?, int, string[]?> RemoveAllCases =>
        new()
        {
            {
                "migrated db: every row is deleted",
                MigratedDb
                    + """INSERT INTO projects VALUES ('C:\p\Keep', '{"title":"Keep"}', 1);""",
                2,
                []
            },
            {
                "not migrated: no write",
                Schema + """INSERT INTO projects VALUES ('C:\p\DbOnly', '{}', 1);""",
                0,
                [@"C:\p\DbOnly"]
            },
            { "no db: none is created", null, 0, null },
        };

    [Theory]
    [MemberData(nameof(RemoveAllCases))]
    public void Remove_all_from_list(
        string name,
        string? dbSql,
        int expectedRemoved,
        string[]? expectedRows
    )
    {
        _ = name;
        Setup(dbSql, JsonAB, """{"schema_version":"1","data":[]}""", false);
        var dbPath = Path.Combine(_hubDir, "hub.db");
        var otherFiles = Snapshot()
            .Where(f => !f.StartsWith("hub.db", StringComparison.Ordinal))
            .ToArray();

        var removed = HubDatabase.TryRemoveAllProjects(dbPath);

        Assert.Equal(expectedRemoved, removed);
        if (expectedRows is null)
        {
            Assert.False(File.Exists(dbPath));
        }
        else
        {
            Assert.Equal(expectedRows, TestSqlite.Paths(dbPath));
        }

        Assert.Equal(
            otherFiles,
            Snapshot().Where(f => !f.StartsWith("hub.db", StringComparison.Ordinal))
        );
    }

    [Fact]
    public void Remove_succeeds_while_running_hub_holds_the_db()
    {
        var dbPath = Path.Combine(_hubDir, "hub.db");
        using var writer = ExternalWriter.Start(
            _root,
            dbPath,
            MigratedDb
                + "PRAGMA wal_autocheckpoint = 0;"
                + """INSERT INTO projects VALUES ('C:\p\InWal', '{"title":"InWal"}', 2);"""
        );

        Assert.True(HubDatabase.TryRemoveProject(dbPath, @"C:\p\InWal"));

        Assert.Equal(["DbOnly"], ProjectParser.LoadProjects(_hubDir).Select(p => p.Title));
    }

    private string[] Snapshot() =>
        Directory
            .GetFiles(_hubDir)
            .Select(f =>
            {
                using var stream = new FileStream(
                    f,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete
                );
                var hash = Convert.ToHexString(
                    System.Security.Cryptography.SHA256.HashData(stream)
                );
                return $"{Path.GetFileName(f)}|{File.GetLastWriteTimeUtc(f).Ticks}|{hash}";
            })
            .Order()
            .ToArray();

    private void Setup(string? dbSql, string? projectsJson, string? excludedJson, bool jsonNewer)
    {
        var dbPath = Path.Combine(_hubDir, "hub.db");
        var jsonPath = Path.Combine(_hubDir, "projects-v1.json");

        if (dbSql == Corrupt)
        {
            File.WriteAllText(
                dbPath,
                "this is not an SQLite database, just text padding the header"
            );
        }
        else if (dbSql is not null)
        {
            TestSqlite.Create(dbPath, dbSql);
        }

        if (projectsJson is not null)
        {
            File.WriteAllText(jsonPath, projectsJson);
        }

        if (excludedJson is not null)
        {
            File.WriteAllText(Path.Combine(_hubDir, "excludedProjectFolders.json"), excludedJson);
        }

        var older = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var newer = older.AddDays(1);
        if (File.Exists(dbPath))
        {
            File.SetLastWriteTimeUtc(dbPath, jsonNewer ? older : newer);
        }

        if (File.Exists(jsonPath))
        {
            File.SetLastWriteTimeUtc(jsonPath, jsonNewer ? newer : older);
        }
    }

    /// <summary>A pwsh process that runs SQL on a db and keeps it open until disposed.</summary>
    private sealed class ExternalWriter : IDisposable
    {
        private const string Script = """
            param([string]$DbPath, [string]$SqlPath)
            $ErrorActionPreference = 'Stop'
            Add-Type -TypeDefinition @'
            using System;
            using System.Runtime.InteropServices;
            public static class HubTestWriter
            {
                [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode)]
                public static extern int sqlite3_open16(string filename, out IntPtr db);

                [DllImport("winsqlite3", CallingConvention = CallingConvention.Cdecl)]
                public static extern int sqlite3_exec(IntPtr db, byte[] sql, IntPtr callback, IntPtr arg, IntPtr errmsg);
            }
            '@
            $db = [IntPtr]::Zero
            if ([HubTestWriter]::sqlite3_open16($DbPath, [ref]$db) -ne 0) { exit 2 }
            $sql = [IO.File]::ReadAllBytes($SqlPath) + [byte]0
            if ([HubTestWriter]::sqlite3_exec($db, $sql, [IntPtr]::Zero, [IntPtr]::Zero, [IntPtr]::Zero) -ne 0) { exit 3 }
            [Console]::Out.WriteLine('ready')
            [void][Console]::In.ReadLine()
            """;

        private readonly System.Diagnostics.Process _process;

        private ExternalWriter(System.Diagnostics.Process process) => _process = process;

        public static ExternalWriter Start(string scratchDir, string dbPath, string sql)
        {
            var scriptPath = Path.Combine(scratchDir, "writer.ps1");
            var sqlPath = Path.Combine(scratchDir, "writer.sql");
            File.WriteAllText(scriptPath, Script);
            File.WriteAllText(sqlPath, sql);

            var info = new System.Diagnostics.ProcessStartInfo("pwsh")
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            foreach (
                var arg in new[]
                {
                    "-NoLogo",
                    "-NoProfile",
                    "-NonInteractive",
                    "-File",
                    scriptPath,
                    dbPath,
                    sqlPath,
                }
            )
            {
                info.ArgumentList.Add(arg);
            }

            var process = System.Diagnostics.Process.Start(info)!;
            var line = process.StandardOutput.ReadLine();
            if (line != "ready")
            {
                process.WaitForExit(10_000);
                var error = process.StandardError.ReadToEnd();
                process.Dispose();
                throw new InvalidOperationException($"writer failed: {line} {error}");
            }

            return new ExternalWriter(process);
        }

        public void Dispose()
        {
            _process.StandardInput.WriteLine();
            if (!_process.WaitForExit(30_000))
            {
                _process.Kill();
            }

            _process.Dispose();
        }
    }

    /// <summary>Writable SQLite access for building fixtures; the product code is read-only.</summary>
    private static class TestSqlite
    {
        public static void Create(string path, string sql)
        {
            using var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()
            );
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        public static string[] Paths(string path)
        {
            using var connection = new SqliteConnection(
                new SqliteConnectionStringBuilder { DataSource = path, Pooling = false }.ToString()
            );
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT path FROM projects ORDER BY path";
            using var reader = command.ExecuteReader();
            var result = new List<string>();
            while (reader.Read())
            {
                result.Add(reader.GetString(0));
            }

            return result.ToArray();
        }
    }
}
