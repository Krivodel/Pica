namespace Pica.Viewer.Services;

internal static class ProgressiveImageDecoder
{
    internal static DecodedImage Decode(
        IProgressiveImageFrameReader frameReader,
        ImageFramePresentationModes framePresentationMode,
        ImageAnimationBufferingPolicy bufferingPolicy,
        CancellationToken ct,
        ImageAnimationFrameCachePolicy? frameCachePolicy = null)
    {
        ArgumentNullException.ThrowIfNull(frameReader);
        ArgumentNullException.ThrowIfNull(bufferingPolicy);
        int frameCount = frameReader.FrameCount;

        if (frameCount <= 1)
        {
            frameReader.Dispose();

            throw new ArgumentOutOfRangeException(
                nameof(frameReader),
                frameCount,
                "A progressive image frame reader must contain multiple frames.");
        }

        DecodedImage image = DecodedImage.CreateProgressive(
            frameCount,
            framePresentationMode,
            frameReader.AnimationIterations,
            bufferingPolicy.GetRequiredFrameCount(frameCount),
            frameCachePolicy);
        int initialFrameCount =
            bufferingPolicy.GetInitialSynchronousFrameCount(
                frameCount);

        try
        {
            for (int i = 0; i < initialFrameCount; i++)
            {
                image.AddFrame(
                    i,
                    frameReader.ReadFrame(
                        i,
                        ct));
            }

            if (initialFrameCount == frameCount)
            {
                frameReader.Dispose();

                return image;
            }

            image.StartDecoding(
                decodingToken => DecodeFrames(
                    frameReader,
                    image,
                    initialFrameCount,
                    decodingToken));

            return image;
        }
        catch
        {
            frameReader.Dispose();
            image.Dispose();
            throw;
        }
    }

    private static void DecodeFrames(
        IProgressiveImageFrameReader frameReader,
        DecodedImage image,
        int firstFrameIndex,
        CancellationToken ct)
    {
        if (!image.RetainsCompleteAnimation)
        {
            MaintainPlaybackBuffer(
                frameReader,
                image,
                ct);
            return;
        }

        DecodeRemainingFrames(
            frameReader,
            image,
            firstFrameIndex,
            ct);
    }

    private static void DecodeRemainingFrames(
        IProgressiveImageFrameReader frameReader,
        DecodedImage image,
        int firstFrameIndex,
        CancellationToken ct)
    {
        using (frameReader)
        {
            for (int i = firstFrameIndex;
                i < frameReader.FrameCount;
                i++)
            {
                image.AddFrame(
                    i,
                    frameReader.ReadFrame(
                        i,
                        ct));
            }
        }
    }

    private static void MaintainPlaybackBuffer(
        IProgressiveImageFrameReader frameReader,
        DecodedImage image,
        CancellationToken ct)
    {
        using (frameReader)
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                int? requestedFrameIndex =
                    image.GetNextRequestedFrameIndex();

                if (requestedFrameIndex
                    is not int frameIndex)
                {
                    image.WaitForFrameRequest(ct);
                    continue;
                }

                image.AddFrame(
                    frameIndex,
                    frameReader.ReadFrame(
                        frameIndex,
                        ct));
            }
        }
    }
}
