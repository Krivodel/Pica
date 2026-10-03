using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

using Microsoft.Win32.SafeHandles;

namespace Pica.Desktop.Services.FileAssociations;

internal static class WindowsFileAssociationNative
{
    private const int ProgramIdAssociationString = 20;
    private const int ExecutableAssociationString = 2;
    private const int MaximumProgramIdLength = 4096;
    private const uint IgnoreUnknownAssociation = 0x00000400;
    private const uint FixedProgramAssociation = 0x00000800;
    private const uint ExecutableNameAssociation = 0x00000002;
    private const string ApplicationProgramsPrefix = @"Applications\";
    private const int NoAssociationResult = unchecked((int)0x80070483);
    private const int FileNotFoundResult = unchecked((int)0x80070002);
    private const int ApplicationNotFoundResult = unchecked((int)0x800401F5);

    internal static string? QueryDefault(string extension)
    {
        return QueryAssociation(extension, ProgramIdAssociationString);
    }

    internal static string? QueryExecutable(string programId)
    {
        if (programId.StartsWith(ApplicationProgramsPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return QueryAssociation(programId[ApplicationProgramsPrefix.Length..], ExecutableAssociationString,
                IgnoreUnknownAssociation | ExecutableNameAssociation);
        }

        return QueryAssociation(programId, ExecutableAssociationString, IgnoreUnknownAssociation | FixedProgramAssociation);
    }

    internal static long GetLastWriteTime(SafeRegistryHandle key)
    {
        int result = RegQueryInfoKeyW(key, nint.Zero, nint.Zero, nint.Zero, nint.Zero, nint.Zero,
            nint.Zero, nint.Zero, nint.Zero, nint.Zero, nint.Zero, out long fileTime);

        if (result != 0)
        {
            throw new Win32Exception(result, "Windows could not read the file association timestamp.");
        }

        return fileTime;
    }

    private static string? QueryAssociation(string association, int associationString, uint flags = IgnoreUnknownAssociation)
    {
        uint length = MaximumProgramIdLength;
        StringBuilder buffer = new(MaximumProgramIdLength);
        int result = AssocQueryStringW(flags, associationString, association, null, buffer, ref length);

        if (result is NoAssociationResult or FileNotFoundResult or ApplicationNotFoundResult)
        {
            return null;
        }

        Marshal.ThrowExceptionForHR(result);

        if (result != 0)
        {
            throw new IOException("Windows returned an incomplete default application identifier.");
        }

        return buffer.Length == 0 ? null : buffer.ToString();
    }

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
    private static extern int AssocQueryStringW(uint flags, int associationString, string association,
        string? extra, StringBuilder output, ref uint outputLength);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)]
    private static extern int RegQueryInfoKeyW(SafeRegistryHandle key, nint className, nint classLength,
        nint reserved, nint subKeyCount, nint maximumSubKeyLength, nint maximumClassLength,
        nint valueCount, nint maximumValueNameLength, nint maximumValueLength,
        nint securityDescriptorLength, out long lastWriteTime);
}
