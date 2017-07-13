namespace NServiceBus.AcceptanceTests.Basic
{
    using System.Collections.Generic;
    using NServiceBus.AcceptanceTesting;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    public class When_using_unicode_characters_in_headers : NServiceBusAcceptanceTest
    {
        [Test]
        public void Should_support_unicode_characters()
        {
            var context = new Context();

            var sentHeaders = new Dictionary<string, string>
            {
                {"a-B1", "a-B"},
                {"a-B2", "a-ɤϡ֎ᾣ♥-b"},
                {"a-ɤϡ֎ᾣ♥-B3", "a-B"},
                {"a-B4", "a-\U0001F60D-b"},
                {"a-\U0001F605-B5", "a-B"},
                {"a-B6", "a-😍-b"},
                {"a-😅-B7", "a-B"}
            };

            Scenario.Define(context)
                .WithEndpoint<Endpoint>(b => b.Given(bus =>
                {
                    var message = new TestMessage();
                    foreach (var header in sentHeaders)
                    {
                        bus.SetMessageHeader(message, header.Key, header.Value);
                    }
                    bus.SendLocal(message);
                }))
                .Done(c => c.Done)
                .Run();

            Assert.IsNotEmpty(context.Headers);
            CollectionAssert.IsSubsetOf(sentHeaders, context.Headers);
        }

        public class Context : ScenarioContext
        {
            public bool Done { get; set; }
            public Dictionary<string, string> Headers { get; set; }
        }

        public class Endpoint : EndpointConfigurationBuilder
        {
            public Endpoint()
            {
                EndpointSetup<DefaultServer>();
            }

            class Handler : IHandleMessages<TestMessage>
            {
                public Context Context { get; set; }
                public IBus Bus { get; set; }

                public void Handle(TestMessage message)
                {
                    Context.Headers = new Dictionary<string, string>(Bus.CurrentMessageContext.Headers);
                    Context.Done = true;
                }
            }
        }

        public class TestMessage : IMessage
        {
        }
    }
}