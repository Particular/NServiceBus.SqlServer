namespace TestRunner
{
    using System;
    using System.IO;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading.Tasks;

    class AgentProcess
    {
        readonly Process process;
        Task<string> outputTask;
        Task<string> errorTask;
        bool started;

        public AgentProcess(string projectName, string behaviorType, string mappedFileName, Dictionary<string, string> args)
        {
            var projectFilePath = GetProjectFilePath(projectName);
            process = new Process();
            process.StartInfo.FileName = @"dotnet";
            process.StartInfo.Arguments = $"run --project \"{projectFilePath}\" \"{behaviorType}\" \"{mappedFileName}\" {string.Join(" ", args.Select(kvp => FormatArgument(kvp)))}";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            process.StartInfo.RedirectStandardInput = true;
            process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            process.StartInfo.CreateNoWindow = true;
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

        public void Start()
        {
            started = process.Start();

            outputTask = process.StandardOutput.ReadToEndAsync();
            errorTask = process.StandardError.ReadToEndAsync();
        }

#pragma warning disable PS0018
        public async Task Stop()
#pragma warning restore PS0018
        {
            if (!started)
            {
                return;
            }

            process.Kill();

            Trace.WriteLine(await outputTask.ConfigureAwait(false));
            Trace.WriteLine(await errorTask.ConfigureAwait(false));

            process.Dispose();
        }
    }
}