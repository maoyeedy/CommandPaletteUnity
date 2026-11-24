using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Shell;
using Windows.Win32.UI.Shell.Common;

namespace UnityExtension;

/// <summary>
/// Opens native Win32 folder picker dialogs on an STA thread using CsWin32 (no reflection/marshaling).
/// </summary>
internal static class FolderPickerHelper
{
    internal static IReadOnlyList<string> PickFolders(string title) =>
        RunOnStaThread(() => ShowDialog(title, allowMultiSelect: true));

    internal static string? PickFolder(string title)
    {
        var results = RunOnStaThread(() => ShowDialog(title, allowMultiSelect: false));
        return results.Length > 0 ? results[0] : null;
    }

    private static string[] RunOnStaThread(Func<string[]> action)
    {
        string[] result = [];
        Exception? exception = null;

        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
        {
            throw exception;
        }

        return result;
    }

    private static unsafe string[] ShowDialog(string title, bool allowMultiSelect)
    {
        var initHr = PInvoke.CoInitializeEx(
            null,
            COINIT.COINIT_APARTMENTTHREADED | COINIT.COINIT_DISABLE_OLE1DDE
        );
        var shouldUninitialize = initHr.Succeeded;

        IFileOpenDialog* dialog = null;
        try
        {
            var clsid = typeof(FileOpenDialog).GUID;
            var iid = typeof(IFileOpenDialog).GUID;
            var hr = PInvoke.CoCreateInstance(
                in clsid,
                null,
                CLSCTX.CLSCTX_INPROC_SERVER,
                in iid,
                out var rawDialog
            );
            if (hr.Failed || rawDialog is null)
            {
                return [];
            }

            dialog = (IFileOpenDialog*)rawDialog;

            dialog->GetOptions(out var options);
            options |=
                FILEOPENDIALOGOPTIONS.FOS_PICKFOLDERS
                | FILEOPENDIALOGOPTIONS.FOS_FORCEFILESYSTEM
                | FILEOPENDIALOGOPTIONS.FOS_PATHMUSTEXIST;
            if (allowMultiSelect)
            {
                options |= FILEOPENDIALOGOPTIONS.FOS_ALLOWMULTISELECT;
            }

            dialog->SetOptions(options);

            fixed (char* titlePtr = title)
            {
                dialog->SetTitle(titlePtr);
            }

            var ownerHwnd = PInvoke.GetForegroundWindow();
            try
            {
                dialog->Show(ownerHwnd);
            }
            catch (COMException)
            {
                // User cancelled (ERROR_CANCELLED, 0x800704C7) or dialog failed to open.
                return [];
            }

            return allowMultiSelect ? ReadMultipleResults(dialog) : ReadSingleResult(dialog);
        }
        finally
        {
            if (dialog is not null)
            {
                dialog->Release();
            }

            if (shouldUninitialize)
            {
                PInvoke.CoUninitialize();
            }
        }
    }

    private static unsafe string[] ReadSingleResult(IFileOpenDialog* dialog)
    {
        IShellItem* item = null;
        try
        {
            dialog->GetResult(&item);
            if (item is null)
            {
                return [];
            }

            var path = ReadDisplayName(item);
            return string.IsNullOrEmpty(path) ? [] : [path];
        }
        catch (Exception)
        {
            return [];
        }
        finally
        {
            if (item is not null)
            {
                item->Release();
            }
        }
    }

    private static unsafe string[] ReadMultipleResults(IFileOpenDialog* dialog)
    {
        IShellItemArray* items = null;
        try
        {
            dialog->GetResults(&items);
            if (items is null)
            {
                return [];
            }

            items->GetCount(out var count);
            var paths = new List<string>((int)count);
            for (uint i = 0; i < count; i++)
            {
                IShellItem* item = null;
                try
                {
                    items->GetItemAt(i, &item);
                    if (item is not null)
                    {
                        var path = ReadDisplayName(item);
                        if (!string.IsNullOrEmpty(path))
                        {
                            paths.Add(path);
                        }
                    }
                }
                finally
                {
                    if (item is not null)
                    {
                        item->Release();
                    }
                }
            }

            return [.. paths];
        }
        catch (Exception)
        {
            return [];
        }
        finally
        {
            if (items is not null)
            {
                items->Release();
            }
        }
    }

    private static unsafe string? ReadDisplayName(IShellItem* item)
    {
        PWSTR pathPtr = default;
        try
        {
            item->GetDisplayName(SIGDN.SIGDN_FILESYSPATH, &pathPtr);
            return pathPtr.Value is null ? null : pathPtr.ToString();
        }
        finally
        {
            if (pathPtr.Value is not null)
            {
                PInvoke.CoTaskMemFree(pathPtr.Value);
            }
        }
    }
}
