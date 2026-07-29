using Microsoft.Extensions.Logging;

namespace Pica.Desktop.Services.Logging;

internal sealed record PicaFileLoggingOptions
{
    public required LogLevel MinimumLevel { get; init; }
    public required long MaxFileSizeBytes { get; init; }
    public required int MaximumExceptionDepth { get; init; }
    public required int MaximumMessageCharacters { get; init; }
    public required int MaximumSanitizedMessageCharacters { get; init; }
    public required int MaximumSanitizerInputMessageCharacters { get; init; }
    public required int MaximumStackFrameCount { get; init; }
    public required int RetainedFileCount { get; init; }
    public required int RetentionDays { get; init; }

    private const long DefaultMaxFileSizeBytes = 10L * 1024L * 1024L;
    private const int DefaultMaximumExceptionDepth = 5;
    private const int DefaultMaximumMessageCharacters = 8192;
    private const int DefaultMaximumSanitizedMessageCharacters = 2048;
    private const int DefaultMaximumSanitizerInputMessageCharacters = 8192;
    private const int DefaultMaximumStackFrameCount = 64;
    private const int DefaultRetainedFileCount = 14;
    private const int DefaultRetentionDays = 14;

    public static PicaFileLoggingOptions CreateDefault()
    {
        return new PicaFileLoggingOptions
        {
            MinimumLevel = LogLevel.Warning,
            MaxFileSizeBytes = DefaultMaxFileSizeBytes,
            MaximumExceptionDepth = DefaultMaximumExceptionDepth,
            MaximumMessageCharacters = DefaultMaximumMessageCharacters,
            MaximumSanitizedMessageCharacters =
                DefaultMaximumSanitizedMessageCharacters,
            MaximumSanitizerInputMessageCharacters =
                DefaultMaximumSanitizerInputMessageCharacters,
            MaximumStackFrameCount = DefaultMaximumStackFrameCount,
            RetainedFileCount = DefaultRetainedFileCount,
            RetentionDays = DefaultRetentionDays
        };
    }
}
