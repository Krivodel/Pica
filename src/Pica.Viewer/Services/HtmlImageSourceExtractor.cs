using System.Net;

namespace Pica.Viewer.Services;

internal static class HtmlImageSourceExtractor
{
    private static readonly HashSet<string> RawTextTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "textarea", "title", "xmp"
    };

    internal static IReadOnlyList<string> ExtractSources(string html)
    {
        return ExtractAttributeValues(html, "img", "src");
    }

    internal static string? ExtractBase(string html)
    {
        return ExtractAttributeValues(html, "base", "href").FirstOrDefault();
    }

    private static IReadOnlyList<string> ExtractAttributeValues(
        string html, string tagName, string attributeName)
    {
        ArgumentNullException.ThrowIfNull(html);
        List<string> sources = [];
        int searchIndex = 0;

        while (searchIndex < html.Length)
        {
            int tagIndex = html.IndexOf('<', searchIndex);

            if (tagIndex < 0)
            {
                break;
            }

            if (html.AsSpan(tagIndex).StartsWith("<!--", StringComparison.Ordinal))
            {
                int commentEnd = html.IndexOf("-->", tagIndex, StringComparison.Ordinal);
                searchIndex = commentEnd < 0 ? html.Length : commentEnd + "-->".Length;
                continue;
            }

            int nameStart = tagIndex + 1;
            int nameEnd = nameStart;

            while ((nameEnd < html.Length) && char.IsAsciiLetterOrDigit(html[nameEnd]))
            {
                nameEnd++;
            }

            int tagEnd = FindTagEnd(html, nameEnd);

            if (tagEnd < 0)
            {
                break;
            }

            string name = html[nameStart..nameEnd];
            searchIndex = tagEnd + 1;

            if (RawTextTags.Contains(name))
            {
                int closingTag = html.IndexOf($"</{name}", searchIndex, StringComparison.OrdinalIgnoreCase);
                searchIndex = closingTag < 0 ? html.Length : closingTag;
                continue;
            }

            if (string.Equals(name, tagName, StringComparison.OrdinalIgnoreCase)
                && ((html[nameEnd] is '/' or '>') || char.IsWhiteSpace(html[nameEnd]))
                && TryGetAttributeValue(html.AsSpan(nameEnd, tagEnd - nameEnd),
                    attributeName, out string? value)
                && !string.IsNullOrWhiteSpace(value))
            {
                sources.Add(WebUtility.HtmlDecode(value));
            }
        }

        return sources;
    }

    private static int FindTagEnd(string html, int start)
    {
        char quote = '\0';

        for (int index = start; index < html.Length; index++)
        {
            char character = html[index];

            if (quote != '\0')
            {
                if (character == quote)
                {
                    quote = '\0';
                }
            }
            else if (character is '\'' or '"')
            {
                quote = character;
            }
            else if (character == '>')
            {
                return index;
            }
        }

        return -1;
    }

    private static bool TryGetAttributeValue(
        ReadOnlySpan<char> tag,
        string attributeName,
        out string? value)
    {
        int index = 0;

        while (index < tag.Length)
        {
            SkipWhiteSpace(tag, ref index);
            int nameStart = index;

            while (index < tag.Length
                   && !char.IsWhiteSpace(tag[index])
                   && tag[index] != '=')
            {
                index++;
            }

            ReadOnlySpan<char> name = tag[nameStart..index];
            SkipWhiteSpace(tag, ref index);

            if (index >= tag.Length || tag[index] != '=')
            {
                continue;
            }

            index++;
            SkipWhiteSpace(tag, ref index);
            ReadOnlySpan<char> attributeValue = ReadAttributeValue(tag, ref index);

            if (name.Equals(attributeName, StringComparison.OrdinalIgnoreCase))
            {
                value = attributeValue.ToString();
                return true;
            }
        }

        value = null;
        return false;
    }

    private static ReadOnlySpan<char> ReadAttributeValue(
        ReadOnlySpan<char> tag,
        ref int index)
    {
        if (index >= tag.Length)
        {
            return [];
        }

        char quote = tag[index];

        if (quote is '"' or '\'')
        {
            index++;
            int valueStart = index;

            while (index < tag.Length && tag[index] != quote)
            {
                index++;
            }

            ReadOnlySpan<char> value = tag[valueStart..index];

            if (index < tag.Length)
            {
                index++;
            }

            return value;
        }

        int unquotedValueStart = index;

        while (index < tag.Length && !char.IsWhiteSpace(tag[index]))
        {
            index++;
        }

        return tag[unquotedValueStart..index];
    }

    private static void SkipWhiteSpace(ReadOnlySpan<char> value, ref int index)
    {
        while (index < value.Length && char.IsWhiteSpace(value[index]))
        {
            index++;
        }
    }
}
