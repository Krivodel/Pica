namespace Pica.Viewer.Tests.Services;

internal static class AvifImageTestData
{
    internal const int Width = 4;
    internal const int Height = 3;

    private const string ContentBase64 =
        """
        AAAAHGZ0eXBhdmlmAAAAAG1pZjFhdmlmbWlhZgAAANZtZXRhAAAAAAAAACFoZGxyAAAAAAAAAABwaWN0AAAAAAAAAAAAAAAAAAAA
        ACJpbG9jAAAAAERAAAEAAQAAAAAA+gABAAAAAAAAACYAAAAjaWluZgAAAAAAAQAAABVpbmZlAgAAAAABAABhdjAxAAAAAA5waXRt
        AAAAAAABAAAAVmlwcnAAAAA4aXBjbwAAAAxhdjFDgQAMAAAAABRpc3BlAAAAAAAAAAQAAAADAAAAEHBpeGkAAAAAAwgICAAAABZp
        cG1hAAAAAAAAAAEAAQOBAgMAAAAubWRhdBIACggYBHmiAhoNCDIYGUeHhiGHnnnmgAAAkEDJHF7QU72Iwohw
        """;

    internal static void Create(string path)
    {
        byte[] content = Convert.FromBase64String(ContentBase64);
        File.WriteAllBytes(path, content);
    }
}
