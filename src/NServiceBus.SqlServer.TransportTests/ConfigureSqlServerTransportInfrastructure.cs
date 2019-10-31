using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Settings;
using NServiceBus.Transport;
using NServiceBus.Transport.SQLServer;
using NServiceBus.TransportTests;

public class ConfigureSqlServerTransportInfrastructure : IConfigureTransportInfrastructure
{
    public TransportConfigurationResult Configure(SettingsHolder settings, TransportTransactionMode transportTransactionMode)
    {
#if !NET46
        if (transportTransactionMode == TransportTransactionMode.TransactionScope)
        {
            NUnit.Framework.Assert.Ignore("TransactionScope not supported in net core");
        }
#endif
        this.settings = settings;
        settings.Set(transportTransactionMode);
        settings.Set("NServiceBus.SharedQueue", settings.EndpointName());
        var delayedDeliverySettings = new DelayedDeliverySettings();
        delayedDeliverySettings.TableSuffix("Delayed");
        settings.Set(delayedDeliverySettings);

        var pubSubSettings = new PubSubSettings();
        pubSubSettings.DisableSubscriptionCache();
        settings.Set(pubSubSettings);

        connectionString = Environment.GetEnvironmentVariable("SqlServerTransportConnectionString");
        if (string.IsNullOrEmpty(connectionString))
        {
            connectionString = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True";
        }

        var logicalAddress = LogicalAddress.CreateLocalAddress(settings.ErrorQueueAddress(), new Dictionary<string, string>());
        var localAddress = settings.EndpointName();
        return new TransportConfigurationResult
        {
            TransportInfrastructure = new SqlServerTransportInfrastructure("nservicebus", settings, connectionString, () => localAddress, () => logicalAddress)
        };
    }

    public async Task Cleanup()
    {
        if (settings == null)
        {
            return;
        }
        var queueBindings = settings.Get<QueueBindings>();
        var queueNames = new List<string>();

        using (var conn = new SqlConnection(connectionString))
        {
            await conn.OpenAsync();

            var qn = queueBindings.ReceivingAddresses.ToList();
            qn.ForEach(n =>
            {
                var nameParts = n.Split('@');
                if (nameParts.Length == 2)
                {
                    var sanitizedSchemaName = SanitizeIdentifier(nameParts[1]);
                    var sanitizedTableName = SanitizeIdentifier(nameParts[0]);

                    queueNames.Add($"{sanitizedSchemaName}.{sanitizedTableName}");
                }
                else
                {
                    queueNames.Add(n);
                }
            });
            foreach (var queue in queueNames)
            {
                using (var comm = conn.CreateCommand())
                {
                    comm.CommandText = $"IF OBJECT_ID('{queue}', 'U') IS NOT NULL DROP TABLE {queue}";
                    await comm.ExecuteNonQueryAsync();
                }
            }
        }
    }

    static string SanitizeIdentifier(string identifier)
    {
        // Identifier may initially quoted or unquoted.
        return Quote(Unquote(identifier));
    }

    static string Quote(string unquotedName)
    {
        if (unquotedName == null)
        {
            return null;
        }
        return prefix + unquotedName.Replace(suffix, suffix + suffix) + suffix;
    }

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

    SettingsHolder settings;
    string connectionString;

    const string prefix = "[";
    const string suffix = "]";
}