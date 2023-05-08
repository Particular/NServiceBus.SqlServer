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
        readonly string projectName;
        readonly string behaviorType;
        readonly string generatedProjectFolder;
        readonly Dictionary<string, string> args;
        IPlugin plugin;
        bool started;
        readonly string agentFrameworkPackageName;
        readonly string behaviorPackageName;
        readonly string coreVersionString;
        readonly string transportVersionString;
        readonly string transportPackageName;

        public AgentPlugin(int majorVersionToTest, int minorVersionToTest, int coreMajorVersion, string behaviorType, string generatedProjectFolder, Dictionary<string, string> args)
        {
            projectName = $"TestAgent.V{majorVersionToTest}.{minorVersionToTest}"; //generated project depends on downstream minor
            transportVersionString = $"{majorVersionToTest}.{minorVersionToTest}.*";
            agentFrameworkPackageName = $"TestAgent.Framework.V{coreMajorVersion}"; //agent framework depends only on core major
            behaviorPackageName = $"WireCompatibilityTests.TestBehaviors.V{majorVersionToTest}"; //behaviors depend only on downstream major
            this.behaviorType = $"{behaviorType}, WireCompatibilityTests.TestBehaviors.V{majorVersionToTest}";
            this.generatedProjectFolder = generatedProjectFolder;
            this.args = args;
            coreVersionString = $"{coreMajorVersion}.*";
            transportPackageName = majorVersionToTest > 5 ? "NServiceBus.Transport.SqlServer" : "NServiceBus.SqlServer";
        }

#pragma warning disable PS0018
        public async Task Compile()
#pragma warning restore PS0018
        {
            var projectFolder = Path.Combine(generatedProjectFolder, projectName);
            if (!Directory.Exists(projectFolder))
            {
                Directory.CreateDirectory(projectFolder);
            }

            var projectFilePath = Path.Combine(projectFolder, $"{projectName}.csproj");
            if (!File.Exists(projectFilePath))
            {
                await File.AppendAllTextAsync(projectFilePath, @$"<Project Sdk=""Microsoft.NET.Sdk"">

  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <RootNamespace>TestAgent</RootNamespace>
    <EnableDynamicLoading>true</EnableDynamicLoading>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include=""..\..\PluginBase\PluginBase.csproj"" >
      <Private>false</Private>
      <ExcludeAssets>runtime</ExcludeAssets>
    </ProjectReference>

    <ProjectReference Include=""..\..\WireCompatibilityTestsShared\{agentFrameworkPackageName}\{agentFrameworkPackageName}.csproj"" />
    <ProjectReference Include=""..\..\{behaviorPackageName}\{behaviorPackageName}.csproj"" />

    <PackageReference Include=""NServiceBus"" Version=""{coreVersionString}"" />
    <PackageReference Include=""{transportPackageName}"" Version=""{transportVersionString}"" />

  </ItemGroup>

</Project>
").ConfigureAwait(false);
            }

            var buildProcess = new Process();
            buildProcess.StartInfo.FileName = @"dotnet";
            buildProcess.StartInfo.Arguments = $"build \"{projectFilePath}\"";
            buildProcess.StartInfo.UseShellExecute = false;
            buildProcess.StartInfo.RedirectStandardOutput = true;
            buildProcess.StartInfo.RedirectStandardError = true;
            buildProcess.StartInfo.RedirectStandardInput = true;
            buildProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            buildProcess.StartInfo.CreateNoWindow = true;

            buildProcess.Start();

            buildProcess.WaitForExit(30000);

            if (buildProcess.ExitCode != 0)
            {
                var buildOutput = await buildProcess.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                await Console.Out.WriteLineAsync(buildOutput).ConfigureAwait(false);
                throw new Exception("Build failed");
            }

            var folder = Path.GetDirectoryName(projectFilePath);
            var agentDllPath = $"{folder}/bin/Debug/net6.0/{agentFrameworkPackageName}.dll";

            if (!File.Exists(agentDllPath))
            {
                throw new FileNotFoundException();
            }

            var pluginAssembly = LoadPlugin(agentDllPath);
            plugin = CreateCommands(pluginAssembly).Single();
        }

        public async Task StartEndpoint(CancellationToken cancellationToken = default)
        {
            await plugin.StartEndpoint(behaviorType, args, transportVersionString, cancellationToken).ConfigureAwait(false);
            started = true;
        }

        public Task StartTest(CancellationToken cancellationToken = default) => plugin.StartTest(cancellationToken);

        public Task Stop(CancellationToken cancellationToken = default)
        {
            if (!started)
            {
                return Task.CompletedTask;
            }

            return plugin.Stop(cancellationToken);
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