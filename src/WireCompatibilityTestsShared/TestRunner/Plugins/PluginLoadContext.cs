using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

class PluginLoadContext : AssemblyLoadContext
{
    readonly string pluginPath;
    readonly AssemblyDependencyResolver resolver;

    public PluginLoadContext(string pluginPath)
    {
        this.pluginPath = Path.GetDirectoryName(pluginPath);
        resolver = new AssemblyDependencyResolver(pluginPath);
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
        if (assemblyName.Name == "Microsoft.Data.SqlClient")
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var dllPath = Path.Combine(pluginPath, "runtimes", "win", "lib", "net6.0", "Microsoft.Data.SqlClient.dll");
                if (File.Exists(dllPath))
                {
                    return dllPath;
                }
            }
        }
        if (assemblyName.Name == "System.Data.SqlClient")
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var dllPath = Path.Combine(pluginPath, "runtimes", "win", "lib", "netcoreapp2.1", "System.Data.SqlClient.dll");
                if (File.Exists(dllPath))
                {
                    return dllPath;
                }
            }
        }
        return resolver.ResolveAssemblyToPath(assemblyName);
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
