namespace NServiceBus.Transport.PostgreSql.UnitTests;

using System;
using NUnit.Framework;

public class FailureInfoCacheOptionsTests
{
    [Test]
    public void Should_default_to_10000_entries_without_expiration()
    {
        var options = new PostgreSqlTransport("Server=.;").FailureInfoCache;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(options.MaxEntries, Is.EqualTo(10000));
            Assert.That(options.ExpirationPeriod, Is.Null);
        }
    }

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(2)]
    public void Should_reject_max_entries_below_the_minimum(int size)
    {
        var options = new PostgreSqlTransport("Server=.;").FailureInfoCache;

        Assert.Throws<ArgumentOutOfRangeException>(() => options.MaxEntries = size);
    }

    [Test]
    public void Should_accept_the_minimum_max_entries()
    {
        var options = new PostgreSqlTransport("Server=.;").FailureInfoCache;

        options.MaxEntries = 3;

        Assert.That(options.MaxEntries, Is.EqualTo(3));
    }

    [Test]
    public void Should_reject_non_positive_expiration_period()
    {
        var options = new PostgreSqlTransport("Server=.;").FailureInfoCache;

        using (Assert.EnterMultipleScope())
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => options.ExpirationPeriod = TimeSpan.Zero);
            Assert.Throws<ArgumentOutOfRangeException>(() => options.ExpirationPeriod = TimeSpan.FromSeconds(-1));
        }
    }

    [Test]
    public void Should_allow_clearing_the_expiration_period()
    {
        var options = new PostgreSqlTransport("Server=.;").FailureInfoCache;
        options.ExpirationPeriod = TimeSpan.FromMinutes(5);

        options.ExpirationPeriod = null;

        Assert.That(options.ExpirationPeriod, Is.Null);
    }
}
