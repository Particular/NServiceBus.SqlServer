namespace NServiceBus.Transport.SqlServer;

interface IQueueAddressTranslator
{
    QueueAddress Generate(Transport.QueueAddress queueAddress);
    CanonicalQueueAddress Parse(string address);
    CanonicalQueueAddress TranslatePhysicalAddress(string address);
    CanonicalQueueAddress GetCanonicalForm(QueueAddress transportAddress);
}