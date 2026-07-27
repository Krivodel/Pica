using System;
using System.Windows.Forms;

using Ookii.Dialogs.WinForms;

namespace Pica.Installer;

internal sealed class InstallerFolderPicker
{
    public string? SelectFolder(
        IWin32Window owner,
        string initialPath)
    {
        if (owner is null)
        {
            throw new ArgumentNullException(nameof(owner));
        }

        if (string.IsNullOrWhiteSpace(initialPath))
        {
            throw new ArgumentException(
                "Initial folder path cannot be empty.",
                nameof(initialPath));
        }

        using VistaFolderBrowserDialog dialog =
            new VistaFolderBrowserDialog
            {
                Description =
                    $"Выберите папку установки {InstallerProduct.ApplicationName}",
                SelectedPath = initialPath,
                ShowNewFolderButton = true,
                UseDescriptionForTitle = true
            };

        return dialog.ShowDialog(owner) == DialogResult.OK
            ? dialog.SelectedPath
            : null;
    }
}
