using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Pica.Viewer.Services;

[SupportedOSPlatform("windows")]
internal sealed class WindowsFileActions : PlatformFileActions
{
    public override bool SupportsOpenWith => true;

    private readonly WindowsApplicationIconLoader _applicationIconLoader;
    private readonly IFileRevealPlatform _fileRevealPlatform;

    public WindowsFileActions(
        WindowsApplicationIconLoader applicationIconLoader,
        IFileRevealPlatform fileRevealPlatform)
    {
        _applicationIconLoader = applicationIconLoader
            ?? throw new ArgumentNullException(nameof(applicationIconLoader));
        _fileRevealPlatform = fileRevealPlatform
            ?? throw new ArgumentNullException(nameof(fileRevealPlatform));
    }

    protected override IReadOnlyList<OpenWithApplication> GetOpenWithApplicationsCore(
        string filePath)
    {
        string extension = Path.GetExtension(filePath);

        if (string.IsNullOrWhiteSpace(extension))
        {
            return new List<OpenWithApplication>();
        }

        IWindowsEnumAssocHandlers handlers = CreateHandlerEnumerator(
            extension,
            WindowsAssocFilter.Recommended);
        List<OpenWithApplication> applications = [];

        try
        {
            while (GetNextHandlerOrDefault(handlers) is IWindowsAssocHandler handler)
            {
                try
                {
                    string identifier = GetHandlerName(handler);
                    string displayName = GetHandlerDisplayName(handler);

                    if (!string.IsNullOrWhiteSpace(identifier)
                        && !string.IsNullOrWhiteSpace(displayName)
                        && !applications.Any(application => string.Equals(
                            application.Identifier,
                            identifier,
                            StringComparison.OrdinalIgnoreCase)))
                    {
                        (string iconPath, int iconIndex) = GetHandlerIconLocation(handler);
                        byte[]? iconPngContent = _applicationIconLoader.Load(
                            iconPath,
                            iconIndex,
                            identifier);
                        applications.Add(new OpenWithApplication(
                            identifier,
                            displayName,
                            iconPngContent));
                    }
                }
                finally
                {
                    WindowsComObject.Release(handler);
                }
            }
        }
        finally
        {
            WindowsComObject.Release(handlers);
        }

        return applications
            .OrderBy(application => application.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    protected override Task RevealInFolderCoreAsync(
        string filePath,
        FileRevealWindowMode windowMode,
        CancellationToken ct)
    {
        return _fileRevealPlatform.RevealAsync(filePath, windowMode, ct);
    }

    protected override Task OpenWithCoreAsync(
        string filePath,
        OpenWithApplication application,
        CancellationToken ct)
    {
        string extension = Path.GetExtension(filePath);
        IWindowsAssocHandler? handler = FindHandler(extension, application.Identifier);

        if (handler is null)
        {
            throw new InvalidOperationException(
                $"The Windows association handler '{application.Identifier}' is no longer available.");
        }

        try
        {
            using WindowsShellDataObject dataObject = WindowsShellDataObject.Create(filePath);
            Marshal.ThrowExceptionForHR(handler.Invoke(dataObject.Pointer));
        }
        finally
        {
            WindowsComObject.Release(handler);
        }

        return Task.CompletedTask;
    }

    protected override Task ChooseApplicationCoreAsync(
        string filePath,
        CancellationToken ct)
    {
        WindowsOpenAsInfo openAsInfo = new()
        {
            FilePath = filePath,
            FileClass = null,
            Flags = WindowsOpenAsFlags.Execute
        };
        Marshal.ThrowExceptionForHR(
            WindowsShellNativeMethods.SHOpenWithDialog(nint.Zero, openAsInfo));

        return Task.CompletedTask;
    }

    private static IWindowsEnumAssocHandlers CreateHandlerEnumerator(
        string extension,
        WindowsAssocFilter filter)
    {
        int result = WindowsShellNativeMethods.SHAssocEnumHandlers(
            extension,
            filter,
            out IWindowsEnumAssocHandlers handlers);
        Marshal.ThrowExceptionForHR(result);

        return handlers;
    }

    private static IWindowsAssocHandler? GetNextHandlerOrDefault(IWindowsEnumAssocHandlers handlers)
    {
        int result = handlers.Next(
            1,
            out IWindowsAssocHandler? handler,
            out int fetched);

        if ((result != 0) || (fetched != 1))
        {
            return null;
        }

        return handler;
    }

    private static IWindowsAssocHandler? FindHandler(string extension, string identifier)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        IWindowsEnumAssocHandlers handlers = CreateHandlerEnumerator(
            extension,
            WindowsAssocFilter.Recommended);

        try
        {
            while (GetNextHandlerOrDefault(handlers) is IWindowsAssocHandler handler)
            {
                string handlerIdentifier = GetHandlerName(handler);

                if (string.Equals(
                    handlerIdentifier,
                    identifier,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return handler;
                }

                WindowsComObject.Release(handler);
            }

            return null;
        }
        finally
        {
            WindowsComObject.Release(handlers);
        }
    }

    private static string GetHandlerName(IWindowsAssocHandler handler)
    {
        int result = handler.GetName(out nint namePointer);

        return ReadAndFreeString(result, namePointer);
    }

    private static string GetHandlerDisplayName(IWindowsAssocHandler handler)
    {
        int result = handler.GetUIName(out nint namePointer);

        return ReadAndFreeString(result, namePointer);
    }

    private static (string IconPath, int IconIndex) GetHandlerIconLocation(
        IWindowsAssocHandler handler)
    {
        int result = handler.GetIconLocation(out nint pathPointer, out int iconIndex);
        string iconPath = ReadAndFreeString(result, pathPointer);

        return (iconPath, iconIndex);
    }

    private static string ReadAndFreeString(int result, nint valuePointer)
    {
        try
        {
            return result >= 0
                ? Marshal.PtrToStringUni(valuePointer) ?? string.Empty
                : string.Empty;
        }
        finally
        {
            if (valuePointer != nint.Zero)
            {
                Marshal.FreeCoTaskMem(valuePointer);
            }
        }
    }
}
