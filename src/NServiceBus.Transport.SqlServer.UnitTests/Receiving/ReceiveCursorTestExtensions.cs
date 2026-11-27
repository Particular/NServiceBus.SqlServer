namespace NServiceBus.Transport.SqlServer.UnitTests.Receiving;

using Sql.Shared;

static class ReceiveCursorTestExtensions
{
    // A receive from the highest received row version: no sweep has been requested at generation 0
    public static void ReceivedRow(this ReceiveCursor cursor, long rowVersion) =>
        cursor.Received(new ReceiveAnchor(0, Generation: 0), rowVersion);
}
