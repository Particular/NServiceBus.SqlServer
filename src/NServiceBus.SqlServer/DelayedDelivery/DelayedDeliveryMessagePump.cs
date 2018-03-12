namespace NServiceBus.Transport.SQLServer
{
    using System;
    using System.Threading.Tasks;

    class DelayedDeliveryMessagePump : IPushMessages
    {
        public DelayedDeliveryMessagePump(IPushMessages pump, DelayedMessageProcessor delayedMessageProcessor)
        {
            this.pump = pump;
            this.delayedMessageProcessor = delayedMessageProcessor;
        }

        public Task Init(Func<MessageContext, Task> onMessage, Func<ErrorContext, Task<ErrorHandleResult>> onError, CriticalError criticalError, PushSettings settings)
        {
            delayedMessageProcessor.Init(settings.InputQueue);
            return pump.Init(async context =>
            {
                if (await delayedMessageProcessor.Handle(context).ConfigureAwait(false))
                {
                    return;
                }
                await onMessage(context).ConfigureAwait(false);
            }, context =>
            {
                delayedMessageProcessor.HandleError(context);
                return onError(context);
            }, criticalError, settings);
        }

        public void Start(PushRuntimeSettings limitations)
        {
            pump.Start(limitations);
        }

        public Task Stop()
        {
            return pump.Stop();
        }

        IPushMessages pump;
        DelayedMessageProcessor delayedMessageProcessor;
    }
}