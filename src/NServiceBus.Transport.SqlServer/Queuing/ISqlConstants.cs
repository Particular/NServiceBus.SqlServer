namespace NServiceBus.Transport.SqlServer;

// TODO: Move to NServiceBus.Transport.Sql assembly
interface ISqlConstants
{
    string PurgeText { get; set; }
    string SendTextWithRecoverable { get; set; }
    string SendText { get; set; }
    string CheckIfTableHasRecoverableText { get; set; }
    string StoreDelayedMessageText { get; set; }
    string ReceiveText { get; set; }
    string MoveDueDelayedMessageText { get; set; }
    string PeekText { get; set; }
    string AddMessageBodyStringColumn { get; set; }
    string CreateQueueText { get; set; }
    string CreateDelayedMessageStoreText { get; set; }
    string PurgeBatchOfExpiredMessagesText { get; set; }
    string CheckIfExpiresIndexIsPresent { get; set; }
    string CheckIfNonClusteredRowVersionIndexIsPresent { get; set; }
    string CheckHeadersColumnType { get; set; }
    string CreateSubscriptionTableText { get; set; }
    string SubscribeText { get; set; }
    string GetSubscribersText { get; set; }
    string UnsubscribeText { get; set; }
}