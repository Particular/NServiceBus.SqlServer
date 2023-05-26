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
    using NuGet.Versioning;

    class AgentPlugin
    {
        static readonly AsyncDuplicateLock Locks = new AsyncDuplicateLock();

        readonly string projectName;
        readonly string behaviorType;
        readonly Dictionary<string, string> platformSpecificAssemblies;
        readonly string generatedProjectFolder;
        readonly PluginOptions opts;
        IPlugin plugin;
        bool started;
        readonly string behaviorPackageName;
        readonly SemanticVersion versionToTest;
        readonly string transportPackageName;

        static readonly List<string> projects = new List<string>();
        public AgentPlugin(
            Dictionary<string, string> platformSpecificAssemblies,
            SemanticVersion versionToTest,
            string behaviorType,
            string generatedProjectFolder,
            PluginOptions opts)
        {
            projectName = $"TestAgent.V{versionToTest.ToNormalizedString()}"; //generated project depends on downstream minor
            this.versionToTest = versionToTest;
            behaviorPackageName = $"WireCompatibilityTests.TestBehaviors.V{versionToTest.Major}"; //behaviors depend only on downstream major
            this.behaviorType = $"{behaviorType}, WireCompatibilityTests.TestBehaviors.V{versionToTest.Major}";
            this.platformSpecificAssemblies = platformSpecificAssemblies;
            this.generatedProjectFolder = generatedProjectFolder;
            this.opts = opts;
            transportPackageName = versionToTest.Major > 5 ? "NServiceBus.Transport.SqlServer" : "NServiceBus.SqlServer";
        }


        public async Task Compile(CancellationToken cancellationToken = default)
        {
            var disposable = await Locks.LockAsync(projectName, cancellationToken).ConfigureAwait(false);

            using (disposable)
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

    <ProjectReference Include=""..\..\{behaviorPackageName}\{behaviorPackageName}.csproj"" />

    <PackageReference Include=""{transportPackageName}"" Version=""{versionToTest.ToNormalizedString()}"" />

  </ItemGroup>

</Project>
", cancellationToken).ConfigureAwait(false);
                }

                var buildProcess = new Process();
                buildProcess.StartInfo.FileName = @"dotnet";
                buildProcess.StartInfo.Arguments = $"build \"{projectFilePath}\"";
#if !DEBUG
                buildProcess.StartInfo.Arguments += " --configuration Release";
#endif
                buildProcess.StartInfo.UseShellExecute = false;
                buildProcess.StartInfo.RedirectStandardOutput = true;
                buildProcess.StartInfo.RedirectStandardError = true;
                buildProcess.StartInfo.RedirectStandardInput = true;
                buildProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                buildProcess.StartInfo.CreateNoWindow = true;

                buildProcess.Start();

                await buildProcess.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

                if (buildProcess.ExitCode != 0)
                {
                    var buildOutput = await buildProcess.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                    await Console.Out.WriteLineAsync(buildOutput).ConfigureAwait(false);
                    throw new Exception("Build failed");
                }

                var folder = Path.GetDirectoryName(projectFilePath);
#if DEBUG
                var agentDllPath = Directory.EnumerateFiles($"{folder}/bin/Debug/net6.0/", "TestAgent.Framework.V*.dll").Single();
#else
                var agentDllPath = Directory.EnumerateFiles($"{folder}/bin/Release/net6.0/", "TestAgent.Framework.V*.dll").Single();
#endif

                if (!File.Exists(agentDllPath))
                {
                    throw new FileNotFoundException();
                }

                var pluginAssembly = LoadPlugin(agentDllPath);
                plugin = CreateCommands(pluginAssembly).Single();
                projects.Add(projectName);
            }
        }

        public async Task StartEndpoint(CancellationToken cancellationToken = default)
        {
            await plugin.StartEndpoint(behaviorType, opts, cancellationToken).ConfigureAwait(false);
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

        Assembly LoadPlugin(string pluginLocation)
        {
            var loadContext = new PluginLoadContext(pluginLocation, platformSpecificAssemblies);

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
