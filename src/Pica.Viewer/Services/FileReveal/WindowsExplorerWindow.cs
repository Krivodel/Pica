using System.Runtime.InteropServices;

namespace Pica.Viewer.Services.FileReveal;

internal sealed class WindowsExplorerWindow : IWindowsExplorerWindow
{
    private const int SelectItem = 1;
    private const int DeselectOtherItems = 4;
    private const int EnsureItemIsVisible = 8;
    private const int FocusItem = 16;
    private const int SelectItemFlags = SelectItem
        | DeselectOtherItems
        | EnsureItemIsVisible
        | FocusItem;
    private const int RestoreWindowCommand = 9;

    private object? _window;
    private object? _document;
    private object? _folder;

    public WindowsExplorerWindow(
        object window,
        object document,
        object folder)
    {
        _window = window ?? throw new ArgumentNullException(nameof(window));
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _folder = folder ?? throw new ArgumentNullException(nameof(folder));
    }

    public void SelectFile(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException(
                "File Explorer automation is available only on Windows.");
        }

        object window = _window
            ?? throw new ObjectDisposedException(nameof(WindowsExplorerWindow));
        object document = _document
            ?? throw new ObjectDisposedException(nameof(WindowsExplorerWindow));
        object folder = _folder
            ?? throw new ObjectDisposedException(nameof(WindowsExplorerWindow));
        object? folderItem = WindowsShellAutomation.InvokeMethod(
            folder,
            "ParseName",
            [fileName]);

        if (folderItem is null)
        {
            throw new InvalidOperationException(
                "File Explorer could not resolve the requested file.");
        }

        try
        {
            WindowsShellAutomation.InvokeMethod(
                document,
                "SelectItem",
                [folderItem, SelectItemFlags]);
            long windowHandle = WindowsShellAutomation.GetWindowHandle(window);
            nint nativeWindowHandle = checked((nint)windowHandle);
            _ = ShowWindow(nativeWindowHandle, RestoreWindowCommand);
            _ = SetForegroundWindow(nativeWindowHandle);
        }
        finally
        {
            WindowsShellAutomation.Release(folderItem);
        }
    }

    public void Dispose()
    {
        object? folder = _folder;
        object? document = _document;
        object? window = _window;
        _folder = null;
        _document = null;
        _window = null;
        WindowsShellAutomation.Release(folder);
        WindowsShellAutomation.Release(document);
        WindowsShellAutomation.Release(window);
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint windowHandle);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(
        nint windowHandle,
        int showWindowCommand);
}
