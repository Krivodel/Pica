using System.Text;

using Microsoft.Extensions.Logging;

using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace Pica.Viewer.Services;

internal sealed class ClipboardImageReader : IClipboardImageReader
{
    private readonly ClipboardImageFormatCatalog _formats;
    private readonly IPlatformClipboardSnapshotReader _platformReader;
    private readonly ExternalClipboardImageReader _externalReader;
    private readonly ILogger<ClipboardImageReader> _logger;
    private readonly IViewerUiDispatcher _dispatcher;

    public ClipboardImageReader(
        ClipboardImageFormatCatalog formats,
        IPlatformClipboardSnapshotReader platformReader,
        ExternalClipboardImageReader externalReader,
        ILogger<ClipboardImageReader> logger,
        IViewerUiDispatcher dispatcher)
    {
        _formats = formats ?? throw new ArgumentNullException(nameof(formats));
        _platformReader = platformReader ?? throw new ArgumentNullException(nameof(platformReader));
        _externalReader = externalReader ?? throw new ArgumentNullException(nameof(externalReader));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    }

    public async Task<IReadOnlyList<ClipboardImageInput>> ReadAsync(IClipboard clipboard, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(clipboard);
        ct.ThrowIfCancellationRequested();
        Task<IReadOnlyList<ClipboardImageInput>> task = await _dispatcher.InvokeAsync(
            () => CaptureAsync(clipboard, ct), ct).ConfigureAwait(false);

        return await task.ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<ClipboardImageInput>> CaptureAsync(IClipboard clipboard, CancellationToken ct)
    {
        ClipboardDataSnapshot snapshot = new();

        try
        {
            try
            {
                using ClipboardDataSnapshot native = await _platformReader.ReadAsync(ct);
                native.TransferTo(snapshot);
                snapshot.SequenceNumber = native.SequenceNumber;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Native clipboard capture failed; using Avalonia data");
            }

            try
            {
                using IAsyncDataTransfer? transfer = await clipboard.TryGetDataAsync();

                if (transfer is not null)
                {
                    using ClipboardDataSnapshot avalonia = new();
                    await CaptureTransferAsync(transfer, avalonia, ct);

                    if (!OperatingSystem.IsWindows() || (snapshot.SequenceNumber is null)
                        || (snapshot.SequenceNumber == WindowsClipboardAccess.GetClipboardSequenceNumber()))
                    {
                        avalonia.TransferTo(snapshot);
                    }
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Avalonia clipboard capture failed; using the native snapshot");
            }

            List<ClipboardImageInput> candidates = [];
            candidates.AddRange(snapshot.Files);
            candidates.AddRange(snapshot.Images);
            candidates.AddRange(snapshot.Rasters);
            AddTextCandidates(snapshot.Text, candidates);
            snapshot.Files.Clear();
            snapshot.Images.Clear();
            snapshot.Rasters.Clear();

            foreach (ClipboardImageInput overflow in candidates.Skip(ClipboardImageLimits.MaximumCandidates))
            {
                overflow.Dispose();
            }

            return candidates.Take(ClipboardImageLimits.MaximumCandidates).ToArray();
        }
        finally
        {
            snapshot.Dispose();
        }
    }

    private async Task CaptureTransferAsync(
        IAsyncDataTransfer transfer,
        ClipboardDataSnapshot snapshot,
        CancellationToken ct)
    {
        IReadOnlyList<IStorageItem>? files = await transfer.TryGetFilesAsync();

        foreach (IStorageFile file in files?.OfType<IStorageFile>().Take(ClipboardImageLimits.MaximumCandidates)
                 ?? Enumerable.Empty<IStorageFile>())
        {
            ct.ThrowIfCancellationRequested();

            if (file.TryGetLocalPath() is { } path)
            {
                if (!snapshot.Files.Any(input => string.Equals(input.FilePath, path, StringComparison.OrdinalIgnoreCase)))
                {
                    snapshot.Files.Add(ClipboardImageInput.FromFile(path));
                }
            }
            else
            {
                await using Stream stream = await file.OpenReadAsync();
                byte[] bytes = await ImageStreamBuffer.ReadAllBytesAsync(
                    stream, ClipboardImageLimits.MaximumInputBytes, ct);
                snapshot.Files.Add(ClipboardImageInput.FromBytes(file.Name, bytes));
            }
        }

        foreach (IAsyncDataTransferItem item in transfer.Items)
        {
            foreach (DataFormat format in item.Formats.Where(format => _formats.IsImageFormat(format)
                         || ClipboardImageLinkFormats.Formats.ContainsKey(format.Identifier) || (format == DataFormat.Text)))
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    object? value = await item.TryGetRawAsync(format);

                    if (_formats.IsImageFormat(format) && (value is byte[] bytes)
                        && (bytes.Length <= ClipboardImageLimits.MaximumInputBytes))
                    {
                        snapshot.Images.Add(ClipboardImageInput.FromBytes(_formats.GetFileName(format.Identifier), bytes));
                    }
                    else if (_formats.IsImageFormat(format) && (value is Stream stream))
                    {
                        byte[] content = await ImageStreamBuffer.ReadAllBytesAsync(stream, ClipboardImageLimits.MaximumInputBytes, ct);
                        snapshot.Images.Add(ClipboardImageInput.FromBytes(_formats.GetFileName(format.Identifier), content));
                    }
                    else if (value is string text)
                    {
                        snapshot.Text.Add(text.TrimEnd('\0'));
                    }
                    else if (!_formats.IsImageFormat(format) && (value is byte[] textBytes))
                    {
                        Encoding encoding = ClipboardImageLinkFormats.Formats.GetValueOrDefault(format.Identifier, Encoding.UTF8);
                        snapshot.Text.Add(encoding.GetString(textBytes).TrimEnd('\0'));
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "A clipboard representation could not be captured");
                }
            }
        }

        using Bitmap? bitmap = await transfer.TryGetBitmapAsync();

        if (bitmap is not null)
        {
            const int BitmapBytesPerPixel = 4;

            if ((long)bitmap.PixelSize.Width * bitmap.PixelSize.Height * BitmapBytesPerPixel
                > ClipboardImageLimits.MaximumInputBytes)
            {
                return;
            }

            Bitmap copy = await Task.Run(() => BitmapPixelCopy.CreateCopy(bitmap), ct);
            snapshot.Rasters.Add(ClipboardImageInput.FromBitmap(copy));
        }
    }

    private void AddTextCandidates(IReadOnlyList<string> values, List<ClipboardImageInput> candidates)
    {
        List<ClipboardImageInput> links = [];
        HashSet<string> addresses = new(StringComparer.Ordinal);

        foreach (string value in values.Distinct(StringComparer.Ordinal))
        {
            foreach (string line in value.Split(new char[] { '\r', '\n', '\0' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string path = line.Trim().Trim('"', '\'');

                if (Uri.TryCreate(path, UriKind.Absolute, out Uri? fileUri) && fileUri.IsFile)
                {
                    path = fileUri.LocalPath;
                }

                if (File.Exists(path))
                {
                    candidates.Add(ClipboardImageInput.FromFile(path));
                }

                AddLink(line, null, links, addresses);

                int urlStart = line.IndexOf(":http", StringComparison.OrdinalIgnoreCase);

                if (line.StartsWith("image/", StringComparison.OrdinalIgnoreCase) && (urlStart > 0))
                {
                    AddLink(line[(urlStart + 1)..], null, links, addresses);
                }
            }

            Uri? source = GetSourceUri(value);
            string? baseValue = HtmlImageSourceExtractor.ExtractBase(value);

            if ((baseValue is not null)
                && (Uri.TryCreate(baseValue, UriKind.Absolute, out Uri? baseUri)
                    || ((source is not null) && Uri.TryCreate(source, baseValue, out baseUri))))
            {
                source = baseUri;
            }

            foreach (string imageSource in HtmlImageSourceExtractor.ExtractSources(value))
            {
                if ((Uri.TryCreate(imageSource, UriKind.Absolute, out Uri? imageUri)
                    || ((source is not null) && Uri.TryCreate(source, imageSource, out imageUri)))
                    && (imageUri is { IsFile: true }))
                {
                    candidates.Add(ClipboardImageInput.FromFile(imageUri.LocalPath));
                }
                else
                {
                    AddLink(imageSource, source, links, addresses);
                }
            }
        }

        candidates.AddRange(links);
    }

    private static Uri? GetSourceUri(string html)
    {
        foreach (string line in html.Split('\n'))
        {
            const string SourceUrlPrefix = "SourceURL:";

            if (line.StartsWith(SourceUrlPrefix, StringComparison.OrdinalIgnoreCase)
                && Uri.TryCreate(line[SourceUrlPrefix.Length..].Trim(), UriKind.Absolute, out Uri? uri))
            {
                return uri;
            }
        }

        return null;
    }

    private void AddLink(string value, Uri? source, List<ClipboardImageInput> links, HashSet<string> addresses)
    {
        string text = value.Trim();

        if (text.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            if (addresses.Add(text))
            {
                links.Add(_externalReader.CreateDataInput(text));
            }

            return;
        }

        Uri? uri;
        bool parsed = Uri.TryCreate(text, UriKind.Absolute, out uri)
            || ((source is not null) && Uri.TryCreate(source, text, out uri));

        if (parsed && (uri is not null)
            && ((uri.Scheme == Uri.UriSchemeHttp) || (uri.Scheme == Uri.UriSchemeHttps)))
        {
            if (addresses.Add(uri.AbsoluteUri))
            {
                links.Add(_externalReader.CreateInput(uri));
            }
        }
    }
}
