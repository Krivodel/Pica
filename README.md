# <img src="src/Pica.Viewer/Assets/AppIcon.ico" alt="Pica" width="32" height="32"> Pica

**English** | [Русский](https://github.com/Krivodel/Pica/blob/main/README.ru.md)

A convenient image viewer. It can run as a standalone application or be used by other applications as an embedded viewer.

## Main features

- Automatic fitting of the window size to the image.
- Pinning the window on top of other windows.
- Display of the name, format, resolution, and modification date. Each item can be enabled independently.
- Area selection (pixels are taken directly from the image without any modifications).
- Useful context menu items, including a proper "Open with" menu.
- Supported formats: `.png`, `.jpeg`, `.webp`, `.bmp`, `.gif`, `.ico`, `.avif`, `.heic`, `.heif`, `.tif`.

## Controls

| Action                                                    | Hotkeys                                            |
|-----------------------------------------------------------|----------------------------------------------------|
| Previous image                                            | `A` or `←`                                         |
| Next image                                                | `D` or `→`                                         |
| Zoom                                                      | Mouse wheel                                        |
| Slow zoom                                                 | Hold `Shift`, `Ctrl`, or `Alt` while zooming       |
| Pan                                                       | Drag with `LMB` or `MMB`                           |
| Slow pan                                                  | Hold `Shift` or `Alt` while panning                |
| Reset zoom and position                                   | `Space`                                            |
| Image filtering                                           | `F`                                                |
| Copy                                                      | `Ctrl + C`                                         |
| Select area                                               | `Ctrl` + drag with `LMB`                           |
| Select the entire image                                   | `Ctrl + A`                                         |
| Pan the image while an area selection is active           | Drag with `MMB`                                    |
| Switch between windowed and fullscreen modes              | Double-click with `LMB`, if enabled in the settings |
| Close settings, cancel selection, or close the window     | `Esc`                                              |

## Using in your own applications

`Pica.Viewer` is a NuGet package for Avalonia.
