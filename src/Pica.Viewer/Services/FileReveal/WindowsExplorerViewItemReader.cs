using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Pica.Viewer.Services.FileReveal;

[SupportedOSPlatform("windows")]
internal static class WindowsExplorerViewItemReader
{
    private const uint AllViewItems = 0x2;

    private static readonly Guid FolderViewServiceId =
        new("CDE725B0-CCC9-4519-917E-325D72FAB4CE");

    internal static IReadOnlyList<string> GetItemPaths(object window)
    {
        ArgumentNullException.ThrowIfNull(window);
        IWindowsOleServiceProvider serviceProvider =
            (IWindowsOleServiceProvider)window;
        Guid folderViewServiceId = FolderViewServiceId;
        Guid folderViewInterfaceId = typeof(IWindowsFolderView2).GUID;
        int result = serviceProvider.QueryService(
            ref folderViewServiceId,
            ref folderViewInterfaceId,
            out object? folderViewObject);

        try
        {
            Marshal.ThrowExceptionForHR(result);

            if (folderViewObject is not IWindowsFolderView2 folderView)
            {
                throw new InvalidCastException(
                    "File Explorer did not provide an IFolderView2 service.");
            }

            return ReadItemPaths(folderView);
        }
        finally
        {
            WindowsShellAutomation.Release(folderViewObject);
        }
    }

    private static IReadOnlyList<string> ReadItemPaths(
        IWindowsFolderView2 folderView)
    {
        int result = folderView.ItemCount(
            AllViewItems,
            out int itemCount);
        Marshal.ThrowExceptionForHR(result);
        List<string> itemPaths = new(itemCount);

        for (int itemIndex = 0; itemIndex < itemCount; itemIndex++)
        {
            string? itemPath = GetItemPath(folderView, itemIndex);

            if (!string.IsNullOrWhiteSpace(itemPath))
            {
                itemPaths.Add(itemPath);
            }
        }

        return itemPaths;
    }

    private static string? GetItemPath(
        IWindowsFolderView2 folderView,
        int itemIndex)
    {
        Guid shellItemInterfaceId = typeof(IWindowsShellItem).GUID;
        int result = folderView.GetItem(
            itemIndex,
            ref shellItemInterfaceId,
            out nint shellItemPointer);

        try
        {
            Marshal.ThrowExceptionForHR(result);

            if (shellItemPointer == nint.Zero)
            {
                throw new InvalidOperationException(
                    "File Explorer returned an empty shell item.");
            }

            object shellItemObject =
                Marshal.GetObjectForIUnknown(shellItemPointer);

            try
            {
                if (shellItemObject is not IWindowsShellItem shellItem)
                {
                    throw new InvalidCastException(
                        "File Explorer returned an unsupported shell item.");
                }

                return GetFileSystemPath(shellItem);
            }
            finally
            {
                WindowsShellAutomation.Release(shellItemObject);
            }
        }
        finally
        {
            if (shellItemPointer != nint.Zero)
            {
                Marshal.Release(shellItemPointer);
            }
        }
    }

    private static string? GetFileSystemPath(IWindowsShellItem shellItem)
    {
        int result = shellItem.GetDisplayName(
            WindowsShellItemDisplayName.FileSystemPath,
            out nint itemPathPointer);

        try
        {
            Marshal.ThrowExceptionForHR(result);

            if (itemPathPointer == nint.Zero)
            {
                return null;
            }

            return Marshal.PtrToStringUni(itemPathPointer);
        }
        finally
        {
            if (itemPathPointer != nint.Zero)
            {
                Marshal.FreeCoTaskMem(itemPathPointer);
            }
        }
    }
}
