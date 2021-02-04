namespace NServiceBus.Transport.SqlServer.AcceptanceTests.TransactionScope
{
    using System;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_using_scope_timeout_greater_than_machine_max : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_throw()
        {
            Requires.DtcSupport();

            var exception = Assert.ThrowsAsync<Exception>(async () =>
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
                EndpointSetup<DefaultServer>(c =>
                {
                    var transport = c.ConfigureSqlServerTransport();
                    transport.TransportTransactionMode = TransportTransactionMode.TransactionScope;
                    transport.TransactionScope.Timeout = TimeSpan.FromHours(1);
                });
            }
        }
    }
}