namespace NServiceBus.SqlServer.AcceptanceTests.TransactionScope
{
    using System;
    using System.Configuration;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;
    using Transport.SQLServer;

    public class When_using_scope_timeout_greater_than_machine_max : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_throw()
        {
            var exception = Assert.ThrowsAsync<ConfigurationErrorsException>(async () =>
            {
                await Scenario.Define<Context>()
                    .WithEndpoint<Endpoint>()
                    .Run();
            });

            Assert.That(exception.Message, Contains.Substring("Timeout requested is longer than the maximum value for this machine"));
        }

        class Context : ScenarioContext
        {
        }

        class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>(busConfiguration =>
                {
                    busConfiguration.UseTransport<SqlServerTransport>()
                        .Transactions(TransportTransactionMode.TransactionScope)
                        .TransactionScopeOptions(TimeSpan.FromHours(1));
                });
            }
        }
    }
}