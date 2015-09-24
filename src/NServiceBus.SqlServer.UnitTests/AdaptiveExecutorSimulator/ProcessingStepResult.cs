namespace NServiceBus.SqlServer.UnitTests
{
    class ProcessingStepResult
    {
        readonly long sleepFor;
        readonly bool busyProcessingMessage;

        public ProcessingStepResult(long sleepFor, bool busyProcessingMessage)
        {
            this.sleepFor = sleepFor;
            this.busyProcessingMessage = busyProcessingMessage;
        }

        public long SleepFor
        {
            get { return sleepFor; }
        }

        public bool BusyProcessingMessage
        {
            get { return busyProcessingMessage; }
        }
    }
}