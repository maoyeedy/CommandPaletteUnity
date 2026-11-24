using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace UnityExtension;

/// <summary>
/// Watches a Unity Hub data folder for changes to the project registry (Hub 4 hub.db/hub.db-wal
/// and Hub 3 projects-v1.json/excludedProjectFolders.json) and invokes a debounced callback.
/// CmdPal usually auto-hides on focus loss and re-reads on reopen, so this mainly covers
/// background Hub writes, pinned palettes, or the separate Settings window's "Remove all".
/// </summary>
internal sealed partial class HubDataWatcher : IDisposable
{
    private static readonly TimeSpan DefaultDebounce = TimeSpan.FromMilliseconds(250);

    private static readonly HashSet<string> WatchedFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "hub.db",
        "hub.db-wal",
        "projects-v1.json",
        "excludedProjectFolders.json",
    };

    private readonly string _hubDir;
    private readonly Action _onChanged;
    private readonly TimeSpan _debounce;
    private readonly Lock _gate = new();
    private readonly FileSystemWatcher? _watcher;
    private readonly Timer? _timer;
    private bool _disposed;

    public HubDataWatcher(string hubDir, Action onChanged, TimeSpan? debounce = null)
    {
        ArgumentNullException.ThrowIfNull(onChanged);

        _hubDir = hubDir;
        _onChanged = onChanged;
        _debounce = debounce ?? DefaultDebounce;

        if (string.IsNullOrWhiteSpace(hubDir) || !Directory.Exists(hubDir))
        {
            return;
        }

        _timer = new Timer(OnTimer, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _watcher = new FileSystemWatcher(hubDir)
        {
            IncludeSubdirectories = false,
            // Include Size because SQLite WAL appends on Windows may not update LastWrite until close.
            NotifyFilter =
                NotifyFilters.LastWrite
                | NotifyFilters.Size
                | NotifyFilters.FileName
                | NotifyFilters.CreationTime,
        };
        _watcher.Changed += OnFileEvent;
        _watcher.Created += OnFileEvent;
        _watcher.Deleted += OnFileEvent;
        _watcher.Renamed += OnRenamed;
        _watcher.Error += OnError;
        _watcher.EnableRaisingEvents = true;
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
        }

        if (_watcher is not null)
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();
        }

        _timer?.Dispose();
    }

    private static bool IsWatched(string? name) =>
        name is not null && WatchedFiles.Contains(Path.GetFileName(name));

    private void OnFileEvent(object sender, FileSystemEventArgs e)
    {
        if (IsWatched(e.Name))
        {
            ScheduleRefresh();
        }
    }

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        if (IsWatched(e.Name) || IsWatched(e.OldName))
        {
            ScheduleRefresh();
        }
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        if (Directory.Exists(_hubDir))
        {
            ScheduleRefresh();
        }
    }

    private void ScheduleRefresh()
    {
        lock (_gate)
        {
            if (_disposed || _timer is null)
            {
                return;
            }

            _timer.Change(_debounce, Timeout.InfiniteTimeSpan);
        }
    }

    private void OnTimer(object? state)
    {
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }
        }

        try
        {
            _onChanged();
        }
        catch (Exception)
        {
            // Never crash the COM server from a background watcher callback.
        }
    }
}
