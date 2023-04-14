namespace TestRunner
{
    using System;
    using System.IO;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Reflection;
    using System.Threading;
    using System.Diagnostics;

    class AgentPlugin
    {
        readonly IPlugin process;
        bool started;
        string[] pluginArgs;

        public AgentPlugin(string projectName, string behaviorType, string mappedFileName, Dictionary<string, string> args)
        {
            var projectFilePath = GetProjectFilePath(projectName);

            var buildProcess = new Process();
            buildProcess.StartInfo.FileName = @"dotnet";
            buildProcess.StartInfo.Arguments = $"build --project \"{projectFilePath}\"";
            buildProcess.StartInfo.UseShellExecute = false;
            buildProcess.StartInfo.RedirectStandardOutput = true;
            buildProcess.StartInfo.RedirectStandardError = true;
            buildProcess.StartInfo.RedirectStandardInput = true;
            buildProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            buildProcess.StartInfo.CreateNoWindow = true;

            buildProcess.Start();
            buildProcess.WaitForExit(10000);

            var folder = Path.GetDirectoryName(projectFilePath);
            //var pluginPath = "S:/NServiceBus.SqlServer/src/WireCompatibilityTests.Generated.TestAgent.V7/bin/Debug/net6.0";
            var pluginPath = $"{folder}/bin/Debug/net6.0/{projectName}.dll";

            if (!File.Exists(pluginPath))
            {
                throw new FileNotFoundException();
            }

            var pluginAssembly = LoadPlugin(pluginPath);
            process = CreateCommands(pluginAssembly).Single();
            var x = new List<string>
            {
                behaviorType,
                mappedFileName
            };
            x.AddRange(args.Select(kvp => FormatArgument(kvp)));

            pluginArgs = x.ToArray();
        }

        string GetProjectFilePath(string projectName)
        {
            var directory = AppDomain.CurrentDomain.BaseDirectory;

            while (true)
            {
                if (Directory.EnumerateFiles(directory).Any(file => file.EndsWith(".sln")))
                {
                    var projectFilePath = Directory.EnumerateFiles(directory, $"{projectName}.csproj", SearchOption.AllDirectories).SingleOrDefault();
                    if (!File.Exists(projectFilePath))
                    {
                        throw new Exception($"Unable to find a project file that matches the supplied project name {projectName}");
                    }

                    return projectFilePath;
                }

                var parent = Directory.GetParent(directory);
                if (parent == null)
                {
                    throw new Exception($"Unable to determine the solution directory path due to the absence of a solution file.");
                }

                directory = parent.FullName;
            }
        }

        static string FormatArgument(KeyValuePair<string, string> kvp)
        {
            if (kvp.Value != null)
            {
                return kvp.Key + "=" + kvp.Value;
            }

            return kvp.Key;
        }

        public async Task Start(CancellationToken cancellationToken = default)
        {
            started = true;
            await process.ExecuteAsync(pluginArgs, cancellationToken).ConfigureAwait(false);
        }

        public Task Stop(CancellationToken cancellationToken = default)
        {
            if (!started)
            {
                return Task.CompletedTask;
            }

            return Task.CompletedTask;
        }

        static Assembly LoadPlugin(string pluginLocation)
        {
            //var root = Path.GetDirectoryName(typeof(Program).Assembly.Location);
            //string pluginLocation = Path.GetFullPath(Path.Combine(root, relativePath.Replace('\\', Path.DirectorySeparatorChar)));
            Console.WriteLine($"Loading commands from: {pluginLocation}");
            var loadContext = new PluginLoadContext(pluginLocation);
            return loadContext.LoadFromAssemblyName(AssemblyName.GetAssemblyName(pluginLocation));
        }

        static IEnumerable<IPlugin> CreateCommands(Assembly assembly)
        {
            int count = 0;

            foreach (Type type in assembly.GetTypes())
            {
                if (typeof(IPlugin).IsAssignableFrom(type))
                {
                    if (Activator.CreateInstance(type) is IPlugin result)
                    {
                        count++;
                        yield return result;
                    }
                }
            }

            if (count == 0)
            {
                string availableTypes = string.Join(",", assembly.GetTypes().Select(t => t.FullName));
                throw new ApplicationException($"Can't find any type which implements '{typeof(IPlugin)}' in '{assembly}' from '{assembly.Location}'.\nAvailable types: {availableTypes}");
            }
        }
    }
}