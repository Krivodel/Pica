# <img src="src/Pica.Viewer/Assets/AppIcon.ico" alt="Pica" width="32" height="32"> Pica

**English** | [Русский](https://github.com/Krivodel/Pica/blob/main/README.ru.md)

A convenient image viewer. It can run as a standalone application or be used by other applications as an embedded viewer.

## Main features

- Viewing individual channels with the same functionality as regular images.
- Automatic fitting of the window size to the image.
- Pinning the window on top of other windows.
- Area selection (pixels are taken directly from the image without any modifications).
- Customizable display of information, such as the name, format, resolution, or modification date.
- Useful context menu items, including a proper "Open with" menu.
- Supported formats: `.png`, `.jpeg`, `.webp`, `.bmp`, `.gif`, `.ico`, `.avif`, `.heic`, `.heif`, `.tif`.

## Controls

| Action                                                    | Hotkeys                                            |
|-----------------------------------------------------------|----------------------------------------------------|
| Previous image or channel                                 | `A` or `←`                                         |
| Next image or channel                                     | `D` or `→`                                         |
| Zoom                                                      | Mouse wheel                                        |
| Slow zoom                                                 | Hold `Shift`, `Ctrl`, or `Alt` while zooming       |
| Pan                                                       | Drag with `LMB` or `MMB`                           |
| Slow pan                                                  | Hold `Shift` or `Alt` while panning                |
| Reset zoom and position                                   | `Space`                                            |
| Channel mode                                              | `Tab`                                              |
| Image filtering                                           | `F`                                                |
| Copy                                                      | `Ctrl + C`                                         |
| Select area                                               | `Ctrl` + drag with `LMB`                           |
| Select the entire image                                   | `Ctrl + A`                                         |
| Pan the image while an area selection is active           | Drag with `MMB`                                    |
| Switch between windowed and fullscreen modes              | Double-click with `LMB`, if enabled in the settings |
| Close window or cancel                                    | `Esc`                                              |

## Using in your own applications

`Pica.Viewer` is a NuGet package for Avalonia.
