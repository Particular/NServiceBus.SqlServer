using System;
#if SYSTEMDATASQLCLIENT
using System.Data.SqlClient;
#else
using Microsoft.Data.SqlClient;
#endif
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.AcceptanceTesting.Support;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Settings;
using NServiceBus.Transport;

public class ConfigureEndpointSqlServerTransport : IConfigureEndpointTestExecution
{
    public Task Configure(string endpointName, EndpointConfiguration configuration, RunSettings runSettings, PublisherMetadata publisherMetadata)
    {
        queueBindings = configuration.GetSettings().Get<QueueBindings>();
        settings = configuration.GetSettings();
        settings.Set("SqlServer.SubscriptionTableQuotedQualifiedNameSetter", (Action<string>)SetSubscriptionTableName);

        doNotCleanNativeSubscriptions = runSettings.TryGet<bool>("DoNotCleanNativeSubscriptions", out _);
        connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString");

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new Exception("The 'SqlServerTransportConnectionString' environment variable is not set.");
        }

        var transportConfig = configuration.UseTransport<SqlServerTransport>();
        transportConfig.ConnectionString(connectionString);
        transportConfig.SubscriptionSettings().DisableSubscriptionCache();

#if !NETFRAMEWORK
        transportConfig.Transactions(TransportTransactionMode.SendsAtomicWithReceive);
#endif
        return Task.FromResult(0);
    }

    void SetSubscriptionTableName(string tableQuotedQualifiedName)
    {
        subscriptionTableQuotedQualifiedName = tableQuotedQualifiedName;
    }

    public async Task Cleanup()
    {
        using (var conn = new SqlConnection(connectionString))
        {
            await conn.OpenAsync().ConfigureAwait(false);

            var queueAddresses = queueBindings.ReceivingAddresses.Select(QueueAddress.Parse);

            var commandTextBuilder = new StringBuilder();
            foreach (var address in queueAddresses)
            {
                var qualifiedName = address.QualifiedTableName;
                commandTextBuilder.AppendLine($"IF OBJECT_ID('{qualifiedName}', 'U') IS NOT NULL DROP TABLE {qualifiedName}");
                var delayedAddressQualifiedName = new QueueAddress(address.Table + ".Delayed", address.Schema, address.Catalog).QualifiedTableName;
                commandTextBuilder.AppendLine($"IF OBJECT_ID('{delayedAddressQualifiedName}', 'U') IS NOT NULL DROP TABLE {delayedAddressQualifiedName}");
            }

            if (!doNotCleanNativeSubscriptions && !string.IsNullOrEmpty(subscriptionTableQuotedQualifiedName))
            {
                commandTextBuilder.AppendLine($"IF OBJECT_ID('{subscriptionTableQuotedQualifiedName}', 'U') IS NOT NULL DROP TABLE {subscriptionTableQuotedQualifiedName}");
            }

            var commandText = commandTextBuilder.ToString();
            if (!string.IsNullOrEmpty(commandText))
            {
                await TryDeleteTables(conn, commandText);
            }
        }
    }

    static async Task TryDeleteTables(SqlConnection conn, string commandText)
    {
        try
        {
            using (var comm = conn.CreateCommand())
            {
                comm.CommandText = commandText;
                await comm.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }
        catch (Exception e)
        {
            if (!e.Message.Contains("it does not exist or you do not have permission"))
            {
                throw;
            }
        }
    }

    bool doNotCleanNativeSubscriptions;
    string connectionString;
    QueueBindings queueBindings;
    SettingsHolder settings;
    string subscriptionTableQuotedQualifiedName;

    class QueueAddress
    {
        public QueueAddress(string table, string schemaName, string catalogName)
        {
            Table = table;
            Catalog = SafeUnquote(catalogName);
            Schema = SafeUnquote(schemaName);
        }

        public string Catalog { get; }
        public string Table { get; }
        public string Schema { get; }

        public static QueueAddress Parse(string address)
        {
            var firstAtIndex = address.IndexOf("@", StringComparison.Ordinal);

            if (firstAtIndex == -1)
            {
                return new QueueAddress(address, null, null);
            }

            var tableName = address.Substring(0, firstAtIndex);
            address = firstAtIndex + 1 < address.Length ? address.Substring(firstAtIndex + 1) : string.Empty;

            address = ExtractNextPart(address, out var schemaName);

            string catalogName = null;

            if (address != string.Empty)
            {
                ExtractNextPart(address, out catalogName);
            }
            return new QueueAddress(tableName, schemaName, catalogName);
        }

        public string QualifiedTableName => $"{Quote(Catalog)}.{Quote(Schema)}.{Quote(Table)}";

        static string ExtractNextPart(string address, out string part)
        {
            var noRightBrackets = 0;
            var index = 1;

            while (true)
            {
                if (index >= address.Length)
                {
                    part = address;
                    return string.Empty;
                }

                if (address[index] == '@' && (address[0] != '[' || noRightBrackets % 2 == 1))
                {
                    part = address.Substring(0, index);
                    return index + 1 < address.Length ? address.Substring(index + 1) : string.Empty;
                }

                if (address[index] == ']')
                {
                    noRightBrackets++;
                }

                index++;
            }
        }

        static string Quote(string name)
        {
            if (name == null)
            {
                return null;
            }
            return prefix + name.Replace(suffix, suffix + suffix) + suffix;
        }

        static string SafeUnquote(string name)
        {
            var result = Unquote(name);
            return string.IsNullOrWhiteSpace(result)
                ? null
                : result;
        }

        const string prefix = "[";
        const string suffix = "]";
        static string Unquote(string quotedString)
        {
            if (quotedString == null)
            {
                return null;
            }

            if (!quotedString.StartsWith(prefix) || !quotedString.EndsWith(suffix))
            {
                return quotedString;
            }

            return quotedString
                .Substring(prefix.Length, quotedString.Length - prefix.Length - suffix.Length).Replace(suffix + suffix, suffix);
        }
    }
}