namespace NServiceBus.SqlServer.UnitTests
{
    using System;
    using System.Collections.Generic;
    using NServiceBus.Transports.SQLServer;

    class ProcessingThread
    {
        readonly IRampUpController rampUpController;
        readonly Func<Message> receive;
        readonly Action<Message> process;
        readonly Action<string> addEvent;
        bool successfulRead;

        public ProcessingThread(IRampUpController rampUpController, Func<Message> receive, Action<Message> process, Action<string> addEvent)
        {
            this.rampUpController = rampUpController;
            this.receive = receive;
            this.process = process;
            this.addEvent = addEvent;
        }

        public IEnumerable<ProcessingStepResult> ReceiveLoop()
        {
            var backOff = new BackOff(1000);
            while (rampUpController.CheckHasEnoughWork())
            {
                rampUpController.RampUpIfTooMuchWork();
                var message = receive();
                if (message != null)
                {
                    addEvent("Processing started. Time remaining: "+message.ProcessingTime);
                    successfulRead = true;
                    yield return  new ProcessingStepResult(message.ProcessingTime, true);
                }
                else
                {
                    rampUpController.Failed();
                }
                if (successfulRead)
                {
                    process(message);
                    rampUpController.Succeeded();
                    successfulRead = false;
                }
                else
                {
                    yield return new ProcessingStepResult(backOff.Wait(() => message == null), false);                    
                }
            }
        }
    }
}