using Pica.Protocol;
using Pica.Viewer.Services;

namespace Pica.Viewer.ViewModels;

internal sealed record PreparedSelectionAction(
    PicaActionDefinition Action,
    PicaImageItem Item,
    PreparedClipboardImage Image);
