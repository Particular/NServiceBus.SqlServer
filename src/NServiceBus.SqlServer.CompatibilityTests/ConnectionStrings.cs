namespace NServiceBus.SqlServer.CompatibilityTests
{
    public static class ConnectionStrings
    {
        public static string Default = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus;Integrated Security=True;";
        public static string Instance1 = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus1;Integrated Security=True;";
        public static string Instance2 = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus2;Integrated Security=True;";

        public static string Instance1_Src = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus1;Integrated Security=True;Queue Schema=src";
        public static string Instance1_Dest = @"Data Source=.\SQLEXPRESS;Initial Catalog=nservicebus1;Integrated Security=True;Queue Schema=dest";
    }
}