namespace NServiceBus.Transport.PostgreSql;

using SqlServer;

class PostgreSqlConstants : ISqlConstants
{
    public string PurgeText { get; set; } = string.Empty;
    public string SendTextWithRecoverable { get; set; } = string.Empty;
    public string SendText { get; set; } = string.Empty;
    public string CheckIfTableHasRecoverableText { get; set; } = string.Empty;
    public string StoreDelayedMessageText { get; set; } = string.Empty;
    public string ReceiveText { get; set; } = string.Empty;
    public string MoveDueDelayedMessageText { get; set; } = string.Empty;
    public string PeekText { get; set; } = string.Empty;
    public string AddMessageBodyStringColumn { get; set; } = string.Empty;
    public string CreateQueueText { get; set; } = string.Empty;
    public string CreateDelayedMessageStoreText { get; set; } = string.Empty;
    public string PurgeBatchOfExpiredMessagesText { get; set; } = string.Empty;
    public string CheckIfExpiresIndexIsPresent { get; set; } = string.Empty;
    public string CheckIfNonClusteredRowVersionIndexIsPresent { get; set; } = string.Empty;
    public string CheckHeadersColumnType { get; set; } = string.Empty;

    //HINT: https://stackoverflow.com/questions/1766046/postgresql-create-table-if-not-exists
    public string CreateSubscriptionTableText { get; set; } = "CREATE TABLE IF NOT EXISTS public.mytable (i integer);";
    public string SubscribeText { get; set; } = string.Empty;
    public string GetSubscribersText { get; set; } = string.Empty;
    public string UnsubscribeText { get; set; } = string.Empty;
}