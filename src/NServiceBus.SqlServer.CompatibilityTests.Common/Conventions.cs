namespace CompatibilityTests.Common
{
    using System;
    using System.IO;
    using NUnit.Framework;

    public class Conventions
    {
        static bool RunningOnTeamCity()
        {
            var teamcityVersion = Environment.GetEnvironmentVariable("TEAMCITY_VERSION");

            return !string.IsNullOrEmpty(teamcityVersion);
        }

        static readonly Func<string, string> AssemblyNameResolver =
            version => $"Facade_{version}";

        public static Func<string, string> AssemblyDirectoryResolver =
            version =>
            {
                // ReSharper disable once RedundantAssignment
                var configuration = "Release";

                #if DEBUG
                configuration = "Debug";
                #endif

                var assemblyName = AssemblyNameResolver(version);

                return RunningOnTeamCity()
                    ? Path.Combine(TestContext.CurrentContext.WorkDirectory, $"src\\CompatibilityTests\\Facades\\{assemblyName}\\bin\\{configuration}")
                    : Path.Combine(TestContext.CurrentContext.TestDirectory, $"..\\..\\..\\CompatibilityTests\\{assemblyName}\\bin\\{configuration}");
            };

        public static Func<string, string> AssemblyPathResolver =
            version =>
            {
                var assemblyDirectory = new DirectoryInfo(AssemblyDirectoryResolver(version));

                return Path.Combine(assemblyDirectory.FullName, "Facade.dll");
            };

        public static Func<string, string> EndpointFacadeConfiguratorTypeNameResolver = version => "EndpointFacade";
    }
}