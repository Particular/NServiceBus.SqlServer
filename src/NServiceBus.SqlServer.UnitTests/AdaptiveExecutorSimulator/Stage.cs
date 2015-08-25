namespace NServiceBus.SqlServer.UnitTests
{
    using System;

    class Stage
    {
        readonly long length;
        readonly Action enqueueMessage;
        long elapsed;
        long enqueuedMessages;
        long totalMessages;

        public Stage(long length, long period, Action enqueueMessage)
        {
            this.length = length;
            this.enqueueMessage = enqueueMessage;
            totalMessages = length / period;
        }

        public long Length
        {
            get { return length; }
        }

        public bool Complete
        {
            get { return elapsed == length; }
        }

        public long ConsumeTimePassed(long milliseconds)
        {
            var maxToConsume = length - elapsed;
            var consumed = maxToConsume > milliseconds 
                ? milliseconds : maxToConsume;
            elapsed += consumed;
            var messagesSoFar = (elapsed*totalMessages)/Length;
            var toBeGenerated = messagesSoFar - enqueuedMessages;
            for (var i = 0; i < toBeGenerated; i++)
            {
                enqueueMessage();
            }
            enqueuedMessages += toBeGenerated;
            return consumed;
        }
    }
}