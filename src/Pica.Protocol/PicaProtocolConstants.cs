using System.Reflection;

namespace Pica.Protocol;

public static class PicaProtocolConstants
{
    public const int CurrentVersion = 1;
    public const string PipeArgument = "--pica-pipe";
    public const int MaximumMessageBytes = 4 * 1024 * 1024;

    public static string ApplicationName => ApplicationNameValue;
    public static string ExecutableName => OperatingSystem.IsWindows()
        ? $"{ApplicationName}.exe"
        : ApplicationName;

    private static readonly string ApplicationNameValue = GetApplicationName();

    private static string GetApplicationName()
    {
        AssemblyTitleAttribute? assemblyTitle = typeof(PicaProtocolConstants)
            .Assembly
            .GetCustomAttribute<AssemblyTitleAttribute>();

        return assemblyTitle?.Title
            ?? throw new InvalidOperationException("The application name assembly title is missing.");
    }
}
