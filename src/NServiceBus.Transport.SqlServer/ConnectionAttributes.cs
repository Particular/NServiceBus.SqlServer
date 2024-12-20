namespace NServiceBus.Transport.SqlServer
{
    record struct ConnectionAttributes(string Catalog, bool IsEncrypted);
}
