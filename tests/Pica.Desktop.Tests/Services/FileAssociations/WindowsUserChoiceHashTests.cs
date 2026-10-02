// SPDX-License-Identifier: MPL-2.0
using FluentAssertions;
using Xunit;

using Pica.Desktop.Services.FileAssociations;

namespace Pica.Desktop.Tests.Services.FileAssociations;

public sealed class WindowsUserChoiceHashTests
{
    private const string UserSid = "S-1-5-21-636376821-3290315252-1794850287-1001";

    // Windows-generated vectors from Mozilla's SetDefaultBrowserTest.cpp.
    [Theory]
    [InlineData("https", "FirefoxURL-308046B0AF4A39CB", 19, 23, 7, 56, 506, "uzpIsMVyZ1g=")]
    [InlineData(".html", "FirefoxHTML-308046B0AF4A39CB", 19, 23, 7, 56, 519, "7fjRtUPASlc=")]
    [InlineData("https", "MSEdgeHTM", 19, 23, 3, 48, 119, "Fz0kA3Ymmps=")]
    [InlineData(".html", "ChromeHTML", 19, 23, 6, 3, 628, "R5TD9LGJ5Xw=")]
    [InlineData(".html", "FirefoxHTML-ÀBÇDË😀†", 20, 0, 38, 55, 101, "F3NsK3uNv5E=")]
    public void Calculate_WindowsReferenceVectors_MatchesStoredHash(string extension, string programId,
        int day, int hour, int minute, int second, int millisecond, string expected)
    {
        DateTime timestamp = new(2021, 4, day, hour, minute, second, millisecond, DateTimeKind.Utc);

        string hash = WindowsUserChoiceHash.Calculate(extension, UserSid, programId, timestamp.ToFileTimeUtc());

        hash.Should().Be(expected);
    }
}
