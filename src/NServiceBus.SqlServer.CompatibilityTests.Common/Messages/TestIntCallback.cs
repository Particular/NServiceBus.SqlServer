namespace NServiceBus.SqlServer.CompatibilityTests.Common.Messages
{
    using System;

    [Serializable]
    public class TestIntCallback
    {
        public int Response { get; set; }
    }
}
