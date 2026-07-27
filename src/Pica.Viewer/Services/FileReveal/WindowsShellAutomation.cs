using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Pica.Viewer.Services.FileReveal;

internal static class WindowsShellAutomation
{
    internal static object? GetProperty(object target, string propertyName)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        return target.GetType().InvokeMember(
            propertyName,
            BindingFlags.GetProperty,
            binder: null,
            target,
            args: null,
            modifiers: null,
            CultureInfo.InvariantCulture,
            namedParameters: null);
    }

    internal static object? InvokeMethod(
        object target,
        string methodName,
        object?[]? arguments = null)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentException.ThrowIfNullOrWhiteSpace(methodName);

        return target.GetType().InvokeMember(
            methodName,
            BindingFlags.InvokeMethod,
            binder: null,
            target,
            arguments,
            modifiers: null,
            CultureInfo.InvariantCulture,
            namedParameters: null);
    }

    internal static long GetWindowHandle(object window)
    {
        ArgumentNullException.ThrowIfNull(window);

        object? windowHandleValue = GetProperty(window, "HWND");

        return Convert.ToInt64(
            windowHandleValue,
            CultureInfo.InvariantCulture);
    }

    internal static bool IsAutomationException(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        return exception is COMException
            or InvalidCastException
            or MissingMemberException
            or TargetInvocationException;
    }

    internal static void Release(object? value)
    {
        if (OperatingSystem.IsWindows()
            && value is not null
            && Marshal.IsComObject(value))
        {
            Marshal.FinalReleaseComObject(value);
        }
    }
}
