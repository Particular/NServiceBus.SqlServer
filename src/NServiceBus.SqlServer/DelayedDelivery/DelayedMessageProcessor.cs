namespace NServiceBus.Transport.SQLServer
{
    using System.Threading.Tasks;
    using Routing;

    class DelayedMessageProcessor
    {
        const string ForwardHeader = "NServiceBus.SqlServer.ForwardDestination";

        public DelayedMessageProcessor(IDispatchMessages dispatcher, string localAddress)
        {
            this.dispatcher = dispatcher;
            this.localAddress = localAddress;
        }

        public async Task<bool> Handle(MessageContext context)
        {
            context.Headers.TryGetValue(ForwardHeader, out var forwardDestination);
            if (forwardDestination == null)
            {
                //This is not a delayed message. Process in local endpoint instance.
                return false;
            }
            if (forwardDestination == localAddress)
            {
                context.Headers.Remove(ForwardHeader);
                //Do not forward the message. Process in local endpoint instance.
                return false;
            }
            var outgoingMessage = new OutgoingMessage(context.MessageId, context.Headers, context.Body);
            var transportOperation = new TransportOperation(outgoingMessage, new UnicastAddressTag(forwardDestination));
            context.Headers.Remove(ForwardHeader);
            await dispatcher.Dispatch(new TransportOperations(transportOperation), context.TransportTransaction, context.Extensions).ConfigureAwait(false);
            return true;
        }

        public void HandleError(ErrorContext context)
        {
            context.Message.Headers.Remove(ForwardHeader);
        }

        IDispatchMessages dispatcher;
        string localAddress;
    }
}