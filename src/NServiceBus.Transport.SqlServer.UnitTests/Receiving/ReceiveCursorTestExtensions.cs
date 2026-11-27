namespace NServiceBus.Transport.SqlServer.UnitTests.Receiving;

using Sql.Shared;

static class ReceiveCursorTestExtensions
{
    public static void ReceivedRow(this ReceiveCursor cursor, long rowVersion) =>
        cursor.Received(ReceiveAnchor.HighestReceived(0), rowVersion);

    public static ReceiveAnchor WithoutGeneration(this ReceiveAnchor anchor) => anchor with { Generation = 0 };
}
