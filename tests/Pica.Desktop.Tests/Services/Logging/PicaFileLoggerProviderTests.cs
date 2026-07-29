using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using FluentAssertions;
using Xunit;

using Pica.Desktop.Services.Logging;
using Pica.Tests.Common;

namespace Pica.Desktop.Tests.Services.Logging;

public sealed class PicaFileLoggerProviderTests
{
    [Fact]
    public void AddPicaFileLogging_WithDefaultOptions_WritesWarningButNotInformation()
    {
        using PicaTemporaryDirectory temporaryDirectory = new();
        string logDirectoryPath = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "Logs");
        ServiceCollection services = new();
        services.AddPicaFileLogging(
            logDirectoryPath,
            PicaFileLoggingOptions.CreateDefault());

        using (ServiceProvider provider =
            services.BuildServiceProvider())
        {
            ILogger<PicaFileLoggerProviderTests> logger =
                provider.GetRequiredService<
                    ILogger<PicaFileLoggerProviderTests>>();

            logger.LogInformation(
                "Information entry must be filtered.");
            logger.LogWarning("Warning entry must be written.");
        }

        string logPath = Directory
            .GetFiles(logDirectoryPath, "pica-*.log")
            .Should()
            .ContainSingle()
            .Which;
        string contents = File.ReadAllText(logPath);
        contents.Should().Contain("Warning entry must be written.");
        contents.Should().NotContain("Information entry must be filtered.");
    }

    [Fact]
    public void Log_WithFileSizeLimit_RotatesAndRetainsConfiguredFileCount()
    {
        using PicaTemporaryDirectory temporaryDirectory = new();
        string logDirectoryPath = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "Logs");
        PicaFileLoggingOptions options =
            PicaFileLoggingOptions.CreateDefault() with
            {
                MaxFileSizeBytes = 4096,
                RetainedFileCount = 2
            };
        string padding = new('x', 4096);

        using (PicaFileLoggerProvider provider = new(
            logDirectoryPath,
            options))
        {
            ILogger logger = provider.CreateLogger("Pica.Tests");

            for (int index = 0; index < 40; index++)
            {
                logger.LogWarning(
                    "Rotation record {RecordIndex} {Padding}",
                    index,
                    padding);
            }
        }

        string[] logPaths = Directory.GetFiles(
            logDirectoryPath,
            "pica-*.log");
        string retainedContents = string.Join(
            Environment.NewLine,
            logPaths.Select(File.ReadAllText));
        logPaths.Should().HaveCount(2);
        retainedContents.Should().Contain("Rotation record 39");
    }

    [Fact]
    public void Log_WithUnavailableLogDirectory_DoesNotThrow()
    {
        using PicaTemporaryDirectory temporaryDirectory = new();
        string unavailablePath = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "not-a-directory");
        File.WriteAllText(unavailablePath, "file blocks directory creation");
        using PicaFileLoggerProvider provider = new(
            unavailablePath,
            PicaFileLoggingOptions.CreateDefault());
        ILogger logger = provider.CreateLogger("Pica.Tests");

        Action act = () =>
        {
            logger.LogError("This write cannot reach the file system.");
        };

        act.Should().NotThrow();
    }

    [Fact]
    public void Log_WithException_WritesSanitizedDetails()
    {
        using PicaTemporaryDirectory temporaryDirectory = new();
        string logDirectoryPath = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "Logs");
        string confidentialPath = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "private-image.png");
        string apiKey = "desktop-api-key-secret-value";
        string bearerToken = "bearer-token-secret-value";
        string encodedData =
            "QUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFB";

        using (PicaFileLoggerProvider provider = new(
            logDirectoryPath,
            PicaFileLoggingOptions.CreateDefault()))
        {
            ILogger logger = provider.CreateLogger("Pica.Tests");
            InvalidOperationException exception = new(
                $"Failure at '{confidentialPath}', "
                    + $"URL https://private.example.invalid/resource, "
                    + $"owner@example.com, apiKey={apiKey}, "
                    + $"Bearer {bearerToken}, data {encodedData}, "
                    + "phone +1 (555) 123-4567, SSN 123-45-6789 "
                    + "and IP 192.168.1.55.",
                new IOException(
                    $"Inner read failed for '{confidentialPath}' "
                        + $"with token={bearerToken}."));

            logger.LogError(
                exception,
                "Safe operation failed.\r\nForged log line");
        }

        string logPath = Directory
            .GetFiles(logDirectoryPath, "pica-*.log")
            .Should()
            .ContainSingle()
            .Which;
        string contents = File.ReadAllText(logPath);
        contents.Should().Contain(
            "Safe operation failed.  Forged log line");
        contents.Should().Contain(
            typeof(InvalidOperationException).FullName);
        contents.Should().Contain("[REDACTED SECRET]");
        contents.Should().Contain("[REDACTED CREDENTIAL]");
        contents.Should().Contain("[REDACTED DATA]");
        contents.Should().Contain("[REDACTED PATH]");
        contents.Should().Contain("[REDACTED URL]");
        contents.Should().Contain("[REDACTED EMAIL]");
        contents.Should().Contain("[REDACTED PHONE]");
        contents.Should().Contain("[REDACTED SSN]");
        contents.Should().Contain("[REDACTED IP]");
        contents.Should().NotContain(confidentialPath);
        contents.Should().NotContain(apiKey);
        contents.Should().NotContain(bearerToken);
        contents.Should().NotContain(encodedData);
        contents.Should().NotContain("private.example.invalid");
        contents.Should().NotContain("owner@example.com");
        contents.Should().NotContain("+1 (555) 123-4567");
        contents.Should().NotContain("123-45-6789");
        contents.Should().NotContain("192.168.1.55");
        contents.Should().NotContain("\r\nForged log line");
    }

    [Fact]
    public void Log_WithTwoProviders_CreatesSeparateProcessSafeFiles()
    {
        using PicaTemporaryDirectory temporaryDirectory = new();
        string logDirectoryPath = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "Logs");
        PicaFileLoggingOptions options =
            PicaFileLoggingOptions.CreateDefault();
        using (PicaFileLoggerProvider firstProvider = new(
            logDirectoryPath,
            options))
        using (PicaFileLoggerProvider secondProvider = new(
            logDirectoryPath,
            options))
        {
            ILogger firstLogger =
                firstProvider.CreateLogger("Pica.First");
            ILogger secondLogger =
                secondProvider.CreateLogger("Pica.Second");

            firstLogger.LogWarning("First provider entry.");
            secondLogger.LogWarning("Second provider entry.");
        }

        string[] logPaths = Directory.GetFiles(
            logDirectoryPath,
            "pica-*.log");
        string contents = string.Join(
            Environment.NewLine,
            logPaths.Select(File.ReadAllText));
        logPaths.Should().HaveCount(2);
        contents.Should().Contain("First provider entry.");
        contents.Should().Contain("Second provider entry.");
    }
}
