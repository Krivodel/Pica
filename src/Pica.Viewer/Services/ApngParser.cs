using System.Buffers.Binary;

namespace Pica.Viewer.Services;

internal static class ApngParser
{
    private const uint ImageHeaderChunkType =
        0x49484452;
    private const uint AnimationControlChunkType =
        0x6163544C;
    private const uint FrameControlChunkType =
        0x6663544C;
    private const uint ImageDataChunkType =
        0x49444154;
    private const uint FrameDataChunkType =
        0x66644154;
    private const uint ImageEndChunkType =
        0x49454E44;
    private const int ImageHeaderDataLength = 13;
    private const int AnimationControlDataLength = 8;
    private const int FrameControlDataLength = 26;
    private const int FrameDataSequenceLength = 4;
    private const int CanvasWidthOffset = 0;
    private const int CanvasHeightOffset = 4;
    private const int AnimationFrameCountOffset = 0;
    private const int AnimationIterationsOffset = 4;
    private const int FrameSequenceNumberOffset = 0;
    private const int FrameWidthOffset = 4;
    private const int FrameHeightOffset = 8;
    private const int FrameHorizontalOffset = 12;
    private const int FrameVerticalOffset = 16;
    private const int FrameDelayNumeratorOffset = 20;
    private const int FrameDelayDenominatorOffset = 22;
    private const int FrameDisposeOperationOffset = 24;
    private const int FrameBlendOperationOffset = 25;
    private const double MillisecondsPerSecond = 1000d;
    private const ushort DefaultDelayDenominator = 100;

    internal static bool IsAnimated(
        Stream sourceStream,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sourceStream);
        long initialPosition = sourceStream.Position;

        try
        {
            PngChunkReader chunkReader = new(
                sourceStream);
            chunkReader.ReadSignature();

            while (true)
            {
                uint chunkType =
                    chunkReader
                        .ReadNextChunkTypeAndSkipData(
                            ct);

                if (chunkType == AnimationControlChunkType)
                {
                    return true;
                }

                if ((chunkType == ImageDataChunkType)
                    || (chunkType == ImageEndChunkType))
                {
                    return false;
                }
            }
        }
        finally
        {
            sourceStream.Position = initialPosition;
        }
    }

    internal static ApngAnimationData Parse(
        Stream sourceStream,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(sourceStream);
        ct.ThrowIfCancellationRequested();
        PngChunkReader chunkReader = new(
            sourceStream);
        chunkReader.ReadSignature();
        byte[] imageHeaderData =
            chunkReader.ReadRequiredChunk(
            ImageHeaderChunkType,
            ImageHeaderDataLength,
            ct);
        int canvasWidth = ReadPositiveInt32(
            imageHeaderData,
            CanvasWidthOffset,
            "canvas width");
        int canvasHeight = ReadPositiveInt32(
            imageHeaderData,
            CanvasHeightOffset,
            "canvas height");
        List<byte[]> sharedChunks = [];
        List<ApngFrameData> frames = [];
        List<byte[]>? currentCompressedData = null;
        int currentX = 0;
        int currentY = 0;
        int currentWidth = 0;
        int currentHeight = 0;
        TimeSpan currentDuration = default;
        ApngDisposeOperation currentDisposeOperation =
            ApngDisposeOperation.None;
        ApngBlendOperation currentBlendOperation =
            ApngBlendOperation.Source;
        uint declaredFrameCount = 0;
        uint animationIterations = 0;
        uint expectedSequenceNumber = 0;
        bool hasAnimationControl = false;
        bool hasImageData = false;
        bool currentFrameUsesImageData = false;

        void FinishCurrentFrame()
        {
            if (currentCompressedData is null)
            {
                return;
            }

            if (currentCompressedData.Count == 0)
            {
                throw new InvalidDataException(
                    "The APNG frame does not contain compressed image data.");
            }

            frames.Add(new ApngFrameData(
                currentX,
                currentY,
                currentWidth,
                currentHeight,
                currentDuration,
                currentDisposeOperation,
                currentBlendOperation,
                currentCompressedData.AsReadOnly()));
            currentCompressedData = null;
            currentFrameUsesImageData = false;
        }

        while (true)
        {
            PngChunk chunk =
                chunkReader.ReadChunk(ct);
            uint chunkType = chunk.Type;
            byte[] chunkData = chunk.Data;

            if (chunkType == ImageHeaderChunkType)
            {
                throw new InvalidDataException(
                    "The PNG contains more than one image header chunk.");
            }

            if (!hasImageData
                && (chunkType != AnimationControlChunkType)
                && (chunkType != FrameControlChunkType)
                && (chunkType != ImageDataChunkType)
                && (chunkType != FrameDataChunkType)
                && (chunkType != ImageEndChunkType))
            {
                sharedChunks.Add(
                    chunk.RawContent);
            }

            if (chunkType == AnimationControlChunkType)
            {
                if (hasAnimationControl
                    || hasImageData
                    || (chunkData.Length
                        != AnimationControlDataLength))
                {
                    throw new InvalidDataException(
                        "The PNG contains an invalid animation control chunk.");
                }

                declaredFrameCount =
                    BinaryPrimitives.ReadUInt32BigEndian(
                        chunkData.AsSpan(
                            AnimationFrameCountOffset));
                animationIterations =
                    BinaryPrimitives.ReadUInt32BigEndian(
                        chunkData.AsSpan(
                            AnimationIterationsOffset));
                hasAnimationControl = true;

                if (declaredFrameCount == 0)
                {
                    throw new InvalidDataException(
                        "The APNG declares no animation frames.");
                }

                continue;
            }

            if (chunkType == FrameControlChunkType)
            {
                if (!hasAnimationControl
                    || (chunkData.Length
                        != FrameControlDataLength))
                {
                    throw new InvalidDataException(
                        "The PNG contains an invalid frame control chunk.");
                }

                FinishCurrentFrame();
                ValidateSequenceNumber(
                    chunkData,
                    ref expectedSequenceNumber);
                currentWidth = ReadPositiveInt32(
                    chunkData,
                    FrameWidthOffset,
                    "frame width");
                currentHeight = ReadPositiveInt32(
                    chunkData,
                    FrameHeightOffset,
                    "frame height");
                currentX = ReadNonNegativeInt32(
                    chunkData,
                    FrameHorizontalOffset,
                    "frame horizontal offset");
                currentY = ReadNonNegativeInt32(
                    chunkData,
                    FrameVerticalOffset,
                    "frame vertical offset");
                ValidateFrameBounds(
                    canvasWidth,
                    canvasHeight,
                    currentX,
                    currentY,
                    currentWidth,
                    currentHeight);
                currentDuration = ReadFrameDuration(
                    chunkData);
                currentDisposeOperation =
                    ReadDisposeOperation(
                        chunkData[
                            FrameDisposeOperationOffset]);
                currentBlendOperation =
                    ReadBlendOperation(
                        chunkData[
                            FrameBlendOperationOffset]);
                currentCompressedData = [];
                continue;
            }

            if (chunkType == ImageDataChunkType)
            {
                hasImageData = true;

                if (currentCompressedData is null)
                {
                    continue;
                }

                if ((currentCompressedData.Count > 0)
                    && !currentFrameUsesImageData)
                {
                    throw new InvalidDataException(
                        "An APNG frame cannot mix PNG image data with animation frame data.");
                }

                if (!currentFrameUsesImageData)
                {
                    ValidateDefaultFrameGeometry(
                        canvasWidth,
                        canvasHeight,
                        currentX,
                        currentY,
                        currentWidth,
                        currentHeight);
                    currentFrameUsesImageData = true;
                }

                currentCompressedData.Add(chunkData);
                continue;
            }

            if (chunkType == FrameDataChunkType)
            {
                hasImageData = true;

                if ((currentCompressedData is null)
                    || (chunkData.Length
                        <= FrameDataSequenceLength))
                {
                    throw new InvalidDataException(
                        "The APNG contains frame data without a frame control chunk.");
                }

                if (currentFrameUsesImageData)
                {
                    throw new InvalidDataException(
                        "An APNG frame cannot mix animation frame data with PNG image data.");
                }

                ValidateSequenceNumber(
                    chunkData,
                    ref expectedSequenceNumber);
                currentCompressedData.Add(
                    chunkData[FrameDataSequenceLength..]);
                continue;
            }

            if (chunkType == ImageEndChunkType)
            {
                if (chunkData.Length != 0)
                {
                    throw new InvalidDataException(
                        "The PNG image end chunk must be empty.");
                }

                FinishCurrentFrame();
                break;
            }
        }

        if (!hasAnimationControl)
        {
            throw new InvalidDataException(
                "The PNG does not contain animation control data.");
        }

        if (frames.Count != declaredFrameCount)
        {
            throw new InvalidDataException(
                $"The APNG contains {frames.Count} frames, but declares {declaredFrameCount}.");
        }

        return new ApngAnimationData(
            canvasWidth,
            canvasHeight,
            animationIterations,
            imageHeaderData,
            sharedChunks.AsReadOnly(),
            frames.AsReadOnly());
    }

    private static int ReadPositiveInt32(
        ReadOnlySpan<byte> data,
        int offset,
        string valueName)
    {
        uint value =
            BinaryPrimitives.ReadUInt32BigEndian(
                data[offset..]);

        if ((value == 0)
            || (value > int.MaxValue))
        {
            throw new InvalidDataException(
                $"The APNG {valueName} is invalid.");
        }

        return checked((int)value);
    }

    private static int ReadNonNegativeInt32(
        ReadOnlySpan<byte> data,
        int offset,
        string valueName)
    {
        uint value =
            BinaryPrimitives.ReadUInt32BigEndian(
                data[offset..]);

        if (value > int.MaxValue)
        {
            throw new InvalidDataException(
                $"The APNG {valueName} is invalid.");
        }

        return checked((int)value);
    }

    private static void ValidateFrameBounds(
        int canvasWidth,
        int canvasHeight,
        int x,
        int y,
        int width,
        int height)
    {
        if (((long)x + width > canvasWidth)
            || ((long)y + height > canvasHeight))
        {
            throw new InvalidDataException(
                "The APNG frame lies outside the image canvas.");
        }
    }

    private static void ValidateDefaultFrameGeometry(
        int canvasWidth,
        int canvasHeight,
        int x,
        int y,
        int width,
        int height)
    {
        if ((x != 0)
            || (y != 0)
            || (width != canvasWidth)
            || (height != canvasHeight))
        {
            throw new InvalidDataException(
                "An APNG frame stored in PNG image data must cover the full canvas.");
        }
    }

    private static void ValidateSequenceNumber(
        ReadOnlySpan<byte> chunkData,
        ref uint expectedSequenceNumber)
    {
        uint sequenceNumber =
            BinaryPrimitives.ReadUInt32BigEndian(
                chunkData[
                    FrameSequenceNumberOffset..]);

        if (sequenceNumber
            != expectedSequenceNumber)
        {
            throw new InvalidDataException(
                $"The APNG sequence number is {sequenceNumber}, expected {expectedSequenceNumber}.");
        }

        expectedSequenceNumber = checked(
            expectedSequenceNumber + 1);
    }

    private static TimeSpan ReadFrameDuration(
        ReadOnlySpan<byte> frameControlData)
    {
        ushort numerator =
            BinaryPrimitives.ReadUInt16BigEndian(
                frameControlData[
                    FrameDelayNumeratorOffset..]);
        ushort denominator =
            BinaryPrimitives.ReadUInt16BigEndian(
                frameControlData[
                    FrameDelayDenominatorOffset..]);
        ushort effectiveDenominator =
            denominator == 0
                ? DefaultDelayDenominator
                : denominator;
        double durationMilliseconds =
            MillisecondsPerSecond
            * numerator
            / effectiveDenominator;

        return ImageAnimationTiming.NormalizeFrameDuration(
            durationMilliseconds);
    }

    private static ApngDisposeOperation ReadDisposeOperation(
        byte value)
    {
        return value switch
        {
            0 => ApngDisposeOperation.None,
            1 => ApngDisposeOperation.Background,
            2 => ApngDisposeOperation.Previous,
            _ => throw new InvalidDataException(
                $"The APNG frame disposal operation '{value}' is invalid.")
        };
    }

    private static ApngBlendOperation ReadBlendOperation(
        byte value)
    {
        return value switch
        {
            0 => ApngBlendOperation.Source,
            1 => ApngBlendOperation.Over,
            _ => throw new InvalidDataException(
                $"The APNG frame blend operation '{value}' is invalid.")
        };
    }
}
