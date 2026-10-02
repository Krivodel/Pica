using Xunit;

namespace Pica.Desktop.Tests.Services.Background;

[CollectionDefinition(WindowsShortcutCollection.Name, DisableParallelization = true)]
public sealed class WindowsShortcutCollection
{
    public const string Name = "Windows clipboard shortcuts";
}
