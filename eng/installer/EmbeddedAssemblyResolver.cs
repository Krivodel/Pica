using System;
using System.IO;
using System.Reflection;

namespace Pica.Installer;

internal static class EmbeddedAssemblyResolver
{
    public static void Register()
    {
        AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
    }

    private static Assembly? OnAssemblyResolve(
        object? sender,
        ResolveEventArgs e)
    {
        AssemblyName requestedAssembly = new AssemblyName(e.Name);
        string? requestedName = requestedAssembly.Name;

        if (string.IsNullOrWhiteSpace(requestedName))
        {
            return null;
        }

        string resourceName = $"{requestedName}.dll";
        Assembly installerAssembly = typeof(EmbeddedAssemblyResolver).Assembly;
        Stream? resourceStream = installerAssembly.GetManifestResourceStream(
            resourceName);

        if (resourceStream is null)
        {
            return null;
        }

        using (resourceStream)
        using (MemoryStream assemblyBytes = new MemoryStream())
        {
            resourceStream.CopyTo(assemblyBytes);
            return Assembly.Load(assemblyBytes.ToArray());
        }
    }
}
