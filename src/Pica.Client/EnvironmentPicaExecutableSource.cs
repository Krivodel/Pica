using Pica.Protocol;

namespace Pica.Client;

public sealed class EnvironmentPicaExecutableSource : IPicaExecutableSource
{
    private const string ExecutablePathVariableName = "PICA_EXECUTABLE_PATH";

    public IEnumerable<string> GetCandidatePaths()
    {
        string? configuredPath = Environment.GetEnvironmentVariable(ExecutablePathVariableName);

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            yield return configuredPath;
        }

        string? pathVariable = Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrWhiteSpace(pathVariable))
        {
            yield break;
        }

        foreach (string directoryPath in pathVariable.Split(Path.PathSeparator))
        {
            if (!string.IsNullOrWhiteSpace(directoryPath))
            {
                yield return Path.Combine(directoryPath.Trim(), PicaProtocolConstants.ExecutableName);
            }
        }
    }
}
