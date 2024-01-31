namespace NServiceBus.Transport.PostgreSql;

using SqlServer;

class PostgreSqlConstants : ISqlConstants
{
    public string PurgeText { get; set; }
    public string SendTextWithRecoverable { get; set; }
    public string SendText { get; set; }
    public string CheckIfTableHasRecoverableText { get; set; }
    public string StoreDelayedMessageText { get; set; }
    public string ReceiveText { get; set; }
    public string MoveDueDelayedMessageText { get; set; }
    public string PeekText { get; set; }
    public string AddMessageBodyStringColumn { get; set; }
    public string CreateQueueText { get; set; }
    public string CreateDelayedMessageStoreText { get; set; }
    public string PurgeBatchOfExpiredMessagesText { get; set; }
    public string CheckIfExpiresIndexIsPresent { get; set; }
    public string CheckIfNonClusteredRowVersionIndexIsPresent { get; set; }
    public string CheckHeadersColumnType { get; set; }

    //HINT: https://stackoverflow.com/questions/1766046/postgresql-create-table-if-not-exists
    public string CreateSubscriptionTableText { get; set; } = "CREATE TABLE IF NOT EXISTS public.mytable (i integer);";
    public string SubscribeText { get; set; } = string.Empty;
    public string GetSubscribersText { get; set; }
    public string UnsubscribeText { get; set; } = string.Empty;
}