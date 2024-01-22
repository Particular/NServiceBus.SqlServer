namespace NServiceBus.Transport.PostgreSql;

using SqlServer;

class PostgreSqlQueueAddressTranslator : IQueueAddressTranslator
{
    public QueueAddress Generate(Transport.QueueAddress queueAddress) => throw new System.NotImplementedException();

    public CanonicalQueueAddress Parse(string address) => throw new System.NotImplementedException();

    public CanonicalQueueAddress TranslatePhysicalAddress(string address) => throw new System.NotImplementedException();

    public CanonicalQueueAddress GetCanonicalForm(QueueAddress transportAddress) => throw new System.NotImplementedException();
}