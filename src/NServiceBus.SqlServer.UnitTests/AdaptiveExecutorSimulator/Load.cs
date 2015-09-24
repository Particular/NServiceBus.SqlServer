namespace NServiceBus.SqlServer.UnitTests
{
    using System;
    using System.Collections.Generic;

    class Load
    {
        readonly List<Stage> stages = new List<Stage>();
        int currentStage;
        long currentGlobalTime;
        int totalSentMessages;
        int totalMessages;
        Action<Message> enqueueAction;

        public void Connect(Action<Message> enqueueAction)
        {
            this.enqueueAction = enqueueAction;
        }

        public int TotalSentMessages
        {
            get { return totalSentMessages; }
        }

        public int TotalMessages
        {
            get { return totalMessages; }
        }

        public void TimePassed(long newCurrentGlobalTime)
        {
            if (currentStage >= stages.Count)
            {
                return;
            }
            var timePassed = newCurrentGlobalTime - currentGlobalTime;

            while (timePassed > 0 && currentStage < stages.Count)
            {
                timePassed -= stages[currentStage].ConsumeTimePassed(timePassed);
                if (stages[currentStage].Complete)
                {
                    currentStage++;
                }
            }
            currentGlobalTime = newCurrentGlobalTime;
        }

        public Load AddStage(long length, long period, Func<long> processingTime)
        {
            var stage = new Stage(length, period, () =>
            {
                totalSentMessages++;
                enqueueAction(new Message(processingTime()));
            });
            totalMessages += (int)(length/period);
            stages.Add(stage);
            return this;
        }
    }
}