namespace Pica.Client.Tests;

internal sealed class FixedPicaExecutableSource : IPicaExecutableSource
{
    private readonly IReadOnlyList<string> _paths;

    public FixedPicaExecutableSource(params string[] paths)
    {
        _paths = paths;
    }

    public IEnumerable<string> GetCandidatePaths()
    {
        return _paths;
    }
}
