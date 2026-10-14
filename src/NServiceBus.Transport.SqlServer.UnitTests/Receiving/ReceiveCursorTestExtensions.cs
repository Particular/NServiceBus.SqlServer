namespace NServiceBus.Transport.SqlServer.UnitTests.Receiving;

using Sql.Shared;

static class ReceiveCursorTestExtensions
{
    public static void ReceivedFromHighWater(this ReceiveCursor cursor, long rowVersion) =>
        cursor.Received(ReceiveAnchor.HighWater(0), rowVersion);
}
