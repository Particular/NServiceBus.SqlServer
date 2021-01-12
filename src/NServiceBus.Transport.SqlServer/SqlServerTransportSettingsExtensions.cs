namespace NServiceBus
{
    using System;
#if SYSTEMDATASQLCLIENT
    using System.Data.SqlClient;
#else
    using Microsoft.Data.SqlClient;
#endif
    using System.Threading.Tasks;
    using System.Transactions;
    using Configuration.AdvancedExtensibility;
    using Logging;
    using Transport.SqlServer;

   
}
