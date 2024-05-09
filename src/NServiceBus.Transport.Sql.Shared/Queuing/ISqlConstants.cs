namespace NServiceBus.Transport.Sql.Shared.Queuing;

public interface ISqlConstants
{
    string PurgeText { get; set; }
    string StoreDelayedMessageText { get; set; }
    string ReceiveText { get; set; }
    string MoveDueDelayedMessageText { get; set; }
    string PeekText { get; set; }
    string AddMessageBodyStringColumn { get; set; }
    string CreateQueueText { get; set; }
    string CreateDelayedMessageStoreText { get; set; }
    string CreateSubscriptionTableText { get; set; }
    string SubscribeText { get; set; }
    string GetSubscribersText { get; set; }
    string UnsubscribeText { get; set; }
}