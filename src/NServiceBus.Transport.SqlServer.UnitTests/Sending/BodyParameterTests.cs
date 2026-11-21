namespace NServiceBus.Transport.SqlServer.UnitTests.Sending;

using System;
using Microsoft.Data.SqlClient;
using NServiceBus.Transport.Sql.Shared;
using NUnit.Framework;

[TestFixture]
public class BodyParameterTests
{
    [Test]
    public void Passes_a_large_slice_by_offset()
    {
        var parameter = AddBody(ConnectionString, SqlCommandColumnEncryptionSetting.UseConnectionSetting, 8001);

        Assert.Multiple(() =>
        {
            Assert.That(parameter.Value, Is.SameAs(backingArray));
            Assert.That(parameter.Offset, Is.EqualTo(Offset));
            Assert.That(parameter.Size, Is.EqualTo(8001));
        });
    }

    [Test]
    public void Passes_a_whole_array_without_copying()
    {
        var body = new byte[100];
        using var command = new SqlCommand { Connection = new SqlConnection(ConnectionString) };

        BodyParameter.Add(command, body);

        Assert.That(command.Parameters["Body"].Value, Is.SameAs(body));
    }

    [TestCase(ConnectionString, SqlCommandColumnEncryptionSetting.UseConnectionSetting, 8000)]
    [TestCase(ConnectionString + ";Column Encryption Setting=Enabled", SqlCommandColumnEncryptionSetting.UseConnectionSetting, 8001)]
    [TestCase(ConnectionString, SqlCommandColumnEncryptionSetting.Enabled, 8001)]
    public void Copies_the_slice(string connectionString, SqlCommandColumnEncryptionSetting commandSetting, int bodyLength)
    {
        var parameter = AddBody(connectionString, commandSetting, bodyLength);

        Assert.Multiple(() =>
        {
            Assert.That(parameter.Value, Is.Not.SameAs(backingArray));
            Assert.That(parameter.Offset, Is.Zero);
            Assert.That((byte[])parameter.Value, Is.EqualTo(backingArray.AsSpan(Offset, bodyLength).ToArray()).AsCollection);
        });
    }

    SqlParameter AddBody(string connectionString, SqlCommandColumnEncryptionSetting commandSetting, int bodyLength)
    {
        var command = new SqlCommand(null, new SqlConnection(connectionString), null, commandSetting);
        BodyParameter.Add(command, backingArray.AsMemory(Offset, bodyLength));
        return command.Parameters["Body"];
    }

    readonly byte[] backingArray = CreateBackingArray();

    static byte[] CreateBackingArray()
    {
        var array = new byte[10_000];
        Random.Shared.NextBytes(array);
        return array;
    }

    const int Offset = 13;
    const string ConnectionString = "Data Source=localhost;Initial Catalog=nservicebus;Integrated Security=True";
}
