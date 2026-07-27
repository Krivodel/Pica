using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

using Microsoft.Extensions.Logging;

namespace Pica.Installer;

internal sealed class InstallerForm : Form
{
    private readonly TextBox _installPathTextBox;
    private readonly Button _browseButton;
    private readonly Button _installButton;
    private readonly Button _cancelButton;
    private readonly Label _statusLabel;
    private readonly ILogger<InstallerForm> _logger;
    private readonly InstallerFolderPicker _folderPicker;

    public InstallerForm(
        ILogger<InstallerForm> logger,
        InstallerFolderPicker folderPicker)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _folderPicker = folderPicker
            ?? throw new ArgumentNullException(nameof(folderPicker));

        Text = InstallerProduct.InstallationWindowTitle;
        ClientSize = new Size(560, 132);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9F, FontStyle.Regular);

        Icon? applicationIcon = Icon.ExtractAssociatedIcon(
            Application.ExecutablePath);

        if (applicationIcon is not null)
        {
            Icon = applicationIcon;
        }

        Label pathLabel = new Label
        {
            AutoSize = true,
            Location = new Point(24, 16),
            Text = "Папка установки:"
        };
        _installPathTextBox = new TextBox
        {
            Location = new Point(24, 38),
            Size = new Size(414, 23),
            Text = InstallerPathValidator.GetDefaultInstallPath()
        };
        _browseButton = new Button
        {
            Location = new Point(447, 37),
            Size = new Size(89, 25),
            Text = "Обзор..."
        };
        _statusLabel = new Label
        {
            AutoSize = false,
            Location = new Point(24, 90),
            Size = new Size(250, 22)
        };
        _installButton = new Button
        {
            Location = new Point(344, 86),
            Size = new Size(94, 29),
            Text = "Установить"
        };
        _cancelButton = new Button
        {
            DialogResult = DialogResult.Cancel,
            Location = new Point(447, 86),
            Size = new Size(89, 29),
            Text = "Отмена"
        };

        Controls.Add(pathLabel);
        Controls.Add(_installPathTextBox);
        Controls.Add(_browseButton);
        Controls.Add(_statusLabel);
        Controls.Add(_installButton);
        Controls.Add(_cancelButton);

        AcceptButton = _installButton;
        CancelButton = _cancelButton;

        _browseButton.Click += OnBrowseClicked;
        _installButton.Click += OnInstallClicked;
        _cancelButton.Click += OnCancelClicked;
    }

    private static string GetExistingBrowsePath(string requestedPath)
    {
        string currentPath = requestedPath;

        while (!string.IsNullOrWhiteSpace(currentPath))
        {
            if (Directory.Exists(currentPath))
            {
                return currentPath;
            }

            DirectoryInfo? parentDirectory = Directory.GetParent(currentPath);

            if (parentDirectory is null)
            {
                break;
            }

            currentPath = parentDirectory.FullName;
        }

        return Environment.GetFolderPath(
            Environment.SpecialFolder.ProgramFiles);
    }

    private static string AppendApplicationDirectory(string selectedPath)
    {
        DirectoryInfo selectedDirectory = new DirectoryInfo(selectedPath);

        if (string.Equals(
            selectedDirectory.Name,
            InstallerProduct.ApplicationDirectoryName,
            StringComparison.OrdinalIgnoreCase))
        {
            return selectedDirectory.FullName.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar);
        }

        return Path.Combine(
            selectedDirectory.FullName,
            InstallerProduct.ApplicationDirectoryName);
    }

    private async Task InstallAsync()
    {
        string installPath;

        try
        {
            installPath = InstallerPathValidator.NormalizeAndValidate(
                _installPathTextBox.Text);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(
                ex,
                "Installer rejected an invalid installation path.");
            MessageBox.Show(
                this,
                "Укажите полный путь к отдельной папке установки.",
                InstallerProduct.InstallationWindowTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        _installPathTextBox.Text = installPath;
        SetInstallationControlsEnabled(false);

        try
        {
            using TemporarySetupFile temporarySetup = TemporarySetupFile.Create();
            VelopackSetupRunner runner = new();
            await Task.Run(() =>
            {
                runner.Run(temporarySetup.Path, installPath);
            });
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogInformation(
                ex,
                "Installer elevation was cancelled.");
            SetInstallationControlsEnabled(true);
            _statusLabel.Text = "";
            return;
        }
        catch (IOException ex)
        {
            HandleInstallationFailure(ex);
            return;
        }
        catch (UnauthorizedAccessException ex)
        {
            HandleInstallationFailure(ex);
            return;
        }
        catch (InvalidOperationException ex)
        {
            HandleInstallationFailure(ex);
            return;
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            HandleInstallationFailure(ex);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void SetInstallationControlsEnabled(bool enabled)
    {
        _installPathTextBox.Enabled = enabled;
        _browseButton.Enabled = enabled;
        _installButton.Enabled = enabled;
        _cancelButton.Enabled = enabled;
        _statusLabel.Text = enabled ? "" : "Установка...";
    }

    private void HandleInstallationFailure(Exception ex)
    {
        _logger.LogError(
            ex,
            "Pica installation failed.");
        SetInstallationControlsEnabled(true);
        MessageBox.Show(
            this,
            "Не удалось завершить установку. Попробуйте снова или выберите другую папку.",
            InstallerProduct.InstallationWindowTitle,
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private void OnBrowseClicked(object? sender, EventArgs e)
    {
        string initialPath = GetExistingBrowsePath(
            _installPathTextBox.Text);
        string? selectedPath = _folderPicker.SelectFolder(
            this,
            initialPath);

        if (selectedPath is not null)
        {
            _installPathTextBox.Text = AppendApplicationDirectory(
                selectedPath);
        }
    }

    private async void OnInstallClicked(object? sender, EventArgs e)
    {
        await InstallAsync();
    }

    private void OnCancelClicked(object? sender, EventArgs e)
    {
        Close();
    }
}
