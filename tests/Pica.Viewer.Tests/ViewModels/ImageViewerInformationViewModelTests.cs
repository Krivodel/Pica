using FluentAssertions;
using Xunit;

using Pica.Protocol;
using Pica.Viewer.Services;
using Pica.Viewer.Tests.TestDoubles;
using Pica.Viewer.ViewModels;

namespace Pica.Viewer.Tests.ViewModels;

public sealed class ImageViewerInformationViewModelTests
{
    private static readonly Guid ItemId =
        Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTime ModificationDate =
        new(2026, 7, 29, 12, 30, 0, DateTimeKind.Local);

    [Fact]
    public void Constructor_WithSelectedImage_FormatsEnabledInformation()
    {
        PicaImageItem item = CreateItem();
        ImageViewerSession session = CreateSession(item);
        StubImagePresentationInfo presentation = new();
        presentation.SetPresentation(
            item,
            new ImageDimensions(640, 480));
        RecordingImageFileMetadataProvider metadataProvider = new()
        {
            ModificationDate = ModificationDate
        };
        ImageViewerSettingsViewModel settings = CreateSettings(
            session,
            showModificationDate: true);

        ImageViewerInformationViewModel viewModel = new(
            session,
            presentation,
            metadataProvider,
            settings,
            new RecordingViewModelErrorHandler());

        viewModel.Start();

        viewModel.Information.Should().Be(
            $"image.png · 29.07.2026 12:30 · 640×480");
        metadataProvider.CallCount.Should().Be(1);
        viewModel.Dispose();
        settings.Dispose();
    }

    [Fact]
    public void PresentationChanged_WithNewDimensions_UpdatesInformation()
    {
        PicaImageItem item = CreateItem();
        ImageViewerSession session = CreateSession(item);
        StubImagePresentationInfo presentation = new();
        ImageViewerSettingsViewModel settings = CreateSettings(
            session,
            showModificationDate: false);
        ImageViewerInformationViewModel viewModel = new(
            session,
            presentation,
            new RecordingImageFileMetadataProvider(),
            settings,
            new RecordingViewModelErrorHandler());
        viewModel.Start();

        presentation.SetPresentation(
            item,
            new ImageDimensions(1920, 1080));

        viewModel.Information.Should().Be(
            "image.png · 1920×1080");
        viewModel.Dispose();
        settings.Dispose();
    }

    [Fact]
    public void SelectedChannelChanged_WithChannel_UpdatesInformation()
    {
        PicaImageItem item = CreateItem();
        ImageViewerSession session = CreateSession(item);
        StubImagePresentationInfo presentation = new();
        presentation.SetPresentation(
            item,
            new ImageDimensions(640, 480));
        ImageViewerSettingsViewModel settings = CreateSettings(
            session,
            showModificationDate: false);
        ImageViewerInformationViewModel viewModel = new(
            session,
            presentation,
            new RecordingImageFileMetadataProvider(),
            settings,
            new RecordingViewModelErrorHandler());
        viewModel.Start();

        session.SelectChannelImageMode();

        viewModel.Information.Should().Be(
            "image.png · 640×480 · Канал R");
        viewModel.Dispose();
        settings.Dispose();
    }

    [Fact]
    public async Task InformationSettingChanged_WithDisabledName_UpdatesInformation()
    {
        PicaImageItem item = CreateItem();
        ImageViewerSession session = CreateSession(item);
        StubImagePresentationInfo presentation = new();
        presentation.SetPresentation(
            item,
            new ImageDimensions(640, 480));
        ImageViewerSettingsViewModel settings = CreateSettings(
            session,
            showModificationDate: false);
        ImageViewerInformationViewModel viewModel = new(
            session,
            presentation,
            new RecordingImageFileMetadataProvider(),
            settings,
            new RecordingViewModelErrorHandler());
        viewModel.Start();

        await settings.ChangeShowImageNameCommand.ExecuteAsync(false);

        viewModel.Information.Should().Be("png · 640×480");
        viewModel.Dispose();
        settings.Dispose();
    }

    [Fact]
    public void MetadataLoad_WhenSelectionChanges_IgnoresStaleResult()
    {
        PicaImageItem firstItem = CreateItem();
        PicaImageItem secondItem = new(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "C:\\Images\\second.png",
            "second.png");
        ImageViewerSession session = CreateSession(
            new List<PicaImageItem> { firstItem, secondItem },
            firstItem.Id);
        StubImagePresentationInfo presentation = new();
        ControlledImageFileMetadataProvider metadataProvider = new();
        ImageViewerSettingsViewModel settings = CreateSettings(
            session,
            showModificationDate: true);
        ImageViewerInformationViewModel viewModel = new(
            session,
            presentation,
            metadataProvider,
            settings,
            new RecordingViewModelErrorHandler());

        viewModel.Start();
        session.Navigate(1);
        metadataProvider.Complete(
            firstItem.FilePath,
            ModificationDate);
        metadataProvider.Complete(
            secondItem.FilePath,
            ModificationDate.AddDays(1));

        metadataProvider.RequestedFilePaths.Should().Equal(
            firstItem.FilePath,
            secondItem.FilePath);
        viewModel.Information.Should().Be(
            "second.png · 30.07.2026 12:30");
        viewModel.IsLoading.Should().BeFalse();
        viewModel.Dispose();
        settings.Dispose();
    }

    [Fact]
    public void MetadataLoad_WhenProviderFails_SetsSafeError()
    {
        InvalidOperationException exception = new(
            "Metadata failed.");
        PicaImageItem item = CreateItem();
        ImageViewerSession session = CreateSession(item);
        StubImagePresentationInfo presentation = new();
        RecordingImageFileMetadataProvider metadataProvider = new()
        {
            ExceptionToThrow = exception
        };
        RecordingViewModelErrorHandler errorHandler = new();
        ImageViewerSettingsViewModel settings = CreateSettings(
            session,
            showModificationDate: true);

        ImageViewerInformationViewModel viewModel = new(
            session,
            presentation,
            metadataProvider,
            settings,
            errorHandler);

        viewModel.Start();

        errorHandler.LastException.Should().Be(exception);
        errorHandler.LastOperationName.Should().Be(
            "LoadMetadataAsync");
        viewModel.ErrorMessage.Should().Be(
            RecordingViewModelErrorHandler.SafeMessage);
        viewModel.HasErrorMessage.Should().BeTrue();
        viewModel.IsLoading.Should().BeFalse();
        viewModel.Dispose();
        settings.Dispose();
    }

    private static PicaImageItem CreateItem()
    {
        return new PicaImageItem(
            ItemId,
            "C:\\Images\\image.png",
            "image.png");
    }

    private static ImageViewerSession CreateSession(PicaImageItem item)
    {
        return CreateSession(
            new List<PicaImageItem> { item },
            item.Id);
    }

    private static ImageViewerSession CreateSession(
        IReadOnlyList<PicaImageItem> items,
        Guid selectedItemId)
    {
        PicaViewerRequest request = new(
            items,
            selectedItemId);

        return new ImageViewerSession(request, true);
    }

    private static ImageViewerSettingsViewModel CreateSettings(
        ImageViewerSession session,
        bool showModificationDate)
    {
        ImageViewerState state = new()
        {
            ShowImageName = true,
            ShowImageFormat = true,
            ShowImageResolution = true,
            ShowImageModificationDate = showModificationDate
        };
        ViewerWindowPlacement placement = new(
            false,
            null,
            null,
            null,
            null);
        ViewerWindowPlacementProvider placementProvider =
            new(placement);

        return new ImageViewerSettingsViewModel(
            new RecordingImageViewerStateService(state),
            session,
            new RecordingImageLoadingSettings(),
            placementProvider,
            new RecordingViewModelErrorHandler(),
            state);
    }
}
