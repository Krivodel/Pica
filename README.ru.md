# <img src="src/Pica.Viewer/Assets/AppIcon.ico" alt="Pica" width="32" height="32"> Pica

[English](https://github.com/Krivodel/Pica/blob/main/README.md) | **Русский**

[![Скачать для Windows](https://badgen.net/badge/icon/%D0%A1%D0%BA%D0%B0%D1%87%D0%B0%D1%82%D1%8C%20%D0%B4%D0%BB%D1%8F%20Windows?icon=windows&label)](https://github.com/Krivodel/Pica/releases/latest/download/Pica-win-Setup.exe)

Удобный просмотрщик изображений. Может работать как самостоятельное приложение или использоваться другими программами как встроенный просмотрщик.

## Основные возможности

- Просмотр отдельных каналов с тем же функционалом, что и у обычных изображений.
- Выделение области (пиксели берутся напрямую с изображения без изменений).
- Автоматический подгон размера окна под изображение.
- Закрепление окна поверх других окон.
- Настраиваемое отображение сведений, например названия, формата, разрешения или дата изменения.
- Полезные пункты в контекстном меню, в том числе адекватное меню "Открыть с помощью".
- Поддерживаются форматы: `.png`, `.jpeg`, `.webp`, `.bmp`, `.gif`, `.ico`, `.avif`, `.heic`, `.heif`, `.tif`.

## Управление

| Действие                                       | Горячие клавиши                                           |
|------------------------------------------------|-----------------------------------------------------------|
| Предыдущее изображение или канал               | `A` или `←`                                               |
| Следующее изображение или канал                | `D` или `→`                                               |
| Масштабирование                                | Колесо мыши                                               |
| Медленное масштабирование                      | Удерживание `Shift`, `Ctrl` или `Alt` при масштабировании |
| Перемещение                                    | Перетаскивание `ЛКМ` или `СКМ`                            |
| Медленное перемещение                          | Удерживание `Shift` или `Alt` при перемещении             |
| Сброс масштаба и положения                     | `Пробел`                                                  |
| Режим каналов                                  | `Tab`                                                     |
| Прозрачный фон                                 | `T`                                                       |
| Фильтрация изображения                         | `F`                                                       |
| Копирование                                    | `Ctrl + C`                                                |
| Выделение области                              | `Ctrl` + перетаскивание `ЛКМ`                             |
| Выделение всего изображения                    | `Ctrl + A`                                                |
| Перемещение изображения при активном выделении | Перетаскивание `СКМ`                                      |
| Переключение оконного и полноэкранного режимов | Двойной клик `ЛКМ`, если включено в настройках            |
| Закрытие окна или отмена                       | `Esc`                                                     |

# Быстрый старт

## Установка

Добавь пакет:

```powershell
dotnet add package Pica.Viewer
```

Для предварительной версии используй `--prerelease` или укажи номер версии явно.

## Тема

Если приложение ещё не использует Fluent и SukiUI, добавь их в `App.axaml`:

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

## Пример

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

`window.Show()` должен вызываться в потоке интерфейса.

`Pica.Viewer` передаёт логи в уже настроенную приложением систему логов и не изменяет её.

## Несколько изображений

Передай все изображения в `PicaViewerRequest.Items`, а ID изначально выбранного изображения — в `SelectedItemId`:

```csharp
PicaViewerRequest request = new(images, selectedImageId);
```

В `PicaImageItem.PreviewFilePath` можно передать путь к готовому изображению в низком разрешении для быстрой загрузки. Если предварительного изображения нет, этот параметр можно не указывать, но загрузка может стать чуть дольше.

## Кастомные пункты в контекстном меню

Передай список пунктов в `PicaViewerRequest` и обработчик их нажатий в `CreateAsync`:

```csharp
PicaActionDefinition[] actions =
[
    new(
        Id: "open-in-editor",
        DisplayName: "Открыть в редакторе",
        IconGeometry: "M13,3 L20,3 L20,10 L18,10 L18,6.4 L9.4,15 L8,13.6 L16.6,5 L13,5 Z M4,5 L10,5 L10,7 L6,7 L6,17 L16,17 L16,13 L18,13 L18,19 L4,19 Z",
        IconRotationDegrees: 0d,
        Targets: PicaActionTargets.CurrentImage | PicaActionTargets.Selection,
        Order: 0)
];
PicaViewerRequest request = new(images, selectedImageId, actions);
ImageViewerWindow window = await windowFactory.CreateAsync(request, actionDispatcher, CancellationToken.None);
```

`actions` — список `PicaActionDefinition`, а `actionDispatcher` — реализация `IViewerActionDispatcher`.

Pica вызывает `DispatchCurrentImageAsync` для исходного изображения, `DispatchSelectionAsync` для выделенного фрагмента и `DispatchDerivedImageAsync` для выбранного канала.

Пример реализации `IViewerActionDispatcher`:

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

`MyImageEditor` в примере — класс приложения, открывающий изображение по пути или из массива байтов.
