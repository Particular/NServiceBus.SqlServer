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
        protected const string ConnectionStringWith_test_schema = @"Data Source=.\SQLEXPRESS;Initial Catalog=NServiceBus;Integrated Security=True; Queue Schema=nsb";
        protected const string ConnectionStringWith_sender_schema = @"Data Source=.\SQLEXPRESS;Initial Catalog=NServiceBus;Integrated Security=True; Queue Schema=sender";
        protected const string ConnectionStringWith_receiver_schema = @"Data Source=.\SQLEXPRESS;Initial Catalog=NServiceBus;Integrated Security=True; Queue Schema=receiver";

        [SetUp]
        public void SetUp()
        {
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