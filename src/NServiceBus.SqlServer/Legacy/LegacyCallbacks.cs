namespace NServiceBus.Transport.SQLServer
{
    using System.Collections.Generic;

    class LegacyCallbacks
    {
        public static void SubstituteReplyToWithCallbackQueueIfExists(Dictionary<string, string> headers)
        {

            if (headers.TryGetValue("NServiceBus.SqlServer.CallbackQueue", out var callbackQueueValue))
            {
                headers[Headers.ReplyToAddress] = callbackQueueValue;
            }
        }
    }
}