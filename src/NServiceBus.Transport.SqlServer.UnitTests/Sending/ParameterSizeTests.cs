namespace NServiceBus.Transport.SqlServer.UnitTests.Sending;

using System;
using System.Collections.Generic;
using System.Data.Common;
using Microsoft.Data.SqlClient;
using NServiceBus.Transport.Sql.Shared;
using NUnit.Framework;

public class ParameterSizeTests
{
    [TestCase(1)]
    [TestCase(177)]
    [TestCase(9000)]
    public void Delayed_message_body_and_headers_should_be_sized_as_max_regardless_of_length(int bodyLength)
    {
        var row = StoreDelayedMessageCommand.From([], new byte[bodyLength], TimeSpan.FromSeconds(5), "destination");
        var command = new SqlCommand();

        row.PrepareSendCommand(command);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(command.Parameters["Headers"].Size, Is.EqualTo(-1));
            Assert.That(command.Parameters["Body"].Size, Is.EqualTo(-1));
        }
    }

    [Test]
    public void Topic_parameters_should_have_a_fixed_size_regardless_of_topic_length()
    {
        var command = new SqlCommand();

        SubscriptionTable.AddTopicParameters(command, ["A.B", "Namespace.Events.SomeEvent"]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(command.Parameters["Topic_0"].Size, Is.EqualTo(4000));
            Assert.That(command.Parameters["Topic_1"].Size, Is.EqualTo(4000));
        }
    }

    [Test]
    public void Topic_parameters_should_bind_a_null_topic_as_null()
    {
        var command = new SqlCommand();

        SubscriptionTable.AddTopicParameters(command, [null]);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(command.Parameters["Topic_0"].Value, Is.EqualTo(DBNull.Value));
            Assert.That(command.Parameters["Topic_0"].Size, Is.EqualTo(4000));
        }
    }
}
