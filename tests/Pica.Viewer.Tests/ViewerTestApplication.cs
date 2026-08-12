using Avalonia;

using SukiUI;

namespace Pica.Viewer.Tests;

internal sealed class ViewerTestApplication : Application
{
    public override void Initialize()
    {
        Styles.Add(new SukiTheme());
    }
}
