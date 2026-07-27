using System;
using System.Threading.Tasks;
using System.Windows.Forms;

using Microsoft.Extensions.Logging;

namespace Pica.Installer;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        EmbeddedAssemblyResolver.Register();
        Run();
    }

    private static void Run()
    {
        ILogger<InstallerForm> logger =
            new InstallerFileLogger<InstallerForm>();
        InstallerFolderPicker folderPicker = new InstallerFolderPicker();

        Application.SetUnhandledExceptionMode(
            UnhandledExceptionMode.CatchException);
        Application.ThreadException += (sender, e) =>
        {
            logger.LogError(
                e.Exception,
                "Unhandled installer UI-thread exception.");
            MessageBox.Show(
                "Произошла непредвиденная ошибка установщика.",
                InstallerProduct.InstallationWindowTitle,
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        };
        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            logger.LogError(
                e.Exception,
                "Unobserved installer task exception.");
            e.SetObserved();
        };
        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                logger.LogError(
                    ex,
                    "Unhandled installer process exception. Terminating: {IsTerminating}.",
                    e.IsTerminating);
            }
        };

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new InstallerForm(logger, folderPicker));
    }
}
