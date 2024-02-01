namespace NServiceBus.Transport.Sql
{
    record struct ConnectionAttributes(string Catalog, bool IsEncrypted);
}
