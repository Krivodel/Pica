using Xunit;

namespace Pica.Viewer.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AvaloniaHeadlessCollection
{
    public const string Name = "Avalonia headless";
}
