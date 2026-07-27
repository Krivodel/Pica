using Microsoft.Extensions.Logging;

using Pica.Protocol;
using Pica.Viewer.Services;

namespace Pica.Desktop.Services;

public sealed class ViewerActionDispatcher : IViewerActionDispatcher
{
    private readonly PicaHostConnection? _hostConnection;
    private readonly IImageFormatRegistry _formatRegistry;
    private readonly ILogger<ViewerActionDispatcher> _logger;
    private readonly string? _payloadDirectory;

    public ViewerActionDispatcher(
        PicaHostConnection? hostConnection,
        IImageFormatRegistry formatRegistry,
        ILogger<ViewerActionDispatcher> logger,
        string? payloadDirectory)
    {
        _hostConnection = hostConnection;
        _formatRegistry = formatRegistry ?? throw new ArgumentNullException(nameof(formatRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _payloadDirectory = payloadDirectory;
    }

    public async Task DispatchCurrentImageAsync(
        PicaActionDefinition action,
        PicaImageItem item,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(item);

        PicaActionInvocation invocation = new(
            action.Id,
            item.Id,
            item.FilePath,
            item.FileName,
            _formatRegistry.GetContentType(item.FileName));
        await SendAsync(invocation, ct).ConfigureAwait(false);
    }

    public async Task DispatchSelectionAsync(
        PicaActionDefinition action,
        PicaImageItem item,
        byte[] pngContent,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(pngContent);

        if (string.IsNullOrWhiteSpace(_payloadDirectory))
        {
            _logger.LogWarning(
                "Pica selection action {ActionId} for item {ItemId} was ignored because no payload directory is available",
                action.Id,
                item.Id);

            return;
        }

        Directory.CreateDirectory(_payloadDirectory);
        string filePath = Path.Combine(
            _payloadDirectory,
            $"{Guid.NewGuid():N}{PicaImageFormats.PngExtension}");
        await using FileStream stream = new(
            filePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous);
        await stream.WriteAsync(pngContent, ct).ConfigureAwait(false);
        await stream.FlushAsync(ct).ConfigureAwait(false);

        PicaActionInvocation invocation = new(
            action.Id,
            item.Id,
            filePath,
            PicaImageFormats.SelectionFileName,
            PicaImageFormats.PngContentType);
        await SendAsync(invocation, ct).ConfigureAwait(false);
    }

    private async Task SendAsync(PicaActionInvocation invocation, CancellationToken ct)
    {
        if (_hostConnection is null)
        {
            _logger.LogDebug(
                "Pica action {ActionId} for item {ItemId} was ignored in standalone mode",
                invocation.ActionId,
                invocation.ItemId);

            return;
        }

        try
        {
            await _hostConnection.SendAsync(invocation, ct).ConfigureAwait(false);
            _logger.LogInformation(
                "Delivered Pica action {ActionId} for item {ItemId} to the host",
                invocation.ActionId,
                invocation.ItemId);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "The Pica host disconnected before action {ActionId} was delivered", invocation.ActionId);
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogWarning(ex, "The Pica host connection was closed before action {ActionId} was delivered", invocation.ActionId);
        }
    }
}
