namespace NServiceBus.SqlServer.AcceptanceTests.LegacyMultiInstance
{
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using NServiceBus.AcceptanceTests;
    using NUnit.Framework;

    public class When_using_legacy_multiinstance_mode_with_custom_schema : When_using_legacy_multiinstance
    {
        [Test]
        public Task Should_be_able_to_send_message_to_input_queue_in_different_database()
        {
            Requires.DtcSupport();

            return Scenario.Define<Context>()
                .WithEndpoint<Sender>(c => c.When(s => s.Send(new Message())))
                .WithEndpoint<Receiver>()
                .Done(c => c.ReplyReceived)
                .Run();
        }
    }
}