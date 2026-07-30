using Xunit;

namespace Pica.Desktop.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DesktopHeadlessTestCollection
{
    public const string Name = "Pica desktop headless tests";
}
