namespace NServiceBus.Transports.SQLServer
{
    using System;

    class ExecutorTaskState
    {
        int seen;

        public void Reset()
        {
            seen = 0;
        }

        public void TriggerAnotherTaskIfLongRunning(Action triggerAction)
        {
            seen += 1;
            if (seen == 2)
            {
                triggerAction();
            }
        }
    }
}