namespace NServiceBus.Transport.SqlServer;

internal interface IQueueAddressTranslator
{
    QueueAddress Generate(Transport.QueueAddress queueAddress);
    CanonicalQueueAddress Parse(string address);
    CanonicalQueueAddress TranslatePhysicalAddress(string address);
    CanonicalQueueAddress GetCanonicalForm(QueueAddress transportAddress);
}