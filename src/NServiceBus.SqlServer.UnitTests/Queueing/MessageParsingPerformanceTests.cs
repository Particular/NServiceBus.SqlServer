namespace NServiceBus.SqlServer.UnitTests.Queueing
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using HdrHistogram;
    using NUnit.Framework;
    using Transports;
    using Transports.SQLServer;

    [TestFixture]
    [Explicit]
    public class MessageParsingPerformanceTests
    {
        static readonly int[] BodySizesInKB =
        {
            1,
            2,
            5,
            7,
            10,
            15,
            20,
            30,
            40,
            50,
            100,
            200,
        };

        const int MessageCount = 20000;

        [Test]
        public async Task Run_performance_test()
        {
            const string address = "MessageParsingPerformanceTests";
            var headers = DictionarySerializer.DeSerialize(@"{
	""NServiceBus.MessageIntent"": ""Send"",

    ""MyStaticHeader"": ""StaticHeaderValue"",
	""NServiceBus.CorrelationId"": ""4e5d5ec5-0b94-4e9e-8aaf-a5f300e65945"",
	""NServiceBus.OriginatingMachine"": ""SIMON-MAC"",
	""NServiceBus.OriginatingEndpoint"": ""SendingToAnotherEndpoint.Sender"",
	""$.diagnostics.originating.hostid"": ""e61203761edc85b955254923ab19f7bf"",
	""NServiceBus.ReplyToAddress"": ""SendingToAnotherEndpoint.Sender@[dbo]"",
	""NServiceBus.ContentType"": ""text/xml"",
	""NServiceBus.EnclosedMessageTypes"": ""NServiceBus.AcceptanceTests.Basic.When_sending_to_another_endpoint+MyMessage, NServiceBus.SqlServer.AcceptanceTests, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null"",
	""NServiceBus.ConversationId"": ""f6526769-8509-4e31-b28b-a5f300e6595b"",
	""NServiceBus.MessageId"": ""4e5d5ec5-0b94-4e9e-8aaf-a5f300e65945"",
	""NServiceBus.Version"": ""6.0.0"",
	""NServiceBus.TimeSent"": ""2016-04-25 11:58:40:519860 Z""
}
");
            var connectionFactory = SqlConnectionFactory.Default(new SqlServerTransport().ExampleConnectionStringForErrorMessage);
            var addressParser = new QueueAddressParser("dbo", null, s => null);
            var queueCreator = new QueueCreator(connectionFactory, addressParser);

            var queueBindings = new QueueBindings();
            queueBindings.BindReceiving(address);

            await queueCreator.CreateQueueIfNecessary(queueBindings, "");
            var table = new TableBasedQueue(addressParser.Parse(address));

            foreach (var bodySize in BodySizesInKB)
            {
                await RunTest(connectionFactory, table, headers, bodySize);
            }
        }

        static async Task RunTest(SqlConnectionFactory connectionFactory, TableBasedQueue table, Dictionary<string, string> headers, int bodySizeInKB)
        {
            using (var connection = await connectionFactory.OpenNewConnection())
            {
                await table.Purge(connection);
            }

            var sendHisto = new LongHistogram(TimeSpan.TicksPerHour, 3);

            var watch = new Stopwatch();
            using (var connection = await connectionFactory.OpenNewConnection())
            {
                var random = new Random();
                watch.Start();

                for (var i = 0; i < MessageCount; i++)
                {
                    var startTimestamp = Stopwatch.GetTimestamp();

                    using (var transaction = connection.BeginTransaction())
                    {
                        await table.SendMessage(new OutgoingMessage(i.ToString(), headers, GenerateBody(random, bodySizeInKB)), connection, transaction);
                        transaction.Commit();
                    }

                    var ticks = Stopwatch.GetTimestamp() - startTimestamp;
                    sendHisto.RecordValue(ticks);
                }

                watch.Stop();

                var at90per = sendHisto.GetValueAtPercentile(90);
                Console.WriteLine($"Sending {MessageCount} {bodySizeInKB}KB messages took {watch.ElapsedMilliseconds} ms. {at90per} @90 percentile");
                sendHisto.OutputPercentileDistribution(Console.Out);
                watch.Reset();
            }

            var histogram = new LongHistogram(TimeSpan.TicksPerHour, 3);

            var successfulReads = 0;
            using (var connection = await connectionFactory.OpenNewConnection())
            {
                watch.Start();
                for (var i = 0; i < MessageCount; i++)
                {
                    var startTimestamp = Stopwatch.GetTimestamp();
                    using (var transaction = connection.BeginTransaction())
                    {
                        var result = await table.TryReceive(connection, transaction);
                        if (result.Successful)
                        {
                            successfulReads++;
                        }
                        transaction.Commit();
                    }
                    var ticks = Stopwatch.GetTimestamp() - startTimestamp;
                    histogram.RecordValue(ticks);
                }
                watch.Stop();
                var perMessage = (double)watch.ElapsedMilliseconds / MessageCount;
                var at90per = histogram.GetValueAtPercentile(90);
                Console.WriteLine($"Receiving {MessageCount} {bodySizeInKB}KB messages took {watch.ElapsedMilliseconds} ms. {perMessage} per message. {at90per} @90 percentile");
                histogram.OutputPercentileDistribution(Console.Out);

            }
            Assert.AreEqual(MessageCount, successfulReads);
        }

        static byte[] GenerateBody(Random random, int bodySizeInKB)
        {
            var buffer = new byte[bodySizeInKB * 1000];
            random.NextBytes(buffer);
            return buffer;
        }
    }
}