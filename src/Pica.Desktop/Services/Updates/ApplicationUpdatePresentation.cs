using System.Globalization;

using Pica.Desktop.Resources;

namespace Pica.Desktop.Services.Updates;

internal sealed class ApplicationUpdatePresentation
{
    public string Message { get; private set; }
    public bool IsProgressVisible { get; private set; }
    public bool IsProgressIndeterminate { get; private set; }
    public int DownloadProgress { get; private set; }
    public bool AreActionsEnabled { get; private set; } = true;
    public bool IsBusy { get; private set; }

    public ApplicationUpdatePresentation(string version)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        Message = string.Format(
            CultureInfo.CurrentCulture,
            DesktopUiStrings.UpdateAvailableFormat,
            version);
    }

    public void ShowDownloadProgress(int progress)
    {
        Message = DesktopUiStrings.UpdateDownloading;
        IsProgressVisible = true;
        IsProgressIndeterminate = false;
        DownloadProgress = Math.Clamp(progress, 0, 100);
        AreActionsEnabled = false;
        IsBusy = true;
    }

    public void ShowInstalling()
    {
        Message = DesktopUiStrings.UpdateInstalling;
        IsProgressVisible = true;
        IsProgressIndeterminate = true;
        AreActionsEnabled = false;
        IsBusy = true;
    }

    public void ShowInstallFailure()
    {
        Message = DesktopUiStrings.UpdateInstallFailed;
        IsProgressVisible = false;
        IsProgressIndeterminate = false;
        AreActionsEnabled = true;
        IsBusy = false;
    }
}
