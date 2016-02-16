namespace NServiceBus
{
    class CallbackAddress
    {
        public string Address { get; }

        public CallbackAddress(string address)
        {
            Address = address;
        }
    }
}