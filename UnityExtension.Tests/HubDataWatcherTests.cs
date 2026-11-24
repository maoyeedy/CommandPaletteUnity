using System;
using System.IO;
using System.Threading;
using Xunit;

namespace UnityExtension.Tests;

public sealed class HubDataWatcherTests : IDisposable
{
    private static readonly TimeSpan TestDebounce = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan EventWaitTimeout = TimeSpan.FromSeconds(2);

    private readonly string _hubDir = Directory.CreateTempSubdirectory("hubwatchertest").FullName;

    public void Dispose() => Directory.Delete(_hubDir, recursive: true);

    [Theory]
    [InlineData("hub.db")]
    [InlineData("hub.db-wal")]
    [InlineData("projects-v1.json")]
    [InlineData("excludedProjectFolders.json")]
    public void WatchedFileChange_TriggersCallback(string fileName)
    {
        using var signal = new ManualResetEventSlim(false);
        using var watcher = new HubDataWatcher(_hubDir, signal.Set, TestDebounce);

        File.WriteAllText(Path.Combine(_hubDir, fileName), "changed");

        Assert.True(signal.Wait(EventWaitTimeout));
    }

    [Fact]
    public void AtomicRenameIntoWatchedFile_TriggersCallback()
    {
        using var signal = new ManualResetEventSlim(false);
        using var watcher = new HubDataWatcher(_hubDir, signal.Set, TestDebounce);

        var tempPath = Path.Combine(_hubDir, "projects-v1.json.tmp");
        var targetPath = Path.Combine(_hubDir, "projects-v1.json");
        File.WriteAllText(tempPath, "{}");
        File.Move(tempPath, targetPath, overwrite: true);

        Assert.True(signal.Wait(EventWaitTimeout));
    }

    [Theory]
    [InlineData("hub.db-shm")]
    [InlineData("hubInfo.json")]
    [InlineData("logs/info-log.json")]
    public void UnwatchedFileChange_DoesNotTriggerCallback(string relativePath)
    {
        var count = 0;
        using var watcher = new HubDataWatcher(
            _hubDir,
            () => Interlocked.Increment(ref count),
            TestDebounce
        );

        var fullPath = Path.Combine(_hubDir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, "ignored");

        Thread.Sleep(250);
        Assert.Equal(0, Volatile.Read(ref count));
    }

    [Fact]
    public void BurstOfWrites_CoalescesIntoSingleCallback()
    {
        var count = 0;
        using var signal = new ManualResetEventSlim(false);
        using var watcher = new HubDataWatcher(
            _hubDir,
            () =>
            {
                Interlocked.Increment(ref count);
                signal.Set();
            },
            TimeSpan.FromMilliseconds(120)
        );

        var walPath = Path.Combine(_hubDir, "hub.db-wal");
        for (var i = 0; i < 5; i++)
        {
            File.WriteAllText(walPath, $"write-{i}");
            Thread.Sleep(5);
        }

        Assert.True(signal.Wait(EventWaitTimeout));
        Thread.Sleep(200);
        Assert.Equal(1, Volatile.Read(ref count));
    }

    [Fact]
    public void MissingDirectory_DoesNotThrow()
    {
        var missingDir = Path.Combine(_hubDir, "does-not-exist");
        using var watcher = new HubDataWatcher(missingDir, () => { }, TestDebounce);
    }

    [Fact]
    public void ChangeAfterDispose_DoesNotTriggerCallback()
    {
        var count = 0;
        var watcher = new HubDataWatcher(
            _hubDir,
            () => Interlocked.Increment(ref count),
            TestDebounce
        );
        watcher.Dispose();

        File.WriteAllText(Path.Combine(_hubDir, "hub.db"), "after-dispose");
        Thread.Sleep(250);

        Assert.Equal(0, Volatile.Read(ref count));
    }
}
