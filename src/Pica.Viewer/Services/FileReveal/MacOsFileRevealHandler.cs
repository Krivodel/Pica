using Pica.Viewer.Services;

namespace Pica.Viewer.Services.FileReveal;

internal sealed class MacOsFileRevealHandler
{
    private const string AppleScriptExecutablePath = "/usr/bin/osascript";

    private readonly IFileRevealProcessLauncher _processLauncher;

    public MacOsFileRevealHandler(IFileRevealProcessLauncher processLauncher)
    {
        _processLauncher = processLauncher
            ?? throw new ArgumentNullException(nameof(processLauncher));
    }

    public void Reveal(string filePath, FileRevealWindowMode windowMode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        List<string> arguments = [];
        IReadOnlyList<string> script = CreateScript(windowMode);

        foreach (string scriptLine in script)
        {
            arguments.Add("-e");
            arguments.Add(scriptLine);
        }

        arguments.Add(filePath);
        _processLauncher.Start(AppleScriptExecutablePath, arguments);
    }

    private static IReadOnlyList<string> CreateScript(
        FileRevealWindowMode windowMode)
    {
        List<string> script =
        [
            "on run argv",
            "set targetFile to POSIX file (item 1 of argv) as alias",
            "tell application \"Finder\"",
            "set targetFolder to container of targetFile"
        ];

        if (windowMode == FileRevealWindowMode.ReuseExisting)
        {
            script.Add("set targetWindow to missing value");
            script.Add("repeat with openWindow in Finder windows");
            script.Add("if target of openWindow is targetFolder then");
            script.Add("set targetWindow to openWindow");
            script.Add("exit repeat");
            script.Add("end if");
            script.Add("end repeat");
            script.Add("if targetWindow is missing value then");
            AddNewWindowScript(script);
            script.Add("end if");
        }
        else
        {
            AddNewWindowScript(script);
        }

        script.Add("select targetFile");
        script.Add("set index of targetWindow to 1");
        script.Add("activate");
        script.Add("end tell");
        script.Add("end run");

        return script;
    }

    private static void AddNewWindowScript(List<string> script)
    {
        script.Add("set targetWindow to make new Finder window");
        script.Add("set target of targetWindow to targetFolder");
    }
}
