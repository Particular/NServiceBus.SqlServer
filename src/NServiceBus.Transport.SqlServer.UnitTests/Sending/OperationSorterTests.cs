namespace NServiceBus.Transport.SqlServer.UnitTests.Sending
{
    using System.Collections.Generic;
    using System.Linq;
    using NUnit.Framework;
    using Routing;
    using SqlServer;
    using Transport;

    [TestFixture]
    public class OperationSorterTests
    {
        [TestCaseSource(nameof(TestCases))]
        public void It_deduplicates_based_on_message_id_and_address(TransportOperations transportOperations, int expectedDispatchedMessageCount)
        {
            var queueAddressTranslator = new QueueAddressTranslator("nservicebus", "dbo", null, null);

            var sortResult = transportOperations.UnicastTransportOperations.SortAndDeduplicate(queueAddressTranslator);

            Assert.AreEqual(expectedDispatchedMessageCount, sortResult.DefaultDispatch.Count());
            Assert.IsNull(sortResult.IsolatedDispatch);
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

        [Test]
        public void It_sorts_isolated_and_default_dispatch()
        {
            var queueAddressTranslator = new QueueAddressTranslator("nservicebus", "dbo", null, null);

            var operations = new TransportOperations(
                CreateTransportOperations("1", "dest", DispatchConsistency.Default),
                CreateTransportOperations("2", "dest", DispatchConsistency.Isolated));

            var sortResult = operations.UnicastTransportOperations.SortAndDeduplicate(queueAddressTranslator);

            Assert.AreEqual(1, sortResult.DefaultDispatch.Count());
            Assert.AreEqual(1, sortResult.IsolatedDispatch.Count());
        }

        static TransportOperation CreateTransportOperations(string messageId, string destination, DispatchConsistency dispatchConsistency = DispatchConsistency.Default)
        {
            return new TransportOperation(new OutgoingMessage(messageId, new Dictionary<string, string>(), new byte[0]), new UnicastAddressTag(destination), requiredDispatchConsistency: dispatchConsistency);
        }
    }
}