namespace NServiceBus.Transports.SQLServer
{
    using NServiceBus.Configuration.AdvanceExtensibility;

    /// <summary>
    /// Adds configuration options specific to the Sql Server transport.
    /// </summary>
    public static class SqlServerTransportSettingsExtensions
    {
        /// <summary>
        /// Overrides the default schema (dbo) used for queue tables.
        /// </summary>
        /// <param name="transportExtensions"></param>
        /// <param name="schemaName">Name of the schema to use instead of default value.</param>
        public static TransportExtensions<SqlServerTransport> DefaultSchema(this TransportExtensions<SqlServerTransport> transportExtensions, string schemaName)
        {
            Guard.AgainstNullAndEmpty(nameof(schemaName), schemaName);

            transportExtensions.GetSettings().Set(ConnectionParams.DefaultSchemaSettingsKey, schemaName);
            return transportExtensions;
        }
    }
}
