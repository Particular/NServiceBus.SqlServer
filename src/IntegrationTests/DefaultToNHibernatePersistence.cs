namespace SqlServer.IntegrationTests
{
    using NServiceBus;
    using NServiceBus.Persistence.NHibernate;

    public class DefaultToNHibernatePersistence : INeedInitialization
    {
        public void Init()
        {
            NHibernatePersistence.UseAsDefault();
        }
    }
}