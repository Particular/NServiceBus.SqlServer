using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

class PluginLoadContext : AssemblyLoadContext
{
    readonly Dictionary<string, string> platformSpecificManagedAssemblies;
    readonly string pluginPath;
    readonly AssemblyDependencyResolver resolver;
    readonly string os;

    public PluginLoadContext(string pluginPath, Dictionary<string, string> platformSpecificManagedAssemblies)
    {
        this.platformSpecificManagedAssemblies = platformSpecificManagedAssemblies;
        this.pluginPath = Path.GetDirectoryName(pluginPath);
        resolver = new AssemblyDependencyResolver(pluginPath);
        os = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win" : "unix";
    }

    protected override Assembly Load(AssemblyName assemblyName)
    {
        string assemblyPath = ResolveManagedDllPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }
        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string libraryPath = ResolveUnmanagedDllPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }

    string ResolveManagedDllPath(AssemblyName assemblyName)
    {
        var name = assemblyName.Name;
        if (platformSpecificManagedAssemblies.TryGetValue(name, out var framework))
        {
            var dllPath = BuildPlatformSpecificAssemblyPath(framework, name);
            if (File.Exists(dllPath))
            {
                return dllPath;
            }
        }
        return resolver.ResolveAssemblyToPath(assemblyName);
    }

    string BuildPlatformSpecificAssemblyPath(string framework, string assembly)
    {
        return Path.Combine(pluginPath, "runtimes", os, "lib", framework, $"{assembly}.dll");
    }

    string ResolveUnmanagedDllPath(string unmanagedDllName)
    {
        string dllPath = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (dllPath != null)
        {
            return dllPath;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            dllPath = Path.Combine(pluginPath, "runtimes", $"win-{RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()}", "native", unmanagedDllName);
            if (File.Exists(dllPath))
            {
                return dllPath;
            }
        }
        return null;
    }
}
