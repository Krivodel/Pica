using Pica.Protocol;

namespace Pica.Desktop.Services;

internal static class PicaLaunchArguments
{
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
}
