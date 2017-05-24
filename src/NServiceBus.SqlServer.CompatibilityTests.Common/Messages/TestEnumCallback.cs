namespace CompatibilityTests.Common.Messages
{
    using System;

    public enum CallbackEnum { One, Two, Three }

    [Serializable]
    public class TestEnumCallback
    {
        public CallbackEnum CallbackEnum { get; set; }
    }
}
