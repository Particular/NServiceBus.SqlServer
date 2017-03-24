namespace NServiceBus.Transport.SQLServer
{
    using System;

    class ForwardingConfiguration
    {
        Func<string, string> addressForwardingFunction;

        public ForwardingConfiguration()
        {
            addressForwardingFunction = s =>
            {
                throw new Exception("Forwarding has not been configured. In order to use multi-instance the forwarding queue has to be specified.");
            };
        }

        public void SetForwardingConfiguration(Func<string, string> addressForwardingFunction)
        {
            this.addressForwardingFunction = addressForwardingFunction;
        }

        public string GetForwardAddress(string catalog)
        {
            return addressForwardingFunction(catalog);
        } 
    }
}