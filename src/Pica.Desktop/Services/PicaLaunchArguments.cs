using Pica.Protocol;

namespace Pica.Desktop.Services;

internal static class PicaLaunchArguments
{
    internal const string ClipboardArgument = "--clipboard";
    internal const string ClipboardAgentArgument = "--clipboard-agent";
    internal const string RefreshClipboardArgument = "--refresh-clipboard";

    public static string? GetHostPipeName(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        return (arguments.Count == 2)
            && string.Equals(
                arguments[0],
                PicaProtocolConstants.PipeArgument,
                StringComparison.Ordinal)
            ? arguments[1]
            : null;
    }

    internal static bool IsClipboard(IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        return (arguments.Count == 1) && (arguments[0] == ClipboardArgument);
    }
}
