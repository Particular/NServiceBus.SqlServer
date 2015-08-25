namespace NServiceBus.SqlServer.UnitTests
{
    using System;

    class BackOff
    {
        int maximum;
        int currentDelay = 50;

        public BackOff(int maximum)
        {
            this.maximum = maximum;
        }

        public long Wait(Func<bool> condition)
        {
            if (!condition())
            {
                currentDelay = 50;
                return 0;
            }
            var result = currentDelay;

            if (currentDelay < maximum)
            {
                currentDelay *= 2;
            }

            if (currentDelay > maximum)
            {
                currentDelay = maximum;
            }
            return result;
        }
    }
}