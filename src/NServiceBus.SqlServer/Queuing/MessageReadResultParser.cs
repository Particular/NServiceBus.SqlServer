namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.IO;
    using System.Threading.Tasks;
    using Logging;

    static class MessageReadResultParser
    {
        internal static async Task<MessageReadResult> ParseData(SqlDataReader dataReader)
        {
            var messageRow = await MessageRow.ParseRawData(dataReader).ConfigureAwait(false);
            try
            {
                var parsedHeaders = string.IsNullOrEmpty(messageRow.Headers) 
                    ? new Dictionary<string, string>() 
                    : DictionarySerializer.DeSerialize(messageRow.Headers);

                if (!string.IsNullOrEmpty(messageRow.ReplyToAddress))
                {
                    parsedHeaders[Headers.ReplyToAddress] = messageRow.ReplyToAddress;
                }

                var expired = messageRow.TimeToBeReceived.HasValue && messageRow.TimeToBeReceived.Value < 0;
                if (expired)
                {
                    Logger.InfoFormat($"Message with ID={messageRow.Id} has expired. Removing it from queue.");
                    return MessageReadResult.NoMessage;
                }
                return MessageReadResult.Success(new Message(messageRow.Id.ToString(), parsedHeaders, new MemoryStream(messageRow.Body)));
            }
            catch (Exception ex)
            {
                Logger.Error("Error receiving message. Probable message metadata corruption. Moving to error queue.", ex);
                return MessageReadResult.Poison(messageRow);
            }
        }

        static ILog Logger = LogManager.GetLogger(typeof(MessageReadResultParser));
    }
}