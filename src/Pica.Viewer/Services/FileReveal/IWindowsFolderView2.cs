using System.Runtime.InteropServices;

namespace Pica.Viewer.Services.FileReveal;

[ComImport]
[Guid("1AF3A467-214F-4298-908E-06B03E0B39F9")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IWindowsFolderView2
{
    [PreserveSig]
    int GetCurrentViewMode(out uint viewMode);

    [PreserveSig]
    int SetCurrentViewMode(uint viewMode);

    [PreserveSig]
    int GetFolder(ref Guid interfaceId, out nint folder);

    [PreserveSig]
    int Item(int itemIndex, out nint itemIdList);

    [PreserveSig]
    int ItemCount(uint flags, out int itemCount);

    [PreserveSig]
    int Items(uint flags, ref Guid interfaceId, out nint items);

    [PreserveSig]
    int GetSelectionMarkedItem(out int itemIndex);

    [PreserveSig]
    int GetFocusedItem(out int itemIndex);

    [PreserveSig]
    int GetItemPosition(nint itemIdList, nint position);

    [PreserveSig]
    int GetSpacing(nint spacing);

    [PreserveSig]
    int GetDefaultSpacing(nint spacing);

    [PreserveSig]
    int GetAutoArrange();

    [PreserveSig]
    int SelectItem(int itemIndex, uint flags);

    [PreserveSig]
    int SelectAndPositionItems(
        uint itemCount,
        nint itemIdLists,
        nint positions,
        uint flags);

    [PreserveSig]
    int SetGroupBy(nint propertyKey, int ascending);

    [PreserveSig]
    int GetGroupBy(nint propertyKey, nint ascending);

    [PreserveSig]
    int SetViewProperty(
        nint itemIdList,
        nint propertyKey,
        nint propertyValue);

    [PreserveSig]
    int GetViewProperty(
        nint itemIdList,
        nint propertyKey,
        nint propertyValue);

    [PreserveSig]
    int SetTileViewProperties(
        nint itemIdList,
        nint propertyList);

    [PreserveSig]
    int SetExtendedTileViewProperties(
        nint itemIdList,
        nint propertyList);

    [PreserveSig]
    int SetText(int textType, nint text);

    [PreserveSig]
    int SetCurrentFolderFlags(uint mask, uint flags);

    [PreserveSig]
    int GetCurrentFolderFlags(out uint flags);

    [PreserveSig]
    int GetSortColumnCount(out int columnCount);

    [PreserveSig]
    int SetSortColumns(nint sortColumns, int columnCount);

    [PreserveSig]
    int GetSortColumns(nint sortColumns, int columnCount);

    [PreserveSig]
    int GetItem(
        int itemIndex,
        ref Guid interfaceId,
        out nint shellItem);
}
