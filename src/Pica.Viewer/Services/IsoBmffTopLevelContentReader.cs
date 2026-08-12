namespace Pica.Viewer.Services;

internal static class IsoBmffTopLevelContentReader
{
    internal static IsoBmffTopLevelContent? Read(
        ReadOnlySpan<byte> data)
    {
        int? metaBoxOffset = null;
        int? movieBoxOffset = null;
        int offset = 0;

        while (offset < data.Length)
        {
            if (!IsoBmffBoxReader.TryRead(
                data,
                offset,
                data.Length,
                out IsoBmffBox box))
            {
                return null;
            }

            if ((box.Type == IsoBmffBoxTypes.Meta)
                && !metaBoxOffset.HasValue)
            {
                metaBoxOffset = offset;
            }
            else if ((box.Type == IsoBmffBoxTypes.Movie)
                && !movieBoxOffset.HasValue)
            {
                movieBoxOffset = offset;
            }

            offset = box.End;
        }

        return metaBoxOffset.HasValue
            && movieBoxOffset.HasValue
                ? new IsoBmffTopLevelContent(
                    metaBoxOffset.Value,
                    movieBoxOffset.Value)
                : null;
    }
}
