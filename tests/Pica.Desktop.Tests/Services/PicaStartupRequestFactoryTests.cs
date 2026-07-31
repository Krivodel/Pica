using Microsoft.Extensions.Logging.Abstractions;

using FluentAssertions;
using Xunit;

using Pica.Desktop.Services;
using Pica.Desktop.Tests.TestDoubles;
using Pica.Tests.Common;
using Pica.Viewer.Services;

namespace Pica.Desktop.Tests.Services;

public sealed class PicaStartupRequestFactoryTests
{
    [Fact]
    public async Task CreateAsync_WithFileArguments_CreatesStandaloneRequest()
    {
        using PicaTemporaryDirectory temporaryDirectory = new();
        string filePath = Path.Combine(temporaryDirectory.DirectoryPath, "image.png");
        await CreateImageAsync(filePath, [1, 2, 3]);
        PicaStartupRequestFactory factory = CreateFactory();

        PicaStartupRequest request = await factory.CreateAsync(
            [filePath],
            CancellationToken.None);

        request.HostConnection.Should().BeNull();
        request.ViewerRequest.Items.Should().ContainSingle();
        request.ViewerRequest.Items[0].FilePath.Should().Be(Path.GetFullPath(filePath));
        request.ViewerRequest.Items[0].FileName.Should().Be("image.png");
        request.ViewerRequest.SelectedItemId.Should().Be(request.ViewerRequest.Items[0].Id);
        request.ViewerRequest.Actions.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_WithSingleImage_IncludesDirectoryImagesSortedByNewestFirst()
    {
        using PicaTemporaryDirectory temporaryDirectory = new();
        string firstImagePath = Path.Combine(temporaryDirectory.DirectoryPath, "01.jpg");
        string selectedImagePath = Path.Combine(temporaryDirectory.DirectoryPath, "02.png");
        string iconPath = Path.Combine(temporaryDirectory.DirectoryPath, "03.ico");
        string unsupportedFilePath = Path.Combine(temporaryDirectory.DirectoryPath, "notes.txt");
        await CreateImageAsync(firstImagePath, [1]);
        await CreateImageAsync(selectedImagePath, [2]);
        await CreateImageAsync(iconPath, [3]);
        await File.WriteAllTextAsync(unsupportedFilePath, "text");
        SetLastWriteTimesNewestFirst(selectedImagePath, iconPath, firstImagePath);
        PicaStartupRequestFactory factory = CreateFactory();

        PicaStartupRequest request = await factory.CreateAsync(
            [selectedImagePath],
            CancellationToken.None);

        request.ViewerRequest.Items.Select(item => item.FileName)
            .Should()
            .Equal("02.png", "03.ico", "01.jpg");
        request.ViewerRequest.SelectedItemId.Should().Be(
            request.ViewerRequest.Items.Single(item => item.FileName == "02.png").Id);
    }

    [Fact]
    public async Task CreateAsync_WithSelectedIcon_IncludesAllSupportedDirectoryImages()
    {
        using PicaTemporaryDirectory temporaryDirectory = new();
        string pngPath = Path.Combine(temporaryDirectory.DirectoryPath, "01.png");
        string selectedIconPath = Path.Combine(temporaryDirectory.DirectoryPath, "02.ico");
        await CreateImageAsync(pngPath, [1]);
        await CreateImageAsync(selectedIconPath, [2]);
        SetLastWriteTimesNewestFirst(pngPath, selectedIconPath);
        PicaStartupRequestFactory factory = CreateFactory();

        PicaStartupRequest request = await factory.CreateAsync(
            [selectedIconPath],
            CancellationToken.None);

        request.ViewerRequest.Items.Select(item => item.FileName)
            .Should()
            .Equal("01.png", "02.ico");
        request.ViewerRequest.SelectedItemId.Should().Be(
            request.ViewerRequest.Items.Single(item => item.FileName == "02.ico").Id);
    }

    [Fact]
    public async Task CreateAsync_WithMultipleImagesFromSameDirectory_IncludesAllSupportedDirectoryImages()
    {
        using PicaTemporaryDirectory temporaryDirectory = new();
        string firstRequestedImagePath = Path.Combine(temporaryDirectory.DirectoryPath, "01.jpg");
        string secondRequestedImagePath = Path.Combine(temporaryDirectory.DirectoryPath, "02.jpg");
        string otherFormatImagePath = Path.Combine(temporaryDirectory.DirectoryPath, "03.png");
        await CreateImageAsync(firstRequestedImagePath, [1]);
        await CreateImageAsync(secondRequestedImagePath, [2]);
        await CreateImageAsync(otherFormatImagePath, [3]);
        SetLastWriteTimesNewestFirst(
            firstRequestedImagePath,
            secondRequestedImagePath,
            otherFormatImagePath);
        PicaStartupRequestFactory factory = CreateFactory();

        PicaStartupRequest request = await factory.CreateAsync(
            [firstRequestedImagePath, secondRequestedImagePath],
            CancellationToken.None);

        request.ViewerRequest.Items.Select(item => item.FileName)
            .Should()
            .Equal("01.jpg", "02.jpg", "03.png");
        request.ViewerRequest.SelectedItemId.Should().Be(
            request.ViewerRequest.Items.Single(item => item.FileName == "01.jpg").Id);
    }

    [Fact]
    public async Task CreateAsync_WithExplorerOrder_UsesViewOrder()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using PicaTemporaryDirectory temporaryDirectory = new();
        string firstImagePath = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "01.jpg");
        string selectedImagePath = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "02.png");
        string thirdImagePath = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "03.ico");
        string unsupportedFilePath = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "notes.txt");
        await CreateImageAsync(firstImagePath, [1]);
        await CreateImageAsync(selectedImagePath, [2]);
        await CreateImageAsync(thirdImagePath, [3]);
        await File.WriteAllTextAsync(unsupportedFilePath, "text");
        SetLastWriteTimesNewestFirst(
            selectedImagePath,
            thirdImagePath,
            firstImagePath);
        long sourceWindowHandle = 42L;
        RecordingWindowsExplorerItemOrderProvider orderProvider = new(
            [
                thirdImagePath,
                unsupportedFilePath,
                firstImagePath,
                selectedImagePath
            ]);
        PicaStartupRequestFactory factory = CreateFactory(orderProvider);

        PicaStartupRequest request = await factory.CreateAsync(
            [selectedImagePath],
            sourceWindowHandle,
            CancellationToken.None);

        request.ViewerRequest.Items.Select(item => item.FileName)
            .Should()
            .Equal("03.ico", "01.jpg", "02.png");
        orderProvider.DirectoryPath.Should().Be(
            temporaryDirectory.DirectoryPath);
        orderProvider.SourceWindowHandle.Should().Be(sourceWindowHandle);
    }

    [Fact]
    public async Task CreateAsync_WithImageMissingFromExplorerView_AppendsUsingFallbackOrder()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using PicaTemporaryDirectory temporaryDirectory = new();
        string selectedImagePath = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "01.png");
        string orderedImagePath = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "02.jpg");
        string missingImagePath = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "03.ico");
        await CreateImageAsync(selectedImagePath, [1]);
        await CreateImageAsync(orderedImagePath, [2]);
        await CreateImageAsync(missingImagePath, [3]);
        SetLastWriteTimesNewestFirst(
            missingImagePath,
            selectedImagePath,
            orderedImagePath);
        RecordingWindowsExplorerItemOrderProvider orderProvider = new(
            [orderedImagePath, selectedImagePath]);
        PicaStartupRequestFactory factory = CreateFactory(orderProvider);

        PicaStartupRequest request = await factory.CreateAsync(
            [selectedImagePath],
            42L,
            CancellationToken.None);

        request.ViewerRequest.Items.Select(item => item.FileName)
            .Should()
            .Equal("02.jpg", "01.png", "03.ico");
    }

    [Fact]
    public async Task CreateAsync_WithSelectedImageMissingFromExplorerView_UsesFallbackOrder()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using PicaTemporaryDirectory temporaryDirectory = new();
        string selectedImagePath = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "01.png");
        string adjacentImagePath = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "02.jpg");
        await CreateImageAsync(selectedImagePath, [1]);
        await CreateImageAsync(adjacentImagePath, [2]);
        SetLastWriteTimesNewestFirst(
            selectedImagePath,
            adjacentImagePath);
        RecordingWindowsExplorerItemOrderProvider orderProvider = new(
            [adjacentImagePath]);
        PicaStartupRequestFactory factory = CreateFactory(orderProvider);

        PicaStartupRequest request = await factory.CreateAsync(
            [selectedImagePath],
            42L,
            CancellationToken.None);

        request.ViewerRequest.Items.Select(item => item.FileName)
            .Should()
            .Equal("01.png", "02.jpg");
    }

    [Fact]
    public async Task CreateAsync_WithUnavailableExplorerOrder_UsesFallbackOrder()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        using PicaTemporaryDirectory temporaryDirectory = new();
        string selectedImagePath = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "01.png");
        string adjacentImagePath = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "02.jpg");
        await CreateImageAsync(selectedImagePath, [1]);
        await CreateImageAsync(adjacentImagePath, [2]);
        SetLastWriteTimesNewestFirst(
            adjacentImagePath,
            selectedImagePath);
        RecordingWindowsExplorerItemOrderProvider orderProvider = new(null);
        PicaStartupRequestFactory factory = CreateFactory(orderProvider);

        PicaStartupRequest request = await factory.CreateAsync(
            [selectedImagePath],
            42L,
            CancellationToken.None);

        request.ViewerRequest.Items.Select(item => item.FileName)
            .Should()
            .Equal("02.jpg", "01.png");
        orderProvider.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task CreateAsync_WithoutSourceWindowHandle_DoesNotReadExplorerOrder()
    {
        using PicaTemporaryDirectory temporaryDirectory = new();
        string selectedImagePath = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "01.png");
        await CreateImageAsync(selectedImagePath, [1]);
        RecordingWindowsExplorerItemOrderProvider orderProvider = new(
            [selectedImagePath]);
        PicaStartupRequestFactory factory = CreateFactory(orderProvider);

        await factory.CreateAsync(
            [selectedImagePath],
            CancellationToken.None);

        orderProvider.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task CreateAsync_WithMultipleImageArguments_DoesNotExpandDirectories()
    {
        using PicaTemporaryDirectory firstTemporaryDirectory = new();
        using PicaTemporaryDirectory secondTemporaryDirectory = new();
        string selectedImagePath = Path.Combine(
            firstTemporaryDirectory.DirectoryPath,
            "selected.png");
        string unrequestedImagePath = Path.Combine(
            firstTemporaryDirectory.DirectoryPath,
            "unrequested.png");
        string requestedImagePath = Path.Combine(
            secondTemporaryDirectory.DirectoryPath,
            "requested.jpg");
        await CreateImageAsync(selectedImagePath, [1]);
        await CreateImageAsync(unrequestedImagePath, [2]);
        await CreateImageAsync(requestedImagePath, [3]);
        RecordingWindowsExplorerItemOrderProvider orderProvider = new(
            [unrequestedImagePath]);
        PicaStartupRequestFactory factory = CreateFactory(orderProvider);

        PicaStartupRequest request = await factory.CreateAsync(
            [selectedImagePath, requestedImagePath],
            42L,
            CancellationToken.None);

        request.ViewerRequest.Items.Select(item => item.FilePath)
            .Should()
            .Equal(Path.GetFullPath(selectedImagePath), Path.GetFullPath(requestedImagePath));
        orderProvider.CallCount.Should().Be(0);
    }

    private static void SetLastWriteTimesNewestFirst(params string[] paths)
    {
        DateTime newestDate = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        for (int index = 0; index < paths.Length; index++)
        {
            File.SetLastWriteTimeUtc(paths[index], newestDate.AddDays(-index));
        }
    }

    [Theory]
    [InlineData(PicaImageFormats.AvifExtension)]
    [InlineData(PicaImageFormats.HeicExtension)]
    [InlineData(PicaImageFormats.HeifExtension)]
    [InlineData(PicaImageFormats.TifExtension)]
    [InlineData(PicaImageFormats.TiffExtension)]
    public async Task CreateAsync_WithAdjacentExtendedFormat_IncludesImage(string extension)
    {
        using PicaTemporaryDirectory temporaryDirectory = new();
        string selectedImagePath = Path.Combine(temporaryDirectory.DirectoryPath, "01.png");
        string adjacentImagePath = Path.Combine(
            temporaryDirectory.DirectoryPath,
            "02" + extension);
        await CreateImageAsync(selectedImagePath, [1]);
        await CreateImageAsync(adjacentImagePath, [2]);
        SetLastWriteTimesNewestFirst(selectedImagePath, adjacentImagePath);
        PicaStartupRequestFactory factory = CreateFactory();

        PicaStartupRequest request = await factory.CreateAsync(
            [selectedImagePath],
            CancellationToken.None);

        request.ViewerRequest.Items.Select(item => item.FileName)
            .Should()
            .Equal("01.png", "02" + extension);
    }

    private static async Task CreateImageAsync(string path, byte[] content)
    {
        await File.WriteAllBytesAsync(path, content);
    }

    private static PicaStartupRequestFactory CreateFactory(
        IWindowsExplorerItemOrderProvider? explorerItemOrderProvider = null)
    {
        return new PicaStartupRequestFactory(
            new ImageFormatRegistry(),
            explorerItemOrderProvider
                ?? new RecordingWindowsExplorerItemOrderProvider(null),
            NullLogger<PicaStartupRequestFactory>.Instance);
    }
}
