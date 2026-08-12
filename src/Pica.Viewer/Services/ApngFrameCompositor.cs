namespace Pica.Viewer.Services;

internal sealed class ApngFrameCompositor
{
    private const int BytesPerPixel = 4;
    private const int AlphaChannelOffset = 3;
    private const int MaximumChannelValue = 255;
    private const int RoundingOffset = 127;

    private readonly int _canvasWidth;
    private readonly byte[] _canvas;
    private byte[]? _previousSnapshot;
    private ApngFrameData? _previousFrame;
    private int _composedFrameIndex = -1;

    internal ApngFrameCompositor(
        int canvasWidth,
        int canvasHeight)
    {
        if (canvasWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(canvasWidth),
                canvasWidth,
                "The APNG canvas width must be positive.");
        }

        if (canvasHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(canvasHeight),
                canvasHeight,
                "The APNG canvas height must be positive.");
        }

        _canvasWidth = canvasWidth;
        _canvas = new byte[
            checked(
                canvasWidth
                * canvasHeight
                * BytesPerPixel)];
    }

    internal byte[] Compose(
        int frameIndex,
        ApngFrameData frame,
        byte[] framePixels,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(frame);
        ArgumentNullException.ThrowIfNull(framePixels);

        if (frameIndex != _composedFrameIndex + 1)
        {
            throw new InvalidOperationException(
                $"APNG frames must be composed in sequence. Received frame {frameIndex} after {_composedFrameIndex}.");
        }

        int expectedPixelLength = checked(
            frame.Width
            * frame.Height
            * BytesPerPixel);

        if (framePixels.Length
            != expectedPixelLength)
        {
            throw new InvalidDataException(
                $"The APNG frame pixel buffer has length {framePixels.Length}, expected {expectedPixelLength}.");
        }

        ApplyPreviousDisposal(ct);
        _previousSnapshot =
            frame.DisposeOperation
                == ApngDisposeOperation.Previous
                ? CopyCanvasArea(
                    frame,
                    ct)
                : null;

        if (frame.BlendOperation
            == ApngBlendOperation.Source)
        {
            CopySource(
                frame,
                framePixels,
                ct);
        }
        else
        {
            BlendOver(
                frame,
                framePixels,
                ct);
        }

        _previousFrame = frame;
        _composedFrameIndex = frameIndex;

        return (byte[])_canvas.Clone();
    }

    internal void Reset()
    {
        Array.Clear(_canvas);
        _previousSnapshot = null;
        _previousFrame = null;
        _composedFrameIndex = -1;
    }

    private void ApplyPreviousDisposal(
        CancellationToken ct)
    {
        if (_previousFrame is null)
        {
            return;
        }

        if (_previousFrame.DisposeOperation
            == ApngDisposeOperation.Background)
        {
            ClearCanvasArea(
                _previousFrame,
                ct);
            return;
        }

        if (_previousFrame.DisposeOperation
            != ApngDisposeOperation.Previous)
        {
            return;
        }

        byte[] snapshot = _previousSnapshot
            ?? throw new InvalidDataException(
                "The APNG previous-frame snapshot is not available.");
        WriteCanvasArea(
            _previousFrame,
            snapshot,
            ct);
    }

    private byte[] CopyCanvasArea(
        ApngFrameData frame,
        CancellationToken ct)
    {
        int rowLength = checked(
            frame.Width
            * BytesPerPixel);
        byte[] result = new byte[
            checked(rowLength * frame.Height)];

        for (int rowIndex = 0;
            rowIndex < frame.Height;
            rowIndex++)
        {
            ct.ThrowIfCancellationRequested();
            Buffer.BlockCopy(
                _canvas,
                GetCanvasOffset(
                    frame.X,
                    frame.Y + rowIndex),
                result,
                rowIndex * rowLength,
                rowLength);
        }

        return result;
    }

    private void WriteCanvasArea(
        ApngFrameData frame,
        byte[] source,
        CancellationToken ct)
    {
        int rowLength = checked(
            frame.Width
            * BytesPerPixel);

        for (int rowIndex = 0;
            rowIndex < frame.Height;
            rowIndex++)
        {
            ct.ThrowIfCancellationRequested();
            Buffer.BlockCopy(
                source,
                rowIndex * rowLength,
                _canvas,
                GetCanvasOffset(
                    frame.X,
                    frame.Y + rowIndex),
                rowLength);
        }
    }

    private void ClearCanvasArea(
        ApngFrameData frame,
        CancellationToken ct)
    {
        int rowLength = checked(
            frame.Width
            * BytesPerPixel);

        for (int rowIndex = 0;
            rowIndex < frame.Height;
            rowIndex++)
        {
            ct.ThrowIfCancellationRequested();
            Array.Clear(
                _canvas,
                GetCanvasOffset(
                    frame.X,
                    frame.Y + rowIndex),
                rowLength);
        }
    }

    private void CopySource(
        ApngFrameData frame,
        byte[] framePixels,
        CancellationToken ct)
    {
        int rowLength = checked(
            frame.Width
            * BytesPerPixel);

        for (int rowIndex = 0;
            rowIndex < frame.Height;
            rowIndex++)
        {
            ct.ThrowIfCancellationRequested();
            Buffer.BlockCopy(
                framePixels,
                rowIndex * rowLength,
                _canvas,
                GetCanvasOffset(
                    frame.X,
                    frame.Y + rowIndex),
                rowLength);
        }
    }

    private void BlendOver(
        ApngFrameData frame,
        byte[] framePixels,
        CancellationToken ct)
    {
        int frameRowLength = checked(
            frame.Width
            * BytesPerPixel);

        for (int rowIndex = 0;
            rowIndex < frame.Height;
            rowIndex++)
        {
            ct.ThrowIfCancellationRequested();
            int sourceOffset =
                rowIndex
                * frameRowLength;
            int destinationOffset =
                GetCanvasOffset(
                    frame.X,
                    frame.Y + rowIndex);

            for (int columnIndex = 0;
                columnIndex < frame.Width;
                columnIndex++)
            {
                BlendPixel(
                    framePixels,
                    sourceOffset,
                    destinationOffset);
                sourceOffset += BytesPerPixel;
                destinationOffset += BytesPerPixel;
            }
        }
    }

    private void BlendPixel(
        byte[] source,
        int sourceOffset,
        int destinationOffset)
    {
        int sourceAlpha =
            source[
                sourceOffset
                + AlphaChannelOffset];

        if (sourceAlpha == 0)
        {
            return;
        }

        if (sourceAlpha == MaximumChannelValue)
        {
            Buffer.BlockCopy(
                source,
                sourceOffset,
                _canvas,
                destinationOffset,
                BytesPerPixel);
            return;
        }

        int inverseSourceAlpha =
            MaximumChannelValue
            - sourceAlpha;

        for (int channelIndex = 0;
            channelIndex < BytesPerPixel;
            channelIndex++)
        {
            int composedValue =
                source[sourceOffset + channelIndex]
                + ((_canvas[
                        destinationOffset
                        + channelIndex]
                    * inverseSourceAlpha
                    + RoundingOffset)
                    / MaximumChannelValue);
            _canvas[
                destinationOffset
                + channelIndex] =
                    checked((byte)composedValue);
        }
    }

    private int GetCanvasOffset(
        int x,
        int y)
    {
        return checked(
            ((y * _canvasWidth) + x)
            * BytesPerPixel);
    }
}
