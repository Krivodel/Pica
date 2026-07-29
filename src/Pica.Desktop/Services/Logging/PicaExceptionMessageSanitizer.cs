using System.Text.RegularExpressions;

namespace Pica.Desktop.Services.Logging;

internal sealed class PicaExceptionMessageSanitizer
{
    private static readonly Regex NamedSecretRegex = CreateRegex(
        """\b(api[-_ ]?key|token|access[-_ ]?token|refresh[-_ ]?token|authorization|credential|password|secret|pwd|account[-_ ]?key)\b\s*[:=]\s*(?:"[^"]*"|'[^']*'|[^\s,;]+)""");
    private static readonly Regex ProviderKeyRegex = CreateRegex(
        """\b(?:provider|api)[-_ ]?key[-_: =]+[A-Za-z0-9._~-]{6,}\b""");
    private static readonly Regex CredentialRegex = CreateRegex(
        """\b(?:AIza[0-9A-Za-z_-]{16,}|Bearer\s+\S+|eyJ[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}(?:\.[A-Za-z0-9_-]{10,})?)""");
    private static readonly Regex Base64Regex = CreateRegex(
        """(?<![A-Za-z0-9+/=_-])[A-Za-z0-9+/]{40,}={0,2}(?![A-Za-z0-9+/=_-])""");
    private static readonly Regex LocalPathRegex = CreateRegex(
        """(?:"(?:[A-Z]:[\\/]|\\\\|/(?:home|users|tmp|var|etc|opt|srv|mnt|media|run)/)[^"]+"|'(?:[A-Z]:[\\/]|\\\\|/(?:home|users|tmp|var|etc|opt|srv|mnt|media|run)/)[^']+'|(?:[A-Z]:[\\/]|\\\\|/(?:home|users|tmp|var|etc|opt|srv|mnt|media|run)/)[^\s,;"'<>|]+)""");
    private static readonly Regex UrlRegex = CreateRegex(
        """\b(?:https?|ftp)://[^\s"'<>]+""");
    private static readonly Regex EmailRegex = CreateRegex(
        """\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b""");
    private static readonly Regex PhoneRegex = CreateRegex(
        """(?<!\w)(?:\+?\d[\d ()-]{8,}\d)(?!\w)""");
    private static readonly Regex SocialSecurityNumberRegex = CreateRegex(
        """\b\d{3}-\d{2}-\d{4}\b""");
    private static readonly Regex IpAddressRegex = CreateRegex(
        """\b(?:\d{1,3}\.){3}\d{1,3}\b""");
    private static readonly Regex WhitespaceRegex = CreateRegex(
        """\s+""");

    private readonly int _maximumInputMessageCharacters;
    private readonly int _maximumOutputMessageCharacters;

    public PicaExceptionMessageSanitizer(
        PicaFileLoggingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _maximumInputMessageCharacters =
            options.MaximumSanitizerInputMessageCharacters;
        _maximumOutputMessageCharacters =
            options.MaximumSanitizedMessageCharacters;
    }

    public string? Sanitize(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        string sanitizedMessage =
            message.Length <= _maximumInputMessageCharacters
                ? message
                : message[.._maximumInputMessageCharacters];
        sanitizedMessage = WhitespaceRegex
            .Replace(sanitizedMessage, " ")
            .Trim();
        sanitizedMessage = NamedSecretRegex.Replace(
            sanitizedMessage,
            "$1=[REDACTED SECRET]");
        sanitizedMessage = ProviderKeyRegex.Replace(
            sanitizedMessage,
            "[REDACTED SECRET]");
        sanitizedMessage = CredentialRegex.Replace(
            sanitizedMessage,
            "[REDACTED CREDENTIAL]");
        sanitizedMessage = Base64Regex.Replace(
            sanitizedMessage,
            "[REDACTED DATA]");
        sanitizedMessage = UrlRegex.Replace(
            sanitizedMessage,
            "[REDACTED URL]");
        sanitizedMessage = LocalPathRegex.Replace(
            sanitizedMessage,
            "[REDACTED PATH]");
        sanitizedMessage = EmailRegex.Replace(
            sanitizedMessage,
            "[REDACTED EMAIL]");
        sanitizedMessage = SocialSecurityNumberRegex.Replace(
            sanitizedMessage,
            "[REDACTED SSN]");
        sanitizedMessage = PhoneRegex.Replace(
            sanitizedMessage,
            "[REDACTED PHONE]");
        sanitizedMessage = IpAddressRegex.Replace(
            sanitizedMessage,
            "[REDACTED IP]");

        return sanitizedMessage.Length <= _maximumOutputMessageCharacters
            ? sanitizedMessage
            : sanitizedMessage[.._maximumOutputMessageCharacters];
    }

    private static Regex CreateRegex(string pattern)
    {
        return new Regex(
            pattern,
            RegexOptions.Compiled
                | RegexOptions.CultureInvariant
                | RegexOptions.IgnoreCase);
    }
}
