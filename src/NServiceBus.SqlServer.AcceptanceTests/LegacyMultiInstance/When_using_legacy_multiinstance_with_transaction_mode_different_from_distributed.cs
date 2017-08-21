#if NET452
namespace NServiceBus.SqlServer.AcceptanceTests.LegacyMultiInstance
{
#pragma warning disable 0618
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NUnit.Framework;
    using Transport.SQLServer;

    public class When_using_legacy_multiinstance_with_transaction_mode_different_from_distributed : When_using_legacy_multiinstance
    {
        [TestCase(TransportTransactionMode.None)]
        [TestCase(TransportTransactionMode.ReceiveOnly)]
        [TestCase(TransportTransactionMode.SendsAtomicWithReceive)]
        public async Task Should_throw(TransportTransactionMode transactionMode)
        {
            try
            {
                await Scenario.Define<Context>()
                    .WithEndpoint<Sender>(b =>
                    {
                        b.CustomConfig(c =>
                        {
#pragma warning disable 0618
                            c.UseTransport<SqlServerTransport>()
                                .DefaultSchema("sender")
                                .EnableLegacyMultiInstanceMode(async address =>
                                {
                                    var connection = new SqlConnection(SenderConnectionString);

                                    await connection.OpenAsync();

                                    return connection;
                                })
                                .Transactions(transactionMode);
#pragma warning restore 0618
                        });
                        b.When((bus, c) =>
                        {
                            bus.Send(new Message());
                            return Task.FromResult(0);
                        });
                    })
                    .WithEndpoint<Receiver>()
                    .Run();
            }
            catch (Exception e)
            {
                Assert.Pass(e.Message);
            }

            Assert.Fail();
        }
    }
}
#endif