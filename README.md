# <img src="src/Pica.Viewer/Assets/AppIcon.ico" alt="Pica" width="32" height="32"> Pica

**English** | [Русский](https://github.com/Krivodel/Pica/blob/main/README.ru.md)

[![Download for Windows](https://badgen.net/badge/icon/Download%20for%20Windows?icon=windows&label)](https://github.com/Krivodel/Pica/releases/latest/download/Pica-win-Setup.exe)

A convenient image viewer. It can run as a standalone application or be used by other applications as an embedded viewer.

## Main features

- Viewing individual channels with the same functionality as regular images.
- Area selection (pixels are taken directly from the image without any modifications).
- Automatic fitting of the window size to the image.
- Pinning the window on top of other windows.
- Customizable display of information, such as the name, format, resolution, or modification date.
- Useful context menu items, including a proper "Open with" menu.
- Supported formats: `.png`, `.jpeg`, `.webp`, `.bmp`, `.gif`, `.ico`, `.avif`, `.heic`, `.heif`, `.tif`.

## Controls

| Action                                                   | Hotkeys                                              |
|----------------------------------------------------------|------------------------------------------------------|
| Previous image or channel                                | `A` or `←`                                           |
| Next image or channel                                    | `D` or `→`                                           |
| Zoom                                                     | Mouse wheel                                          |
| Slow zoom                                                | Hold `Shift`, `Ctrl`, or `Alt` while zooming         |
| Pan                                                      | Drag with `LMB` or `MMB`                             |
| Slow pan                                                 | Hold `Shift` or `Alt` while panning                  |
| Reset zoom and position                                  | `Space`                                              |
| Channel mode                                             | `Tab`                                                |
| Transparency background                                  | `T`                                                  |
| Image filtering                                          | `F`                                                  |
| Copy                                                     | `Ctrl + C`                                           |
| Select area                                              | `Ctrl` + drag with `LMB`                             |
| Select the entire image                                  | `Ctrl + A`                                           |
| Pan the image while an area selection is active          | Drag with `MMB`                                      |
| Switch between windowed and fullscreen modes             | Double-click with `LMB`, if enabled in the settings  |
| Close window or cancel                                   | `Esc`                                                |

# Quick start

## Installation

Add the package:

```powershell
dotnet add package Pica.Viewer
```

For a prerelease version, use `--prerelease` or specify the version number explicitly.

## Theme

If the application does not yet use Fluent and SukiUI, add them to `App.axaml`:

```xml
<Application xmlns="https://github.com/avaloniaui"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:suki="using:SukiUI"
             x:Class="Example.Desktop.App">
    <Application.Styles>
        <FluentTheme />
        <suki:SukiTheme ThemeColor="Blue" />
    </Application.Styles>
</Application>
```

## Example

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Pica.Protocol;
using Pica.Viewer;
using Pica.Viewer.Services;
using Pica.Viewer.Views;

ServiceCollection services = new();

services.AddLogging(builder =>
{
    builder.SetMinimumLevel(LogLevel.Warning);
});
services.AddPicaViewer();

ServiceProvider serviceProvider = services.BuildServiceProvider();

Guid imageId = Guid.NewGuid();
string imagePath = Path.GetFullPath("image.png");
string imageName = Path.GetFileName(imagePath);
PicaImageItem image = new(imageId, imagePath, imageName);
PicaViewerRequest request = new([image], imageId);

IImageViewerWindowFactory windowFactory = serviceProvider.GetRequiredService<IImageViewerWindowFactory>();
ImageViewerWindow window = await windowFactory.CreateAsync(request, CancellationToken.None);

window.Show();
```

`window.Show()` must be called on the UI thread.

`Pica.Viewer` forwards logs to the logging system already configured by the application and does not modify it.

## Multiple images

Pass all images in `PicaViewerRequest.Items`, and the ID of the initially selected image in `SelectedItemId`:

```csharp
PicaViewerRequest request = new(images, selectedImageId);
```

You can pass the path to a ready-made low-resolution image in `PicaImageItem.PreviewFilePath` for fast loading. If there is no preview image, this parameter can be omitted, but loading may take slightly longer.

## Custom context menu items

Pass the list of items in `PicaViewerRequest` and their click handler in `CreateAsync`:

```csharp
PicaActionDefinition[] actions =
[
    new(
        Id: "open-in-editor",
        DisplayName: "Open in editor",
        IconGeometry: "M13,3 L20,3 L20,10 L18,10 L18,6.4 L9.4,15 L8,13.6 L16.6,5 L13,5 Z M4,5 L10,5 L10,7 L6,7 L6,17 L16,17 L16,13 L18,13 L18,19 L4,19 Z",
        IconRotationDegrees: 0d,
        Targets: PicaActionTargets.CurrentImage | PicaActionTargets.Selection,
        Order: 0)
];
PicaViewerRequest request = new(images, selectedImageId, actions);
ImageViewerWindow window = await windowFactory.CreateAsync(request, actionDispatcher, CancellationToken.None);
```

`actions` is a list of `PicaActionDefinition`, and `actionDispatcher` is an implementation of `IViewerActionDispatcher`.

Pica calls `DispatchCurrentImageAsync` for the original image, `DispatchSelectionAsync` for the selected fragment, and `DispatchDerivedImageAsync` for the selected channel.

Example implementation of `IViewerActionDispatcher`:

```csharp
public class ViewerActionDispatcher : IViewerActionDispatcher
{
    public Task DispatchCurrentImageAsync(
        PicaActionDefinition action,
        PicaImageItem item,
        CancellationToken ct)
    {
        return MyImageEditor.OpenAsync(item.FilePath, ct);
    }

    public Task DispatchSelectionAsync(
        PicaActionDefinition action,
        PicaImageItem item,
        byte[] pngContent,
        CancellationToken ct)
    {
        return MyImageEditor.OpenAsync(PicaImageFormats.SelectionFileName, pngContent, ct);
    }

    public Task DispatchDerivedImageAsync(
        PicaActionDefinition action,
        PicaImageItem item,
        string fileName,
        byte[] pngContent,
        CancellationToken ct)
    {
        return MyImageEditor.OpenAsync(fileName, pngContent, ct);
    }
}
```

`MyImageEditor` in the example is an application class that opens an image by path or from a byte array.
