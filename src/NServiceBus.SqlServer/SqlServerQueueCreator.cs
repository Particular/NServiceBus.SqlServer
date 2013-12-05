namespace NServiceBus.Transports.SQLServer
{
    using System.Data;
    using System.Data.SqlClient;

    public class SqlServerQueueCreator : ICreateQueues
    {
        public void CreateQueueIfNecessary(Address address, string account)
        {
            //No need to do this now, should be already done. Maybe add validation here?
        }
    }
}