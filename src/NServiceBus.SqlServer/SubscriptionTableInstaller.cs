namespace NServiceBus.Transport.SQLServer
{
    using System.Threading.Tasks;
    using Installation;
    using Settings;

    /// <summary>
    /// Subscription table cannot be created via ICreateQueues because it needs to be created also for send-only endpoints. Installers are registered
    /// in the container via a convention so the only way to pass objects to them is via settings or via container. The transport infrastructure
    /// cannot access the container so we're doing it via settings (HACK).
    /// </summary>
    class SubscriptionTableInstaller : INeedToInstallSomething
    {
        SubscriptionTableCreator creator;

        public SubscriptionTableInstaller(ReadOnlySettings settings)
        {
            creator = settings.GetOrDefault<SubscriptionTableCreator>();
        }

        public Task Install(string identity)
        {
            return creator?.CreateIfNecessary() ?? TaskEx.CompletedTask;
        }
    }
}