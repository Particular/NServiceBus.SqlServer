namespace NServiceBus.SqlServer.UnitTests
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Extensibility;
    using NUnit.Framework;
    using Routing;
    using Transports;
    using Transports.SQLServer;

    [TestFixture]
    public class MessageDispatcherTests
    {
        [TestCaseSource(nameof(TestCases))]
        public async Task It_deduplicates_based_on_message_id_and_address(TransportOperations transportOperations, int expectedDispatchedMessageCount)
        {
            var queueDispatcher = new FakeTableBasedQueueDispatcher();

            var dispatcher = new MessageDispatcher(queueDispatcher, new QueueAddressParser("dbo", null, s => null));

            await dispatcher.Dispatch(transportOperations, new ContextBag());

            Assert.AreEqual(expectedDispatchedMessageCount, queueDispatcher.DispatchedMessageIds.Count);
        }

        static object[] TestCases =
        {
            new object[]
            {
                new TransportOperations(
                    CreateTransportOperations("1", "Destination@dbo"),
                    CreateTransportOperations("1", "Destination")
                ),
                1
            },
            new object[]
            {
                new TransportOperations(
                    CreateTransportOperations("1", "Destination@dbo"),
                    CreateTransportOperations("1", "Destination@someSchema")
                ),
                2
            },
            new object[]
            {
                new TransportOperations(
                    CreateTransportOperations("1", "Destination@dbo"),
                    CreateTransportOperations("1", "Destination@someSchema"),
                    CreateTransportOperations("1", "Destination")
                ),
                2
            },
            new object[]
            {
                new TransportOperations(
                    CreateTransportOperations("1", "Destination@dbo"),
                    CreateTransportOperations("2", "Destination")
                ),
                2
            }
        };

        static TransportOperation CreateTransportOperations(string messageId, string destination)
        {
            return new TransportOperation(new OutgoingMessage(messageId, new Dictionary<string, string>(), new byte[0]), new UnicastAddressTag(destination));
        }

        class FakeTableBasedQueueDispatcher : TableBasedQueueDispatcher
        {
            public List<string> DispatchedMessageIds = new List<string>();

            public FakeTableBasedQueueDispatcher() 
                : base(null)
            {
            }

            public override Task DispatchAsIsolated(List<MessageWithAddress> isolatedConsistencyOperations)
            {
                DispatchedMessageIds.AddRange(isolatedConsistencyOperations.Select(x => x.Message.MessageId));
                return Task.FromResult(0);
            }

            public override Task DispatchOperationsWithNewConnectionAndTransaction(List<MessageWithAddress> defaultConsistencyOperations)
            {
                DispatchedMessageIds.AddRange(defaultConsistencyOperations.Select(x => x.Message.MessageId));
                return Task.FromResult(0);
            }

            public override Task DispatchUsingReceiveTransaction(TransportTransaction transportTransaction, List<MessageWithAddress> defaultConsistencyOperations)
            {
                DispatchedMessageIds.AddRange(defaultConsistencyOperations.Select(x => x.Message.MessageId));
                return Task.FromResult(0);
            }
        }
    }
}