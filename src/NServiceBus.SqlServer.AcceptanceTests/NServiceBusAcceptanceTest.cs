namespace NServiceBus.AcceptanceTests
{
    using System;
    using AcceptanceTesting.Customization;
    using NUnit.Framework;

    /// <summary>
    /// Base class for all the NSB test that sets up our conventions
    /// </summary>
    [TestFixture]    
    public abstract class NServiceBusAcceptanceTest
    {
        [SetUp]
        public void SetUp()
        {
            Environment.SetEnvironmentVariable("Transport.UseSpecific", "SqlServer", EnvironmentVariableTarget.Process);

            Conventions.EndpointNamingConvention= t =>
                {
                    var baseNs = typeof (NServiceBusAcceptanceTest).Namespace;
                    var testName = GetType().Name;
                    return t.FullName.Replace(baseNs + ".", "").Replace(testName + "+", "");
                };

            Conventions.DefaultRunDescriptor = () => ScenarioDescriptors.Transports.Default;
        }
    }
}