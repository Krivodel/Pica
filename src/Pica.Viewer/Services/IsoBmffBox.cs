namespace Pica.Viewer.Services;

internal readonly record struct IsoBmffBox(
    uint Type,
    int PayloadStart,
    int End);
