namespace NServiceBus.SqlServer.AcceptanceTests
{
    using System.Configuration;
    using System.Reflection;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NServiceBus.Transports.SQLServer;
    using NUnit.Framework;

    public class When_in_native_transaction_mode
    {
        const string OtherDatabaseConnectionString = @"Server=localhost\sqlexpress;Database=other;Trusted_Connection=True";
        const string OtherSchemaConnectionString = @"Server=localhost\sqlexpress;Database=nservicebus;Trusted_Connection=True;Queue Schema=other";
        const string ExceptionText = "The transport is running in native SQL Server transactions mode without an outbox";

        [Test]
        public void When_multi_db_via_configuration_it_fails_to_start()
        {
            var context = new Context();
            Scenario.Define(context)
                   .WithEndpoint<Receiver>(b => b.CustomConfig(c => AddConnectionString("NServiceBus/Transport/OtherEndpoint", OtherDatabaseConnectionString)))
                   .AllowExceptions()
                   .Done(c => true)
                   .Run();

            StringAssert.Contains(ExceptionText, context.Exceptions);
        }
        
        [Test]
        public void When_multi_db_via_code_it_fails_to_start()
        {
            var context = new Context();
            Scenario.Define(context)
                   .WithEndpoint<Receiver>(b => b.CustomConfig(c => c.UseTransport<SqlServerTransport>().UseSpecificConnectionInformation(
                       EndpointConnectionInfo.For("A").UseConnectionString(OtherDatabaseConnectionString)
                       )))
                   .AllowExceptions()
                   .Done(c => true)
                   .Run();

            StringAssert.Contains(ExceptionText, context.Exceptions);
        } 
        
        [Test]
        public void When_multi_db_via_callback_it_fails_to_start_even_when_not_using_other_database()
        {
            var context = new Context();
            Scenario.Define(context)
                   .WithEndpoint<Receiver>(b => b.CustomConfig(c => c.UseTransport<SqlServerTransport>().UseSpecificConnectionInformation(
                       e => ConnectionInfo.Create().UseSchema("nsb")
                       )))
                   .AllowExceptions()
                   .Done(c => true)
                   .Run();

            StringAssert.Contains(ExceptionText, context.Exceptions);
        }

        [Test]
        public void When_multi_schema_via_configuration_it_starts()
        {
            var context = new Context();
            Scenario.Define(context)
                   .WithEndpoint<Receiver>(b => b.CustomConfig(c => AddConnectionString("NServiceBus/Transport/OtherEndpoint", OtherSchemaConnectionString)))
                   .Done(c => true)
                   .Run();

            Assert.IsNull(context.Exceptions);
        }

        [Test]
        public void When_multi_schema_via_code_it_starts()
        {
            var context = new Context();
            Scenario.Define(context)
                   .WithEndpoint<Receiver>(b => b.CustomConfig(c => c.UseTransport<SqlServerTransport>().UseSpecificConnectionInformation(
                       EndpointConnectionInfo.For("A").UseSchema("A"),
                       EndpointConnectionInfo.For("B").UseSchema("B")
                       )))
                   .Done(c => true)
                   .Run();

            Assert.IsNull(context.Exceptions);
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
                EndpointSetup<DefaultServer>(b => b.Transactions().DisableDistributedTransactions());
            }
        }
    }
}