namespace NServiceBus.SqlServer.UnitTests
{
    using System;
    using System.Collections.Generic;

    class Load
    {
        private readonly List<Message> queue = new List<Message>();
        readonly List<Stage> stages = new List<Stage>();
        int currentStage;
        long currentGlobalTime;
        int totalMessages;

        public bool HasMoreMessages
        {
            get { return queue.Count > 0 || currentStage < stages.Count; }
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

        public Message TryDequeue()
        {
            if (queue.Count > 0)
            {
                var result = queue[0];
                queue.RemoveAt(0);
                return result;
            }
            return null;
        }

        public Load AddStage(long length, long period, Func<long> processingTime)
        {
            var stage = new Stage(length, period, () =>
            {
                totalMessages++;
                queue.Add(new Message(processingTime()));
            });
            stages.Add(stage);
            return this;
        }
    }
}