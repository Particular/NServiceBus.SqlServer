namespace NServiceBus.Transport.SqlServer.UnitTests.Receiving;

using Sql.Shared;

static class ReceiveCursorTestExtensions
{
    public static void ReceivedRow(this ReceiveCursor cursor, long rowVersion) =>
        cursor.Received(ReceiveAnchor.HighestReceived(0), rowVersion);
}
