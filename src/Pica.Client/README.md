# Подключение Pica к стороннему приложению

`Pica.Client` находит установленную Pica и запускает отдельный процесс, а `Pica.Protocol` описывает изображения и дополнительные действия интерфейса.

1. Зарегистрируйте клиент вызовом `services.AddPicaClient()`.
2. Получите путь через `IPicaExecutableLocator`. Значение `null` означает, что Pica не установлена; в этом случае приложение не должно выполнять открытие.
3. Сформируйте `PicaViewerRequest`: передайте файловые пути, выбранное изображение и собственные `PicaActionDefinition`.
4. Запустите `IPicaProcessRunner.RunAsync`. Обработчик получает `PicaActionInvocation` для каждого выбранного пользователем дополнительного действия.

Дополнительное действие само описывает подпись, значок, порядок и поддерживаемую цель: полное изображение, выделенную область либо оба варианта. Pica ничего не знает о смысле действия и только возвращает приложению его идентификатор и файловый результат. Каталог `ActionPayloadDirectory` принадлежит вызывающему приложению: оно создаёт его до запуска и удаляет после завершения Pica.

Для разработки путь к исполняемому файлу можно задать переменной окружения `PICA_EXECUTABLE_PATH`.

Установленная версия обнаруживается в следующих местах:

- Windows: `App Paths`, `%LocalAppData%\Programs\Pica\Pica.exe`, `%ProgramFiles%\Pica\Pica.exe` и `PATH`;
- macOS: `~/Applications/Pica.app/Contents/MacOS/Pica`, `/Applications/Pica.app/Contents/MacOS/Pica` и `PATH`;
- Linux: `~/.local/bin/Pica`, `~/.local/share/Pica/Pica`, `/usr/local/bin/Pica`, `/usr/bin/Pica`, `/opt/Pica/Pica` и `PATH`.
