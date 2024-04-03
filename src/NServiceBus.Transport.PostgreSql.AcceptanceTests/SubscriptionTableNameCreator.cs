using NServiceBus.Transport.PostgreSql;

static class SubscriptionTableNameCreator
{
    public static SubscriptionTableName CreateDefault() => new("SubscriptionRouting", "public");
}