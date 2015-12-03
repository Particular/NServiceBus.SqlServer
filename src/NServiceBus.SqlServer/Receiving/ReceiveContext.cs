namespace NServiceBus.Transports.SQLServer
{
    using System.Data.SqlClient;

    class ReceiveContext
    {
        public ReceiveType Type { get; set; } 

        public SqlConnection Connection { get; set; }

        public SqlTransaction Transaction { get; set; }
    }

    enum ReceiveType
    {
        NoTransaction,
        NativeTransaction,
        TransactionScope
    }
}