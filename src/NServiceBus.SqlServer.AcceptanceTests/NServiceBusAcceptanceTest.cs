namespace NServiceBus.AcceptanceTests
{
    using AcceptanceTesting.Customization;
    using NUnit.Framework;
    using System;

    /// <summary>
    /// Base class for all the NSB test that sets up our conventions
    /// </summary>
    [TestFixture]
// ReSharper disable once PartialTypeWithSinglePart
    public abstract partial class NServiceBusAcceptanceTest
    {
        [SetUp]
        public void SetUp()
        {
            Environment.SetEnvironmentVariable(
                "SqlServer.ConnectionString",
                @"Data Source=.\SQLEXPRESS;Initial Catalog=NServiceBus.SqlServer.AcceptanceTests;Integrated Security=True; Queue Schema=mySchema"
            );

            Conventions.EndpointNamingConvention= t =>
                {
                    var baseNs = typeof (NServiceBusAcceptanceTest).Namespace;
                    var testName = GetType().Name;
                    return t.FullName.Replace(baseNs + ".", "").Replace(testName + "+", "")
                            + "." + System.Threading.Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(testName).Replace("_", "");
                };

            Conventions.DefaultRunDescriptor = () => ScenarioDescriptors.Transports.Default;
        }
    }
}