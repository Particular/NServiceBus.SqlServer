namespace NServiceBus.SqlServer.UnitTests
{
    class Message
    {
        readonly long processingTime;

        public Message(long processingTime)
        {
            this.processingTime = processingTime;
        }

        public long ProcessingTime
        {
            get { return processingTime; }
        }
    }
}