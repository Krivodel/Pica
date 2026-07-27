namespace Pica.Client;

public interface IPicaExecutableSource
{
    IEnumerable<string> GetCandidatePaths();
}
