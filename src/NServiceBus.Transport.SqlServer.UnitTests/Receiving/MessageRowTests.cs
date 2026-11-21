namespace NServiceBus.Transport.SqlServer.UnitTests.Receiving;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus.Transport.Sql.Shared;
using NUnit.Framework;

[TestFixture]
public class MessageRowTests
{
    [TestCase(0, 1)]
    [TestCase(1, 1)]
    [TestCase(65_536, 65_536)]
    [TestCase(65_537, 8000)]
    [TestCase(1_048_576, 8000)]
    [TestCase(1_048_593, 8000)]
    [TestCase(3_145_733, 8000)]
    [TestCase(3_145_733, int.MaxValue)]
    public async Task Reads_the_entire_body_regardless_of_how_the_stream_chunks_it(int bodyLength, int maxBytesPerRead)
    {
        var body = new byte[bodyLength];
        Random.Shared.NextBytes(body);
        await using var stream = new ChunkedStream(body, maxBytesPerRead);

        var result = await MessageRow.ReadToEnd(stream);

        Assert.That(result, Is.EqualTo(body).AsCollection);
    }

    sealed class ChunkedStream(byte[] data, int maxBytesPerRead) : MemoryStream(data, writable: false)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(buffer.Length, maxBytesPerRead)], cancellationToken);
    }
}
