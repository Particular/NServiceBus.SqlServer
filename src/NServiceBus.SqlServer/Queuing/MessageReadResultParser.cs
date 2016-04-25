namespace NServiceBus.Transports.SQLServer
{
    using System;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Logging;

    static class MessageReadResultParser
    {
        internal static async Task<MessageReadResult> ParseData(SqlDataReader dataReader)
        {
            try
            {
                var message = await MessageParser.ParseRawData(dataReader).ConfigureAwait(false);

                if (message.TTBRExpired)
                {
                    var messageId = message.GetLogicalId() ?? message.TransportId;

                    Logger.InfoFormat($"Message with ID={messageId} has expired. Removing it from queue.");

                    return MessageReadResult.NoMessage;
                }

                return MessageReadResult.Success(message);
            }
            catch (Exception ex)
            {
                Logger.Error("Error receiving message. Probable message metadata corruption. Moving to error queue.", ex);

                // allocations are OK here for poison messages.
                var values = new object[dataReader.FieldCount];
                dataReader.GetValues(values);
                return MessageReadResult.Poison(values);
            }
        }

        static ILog Logger = LogManager.GetLogger(typeof(MessageReadResultParser));
    }
}