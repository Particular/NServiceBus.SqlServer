﻿namespace NServiceBus.Transport.SqlServer.UnitTests.Sending
{
    using System.Linq;
    using NServiceBus.Transport.Sql.Shared;
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

            var sortResult = transportOperations.UnicastTransportOperations.SortAndDeduplicate(s => queueAddressTranslator.Parse(s).Address);

            Assert.Multiple(() =>
            {
                Assert.That(sortResult.DefaultDispatch.Count(), Is.EqualTo(expectedDispatchedMessageCount));
                Assert.That(sortResult.IsolatedDispatch, Is.Null);
            });
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

            var sortResult = operations.UnicastTransportOperations.SortAndDeduplicate(s => queueAddressTranslator.Parse(s).Address);

            Assert.Multiple(() =>
            {
                Assert.That(sortResult.DefaultDispatch.Count(), Is.EqualTo(1));
                Assert.That(sortResult.IsolatedDispatch.Count(), Is.EqualTo(1));
            });
        }

        static TransportOperation CreateTransportOperations(string messageId, string destination, DispatchConsistency dispatchConsistency = DispatchConsistency.Default)
        {
            return new TransportOperation(new OutgoingMessage(messageId, [], new byte[0]), new UnicastAddressTag(destination), requiredDispatchConsistency: dispatchConsistency);
        }
    }
}