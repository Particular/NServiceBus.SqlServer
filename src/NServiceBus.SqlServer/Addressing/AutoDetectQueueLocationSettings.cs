namespace NServiceBus.Transport.SQLServer
{
    using Configuration.AdvanceExtensibility;
    using Settings;

    /// <summary>
    /// Configures automatic queue location detection
    /// </summary>
    public class AutoDetectQueueLocationSettings : ExposeSettings
    {
        /// <summary>
        /// Creates new instance of auto detect settings.
        /// </summary>        
        public AutoDetectQueueLocationSettings(SettingsHolder settings) : base(settings)
        {
        }

        /// <summary>
        /// Restricts queue location detection to specified allowed schemas.
        /// </summary>
        /// <param name="allowedSchemas">A collection of schemas where to look for queues.</param>
        public AutoDetectQueueLocationSettings InSchemas(params string[] allowedSchemas)
        {
            Guard.AgainstNull(nameof(allowedSchemas), allowedSchemas);

            this.GetSettings().Set(AutoDetectQueueLocation.SchemaFilterKey, allowedSchemas);
            return this;
        }

        /// <summary>
        /// Restricts queue location detection to specified allowed catalogs.
        /// </summary>
        /// <param name="allowedCatalogs">A collection of catalogs where to look for queues.</param>
        public AutoDetectQueueLocationSettings InCatalogs(params string[] allowedCatalogs)
        {
            Guard.AgainstNull(nameof(allowedCatalogs), allowedCatalogs);

            this.GetSettings().Set(AutoDetectQueueLocation.CatalogFilterKey, allowedCatalogs);
            return this;
        }

        /// <summary>
        /// Specifies a collection of queues for which the location will not be automatically detected. These queues will
        /// be assumed to be presend in the default schema/catalog unless specified explicitly.
        /// </summary>
        /// <param name="ignoredQueues">A collection of queues to be ignored when detecting locations.</param>
        public AutoDetectQueueLocationSettings IgnoreQueues(params string[] ignoredQueues)
        {
            Guard.AgainstNull(nameof(ignoredQueues), ignoredQueues);

            this.GetSettings().Set(AutoDetectQueueLocation.IgnoredQueuesKey, ignoredQueues);
            return this;
        }
    }
}