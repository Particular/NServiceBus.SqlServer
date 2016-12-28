namespace NServiceBus.SqlServer.UnitTests
{
    using NUnit.Framework;
    using Transport.SQLServer;

    class TableBasedQueueTests
    {
        [Test]
        public void Table_name_and_schema_should_be_quoted()
        {
            Assert.AreEqual("[nsb].[MyEndpoint]", new TableBasedQueue(new QueueAddress(null, "nsb", "MyEndpoint")).ToString());
            Assert.AreEqual("[nsb].[MyEndoint]]; SOME OTHER SQL;--]", new TableBasedQueue(new QueueAddress(null, "nsb", "MyEndoint]; SOME OTHER SQL;--")).ToString());
        }
    }
}