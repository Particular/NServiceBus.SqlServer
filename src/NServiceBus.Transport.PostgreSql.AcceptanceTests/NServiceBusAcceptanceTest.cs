namespace NServiceBus.AcceptanceTests
{
    using System;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Newtonsoft.Json.Linq;
    using NUnit.Framework;
    using NUnit.Framework.Interfaces;
    using NUnit.Framework.Internal;

    /// <summary>
    /// Base class for all the NSB test that sets up our conventions
    /// </summary>
    [TestFixture]
    public abstract class NServiceBusAcceptanceTest
    {
        [SetUp]
        public void SetUp()
        {
            Conventions.EndpointNamingConvention = t =>
            {
                var classAndEndpoint = t.FullName.Split('.').Last();

                var testName = classAndEndpoint.Split('+').First();

                testName = testName.Replace("When_", "");

                var endpointBuilder = classAndEndpoint.Split('+').Last();

                testName = Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(testName);

                testName = testName.Replace("_", "");

                var fullTestName = testName + "." + endpointBuilder;

                //TODO: shorten the .delayed to get additional symbols for the queue name
                // Max length for table name is 63. We need to reserve space for the:
                // - ".delayed" - suffix (8)
                // - "_Seq_seq" suffix for auto-created sequence backing up the Seq column (8)
                // - hashcode (8)
                // In summary, we can use 63-8-8-8=39
                var charactersToConsider = int.Min(fullTestName.Length, 39);

                return $"{fullTestName.Substring(0, charactersToConsider)}{CreateDeterministicHash(fullTestName):X8}";
            };
        }

        public static uint CreateDeterministicHash(string input)
        {
            using (var provider = MD5.Create())
            {
                var inputBytes = Encoding.Default.GetBytes(input);
                var hashBytes = provider.ComputeHash(inputBytes);
                // generate a guid from the hash:
                return BitConverter.ToUInt32(hashBytes, 0) % 1000000;
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (!TestExecutionContext.CurrentContext.TryGetRunDescriptor(out var runDescriptor))
            {
                return;
            }

            var scenarioContext = runDescriptor.ScenarioContext;

            if (Environment.GetEnvironmentVariable("CI") != "true" || Environment.GetEnvironmentVariable("VERBOSE_TEST_LOGGING")?.ToLower() == "true")
            {
                TestContext.WriteLine($@"Test settings:
{string.Join(Environment.NewLine, runDescriptor.Settings.Select(setting => $"   {setting.Key}: {setting.Value}"))}");

                TestContext.WriteLine($@"Context:
{string.Join(Environment.NewLine, scenarioContext.GetType().GetProperties().Select(p => $"{p.Name} = {p.GetValue(scenarioContext, null)}"))}");
            }

            if (TestExecutionContext.CurrentContext.CurrentResult.ResultState == ResultState.Failure || TestExecutionContext.CurrentContext.CurrentResult.ResultState == ResultState.Error)
            {
                TestContext.WriteLine(string.Empty);
                TestContext.WriteLine($"Log entries (log level: {scenarioContext.LogLevel}):");
                TestContext.WriteLine("--- Start log entries ---------------------------------------------------");
                foreach (var logEntry in scenarioContext.Logs)
                {
                    TestContext.WriteLine($"{logEntry.Timestamp:T} {logEntry.Level} {logEntry.Endpoint ?? TestContext.CurrentContext.Test.Name}: {logEntry.Message}");
                }
                TestContext.WriteLine("--- End log entries ---------------------------------------------------");
            }
        }
    }
}
