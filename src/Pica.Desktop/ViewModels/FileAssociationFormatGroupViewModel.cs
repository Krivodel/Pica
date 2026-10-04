using System.Collections.ObjectModel;

namespace Pica.Desktop.ViewModels;

internal sealed class FileAssociationFormatGroupViewModel
{
    public ReadOnlyCollection<FileAssociationFormatViewModel> Formats { get; }

    public FileAssociationFormatGroupViewModel(ReadOnlyCollection<FileAssociationFormatViewModel> formats)
    {
        Formats = formats ?? throw new ArgumentNullException(nameof(formats));
    }
}
