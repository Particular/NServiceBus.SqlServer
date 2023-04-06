namespace WireCompatibilityTests.TestBehaviors.V7
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NServiceBus;
    using TestComms;
    using TestLogicApi;

    class Sender : ITestBehavior
    {
        public EndpointConfiguration Configure(Dictionary<string, string> args)
        {
            var config = new EndpointConfiguration("Sender");

            var routing = config.UseTransport<LearningTransport>().Routing();
            routing.RouteToEndpoint(typeof(MyRequest), "Receiver");

            return config;
        }

#pragma warning disable PS0018
        public async Task Execute(IEndpointInstance endpointInstance)
#pragma warning restore PS0018
        {
            await endpointInstance.Send(new MyRequest()).ConfigureAwait(false);
        }

        public class MyResponseHandler : IHandleMessages<MyResponse>
        {
            ITestContextAccessor contextAccessor;

            public MyResponseHandler(ITestContextAccessor contextAccessor)
            {
                this.contextAccessor = contextAccessor;
            }

#pragma warning disable PS0018
            public Task Handle(MyResponse message, IMessageHandlerContext context)
#pragma warning restore PS0018
            {
                contextAccessor.SetFlag("ResponseReceived", true);
                contextAccessor.Success();

                return Task.CompletedTask;
            }
        }
    }
}