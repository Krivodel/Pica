namespace Pica.Viewer.Tests.Services;

internal static class ApngImageTestData
{
    private const string ContentBase64 =
        """
        iVBORw0KGgoAAAANSUhEUgAAAAQAAAADCAYAAAC09K7GAAAACGFjVEwAAAACAAAAAPONk3AAAAAaZmNUTAAAAAAAAAAEAAAAAwAAAAAA
        AAAAADID6AAAkiFljQAAABVJREFUeJxj/M/A8J8BCTAxoAEMAQBhawIEQpfqeQAAABpmY1RMAAAAAQAAAAQAAAADAAAAAAAAAAAA
        eAPoAAAbEc+xAAAAGWZkQVQAAAACeJxjZGD4/58BCTAxoAEMAQBfbQIEsVDRmwAAAABJRU5ErkJggg==
        """;
    private const string SourceClearContentBase64 =
        """
        iVBORw0KGgoAAAANSUhEUgAAAAIAAAABCAYAAAD0In+KAAAACGFjVEwAAAACAAAAAPONk3AAAAAaZmNUTAAAAAAAAAACAAAAAQAA
        AAAAAAAAADID6AAAp3vfDwAAABFJREFUeJxj+M/A8J/hP8N/ABD4A/1BcHnoAAAAGmZjVEwAAAABAAAAAQAAAAEAAAAAAAAAAAB4
        A+gAAI0d85oAAAARZmRBVAAAAAJ4nGNgYGBgAAAABQABtvrrOQAAAABJRU5ErkJggg==
        """;
    private const string SeparateDefaultContentBase64 =
        """
        iVBORw0KGgoAAAANSUhEUgAAAAIAAAABCAYAAAD0In+KAAAACGFjVEwAAAACAAAAA2qEwsoAAAARSURBVHicY2RgYPjPwMDAAAAF
        DQEB66msJQAAABpmY1RMAAAAAAAAAAIAAAABAAAAAAAAAAAAMgPoAACne98PAAAAFWZkQVQAAAABeJxj/M/A8J+BgYEBAA0FAgDn
        WyfOAAAAGmZjVEwAAAACAAAAAgAAAAEAAAAAAAAAAAB4A+gAAFiuTA4AAAAVZmRBVAAAAAN4nGNk+M/wn4GBgQEADAYCAIZzLM8A
        AAAASUVORK5CYII=
        """;

    internal static byte[] GetContent()
    {
        return Convert.FromBase64String(ContentBase64);
    }

    internal static byte[] GetSourceClearContent()
    {
        return Convert.FromBase64String(
            SourceClearContentBase64);
    }

    internal static byte[] GetSeparateDefaultContent()
    {
        return Convert.FromBase64String(
            SeparateDefaultContentBase64);
    }

    internal static void Create(string path)
    {
        byte[] content = GetContent();
        File.WriteAllBytes(path, content);
    }
}
