using System;
using System.Diagnostics;

namespace NSB12SampleMessages
{
    public class Statistics
    {
        private readonly string name;
        private readonly int snapshotInterval;
        private readonly Stopwatch stopwatch = new Stopwatch();

        private int counter = 0;
        private long previousElapsed = 0;

        public Statistics(string name, int snapshotInterval)
        {
            this.name = name;
            this.snapshotInterval = snapshotInterval;
        }

        public void Start()
        {
            stopwatch.Start();
        }

        public void MessageProcessed()
        {
            counter++;

            if (counter % snapshotInterval == 0)
            {
                var currentElapsed = stopwatch.ElapsedMilliseconds;
                var intervalElapsed = currentElapsed - previousElapsed;

                Console.WriteLine($"{name} processed: {snapshotInterval} : {intervalElapsed}");

                previousElapsed = currentElapsed;
            }
        }
    }
}