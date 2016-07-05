namespace NServiceBus.SqlServer.UnitTests
{
    using NServiceBus.Transports.SQLServer;
    using NUnit.Framework;

    class TableBasedQueueTests
    {
        [Test]
        public void Table_name_and_schema_should_be_quoted()
        {
            Assert.AreEqual("[nsb].[MyEndpoint]", new TableBasedQueue("MyEndpoint", "nsb").ToString());
            Assert.AreEqual("[nsb].[MyEndoint]]; SOME OTHER SQL;--]", new TableBasedQueue("MyEndoint]; SOME OTHER SQL;--", "nsb").ToString());
        }
    }
}