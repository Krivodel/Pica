using Microsoft.Extensions.Logging;

namespace Pica.Client;

public sealed class PicaExecutableLocator : IPicaExecutableLocator
{
    private readonly IReadOnlyList<IPicaExecutableSource> _sources;
    private readonly ILogger<PicaExecutableLocator> _logger;

    public PicaExecutableLocator(
        IEnumerable<IPicaExecutableSource> sources,
        ILogger<PicaExecutableLocator> logger)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(logger);

        _sources = sources.ToList();
        _logger = logger;
    }

    public string? FindExecutablePath()
    {
        _logger.LogDebug(
            "Searching for the Pica executable using {SourceCount} configured sources",
            _sources.Count);

        StringComparer pathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        HashSet<string> visitedPaths = new(pathComparer);

        foreach (IPicaExecutableSource source in _sources)
        {
            IReadOnlyList<string> candidatePaths;

            try
            {
                candidatePaths = source.GetCandidatePaths().ToList();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to read Pica executable candidates from source {SourceType}",
                    source.GetType().Name);

                continue;
            }

            _logger.LogDebug(
                "Pica executable source {SourceType} returned {CandidateCount} candidates",
                source.GetType().Name,
                candidatePaths.Count);

            foreach (string candidatePath in candidatePaths.Where(path => !string.IsNullOrWhiteSpace(path)))
            {
                string fullPath;

                try
                {
                    fullPath = Path.GetFullPath(candidatePath.Trim().Trim('"'));
                }
                catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
                {
                    _logger.LogWarning(
                        ex,
                        "Ignored an invalid Pica executable candidate from source {SourceType}",
                        source.GetType().Name);

                    continue;
                }

                if (visitedPaths.Add(fullPath) && IsExecutableFile(fullPath))
                {
                    _logger.LogInformation(
                        "Found the Pica executable using source {SourceType}",
                        source.GetType().Name);

                    return fullPath;
                }
            }
        }

        _logger.LogWarning(
            "Pica executable was not found after checking {CandidateCount} unique candidates",
            visitedPaths.Count);

        return null;
    }

    private bool IsExecutableFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return false;
        }

        if (OperatingSystem.IsWindows())
        {
            return true;
        }

        try
        {
            UnixFileMode mode = File.GetUnixFileMode(filePath);
            UnixFileMode executableModes = UnixFileMode.UserExecute
                | UnixFileMode.GroupExecute
                | UnixFileMode.OtherExecute;

            return (mode & executableModes) != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(
                ex,
                "Failed to inspect Unix permissions for a Pica executable candidate");

            return false;
        }
    }
}
