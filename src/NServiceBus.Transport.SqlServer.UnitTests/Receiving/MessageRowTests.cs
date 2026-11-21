namespace NServiceBus.Transport.SqlServer.UnitTests.Receiving;

using System;
using System.Buffers;
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

    [Test]
    public void Clears_what_a_failed_read_wrote_before_returning_the_buffer()
    {
        Assert.ThrowsAsync<IOException>(() => MessageRow.ReadToEnd(new StreamThatFailsAfterWriting()));

        // The shared pool hands the most recently returned array back to the same thread.
        var rented = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            Assert.That(rented.AsSpan().IndexOfAnyExcept((byte)0), Is.EqualTo(-1));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    sealed class StreamThatFailsAfterWriting : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            buffer.Span.Fill(0xFF);
            throw new IOException("Connection lost");
        }
    }

    sealed class ChunkedStream(byte[] data, int maxBytesPerRead) : MemoryStream(data, writable: false)
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(buffer.Length, maxBytesPerRead)], cancellationToken);
    }
}
