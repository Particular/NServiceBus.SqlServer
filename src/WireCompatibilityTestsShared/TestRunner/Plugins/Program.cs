//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Reflection;
//using System.Threading.Tasks;

//class Program
//{
//    static async Task Main()
//    {
//        try
//        {
//            var pluginPaths = Directory.GetFiles("plugins", "v*.dll", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true });

//            var commands = pluginPaths.SelectMany(pluginPath =>
//            {
//                var pluginAssembly = LoadPlugin(pluginPath);
//                return CreateCommands<IPlugin>(pluginAssembly);
//            }).ToList();

//            var tasks = new List<Task>();
//            foreach (var command in commands)
//            {
//                await Console.Out.WriteLineAsync($"{command}").ConfigureAwait(false);
//                tasks.Add(command.Start());
//            }

//            await Task.WhenAll(tasks).ConfigureAwait(false);
//        }
//        catch (Exception ex)
//        {
//            Console.WriteLine(ex);
//        }
//        finally
//        {
//            Console.WriteLine("Done!");
//        }
//    }

//    static Assembly LoadPlugin(string relativePath)
//    {
//        var root = Path.GetDirectoryName(typeof(Program).Assembly.Location);
//        string pluginLocation = Path.GetFullPath(Path.Combine(root, relativePath.Replace('\\', Path.DirectorySeparatorChar)));
//        Console.WriteLine($"Loading commands from: {pluginLocation}");
//        var loadContext = new PluginLoadContext(pluginLocation);
//        return loadContext.LoadFromAssemblyName(AssemblyName.GetAssemblyName(pluginLocation));
//    }

//    static IEnumerable<T> CreateCommands<T>(Assembly assembly)
//    {
//        int count = 0;

//        foreach (Type type in assembly.GetTypes())
//        {
//            if (typeof(T).IsAssignableFrom(type))
//            {
//                if (Activator.CreateInstance(type) is T result)
//                {
//                    count++;
//                    yield return result;
//                }
//            }
//        }

//        if (count == 0)
//        {
//            string availableTypes = string.Join(",", assembly.GetTypes().Select(t => t.FullName));
//            throw new ApplicationException($"Can't find any type which implements {typeof(T)}in {assembly} from {assembly.Location}.\nAvailable types: {availableTypes}");
//        }
//    }
//}
