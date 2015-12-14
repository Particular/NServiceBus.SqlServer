using NServiceBus.AcceptanceTests.EndpointTemplates;


namespace NServiceBus.SqlServer.AcceptanceTests.ServiceControl
{
    using System.Configuration;
    using System.Linq;
    using System.Reflection;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests.ScenarioDescriptors;
    using NUnit.Framework;

    class When_an_endpoint_connects_to_multiple_databases
    {

        const string OtherDatabaseConnectionString = @"Server=localhost\sqlexpress;Database=other;Trusted_Connection=True";
        const string ExceptionText = "The transport is running in native SQL Server transactions mode without an outbox";


        [Test]
        public void When_multi_db_via_configuration_it_fails_to_start()
        {
           Scenario.Define<Context>()
                   .WithEndpoint<Receiver>(b => b.CustomConfig(c => AddConnectionString("NServiceBus/Transport/OtherEndpoint", OtherDatabaseConnectionString)))
                   .Done(c => true)
                   .Repeat(r => r.For(Transports.Default))
                   .Should(c => Assert.True(c.Exceptions.Select(ex=>ex.Message).Contains(ExceptionText)))
                   .Run();

        }


        class Context : ScenarioContext
        {
        }

        static void AddConnectionString(string name, string value)
        {
            ConfigurationManager.ConnectionStrings.Add(new ConnectionStringSettings(name, value));
        }

        [SetUp]
        [TearDown]
        public void ClearConnectionStrings()
        {
            var connectionStrings = ConfigurationManager.ConnectionStrings;
            //Setting the read only field to false via reflection in order to modify the connection strings
            var readOnlyField = typeof(ConfigurationElementCollection).GetField("bReadOnly", BindingFlags.Instance | BindingFlags.NonPublic);
            readOnlyField.SetValue(connectionStrings, false);
            connectionStrings.Clear();
        }

        public class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                // this is ServiceControl's current config
                EndpointSetup<DefaultServer>(c => c.Transactions()
                        .DisableDistributedTransactions()
                        .DoNotWrapHandlersExecutionInATransactionScope()
                        );
            }
        }
    }
}
