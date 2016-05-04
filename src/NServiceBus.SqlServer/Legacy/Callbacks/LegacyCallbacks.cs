namespace NServiceBus.Transports.SQLServer
{
    using System.Collections.Generic;

    class LegacyCallbacks
    {
        public static void SubstituteReplyToWithCallbackQueueIfExists(Dictionary<string, string> headers)
        {
            string callbackQueueValue;

            if (headers.TryGetValue("NServiceBus.SqlServer.CallbackQueue", out callbackQueueValue))
            {
                headers[Headers.ReplyToAddress] = callbackQueueValue;
            }
        }
    }
}