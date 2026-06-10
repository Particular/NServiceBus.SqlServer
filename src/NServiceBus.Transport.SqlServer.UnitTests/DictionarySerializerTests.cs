namespace NServiceBus.Transport.SqlServer.UnitTests;

using System.Collections.Generic;
using NServiceBus.Transport.Sql.Shared;
using NUnit.Framework;
using Particular.Approvals;

[TestFixture]
public class DictionarySerializerTests
{
    [Test]
    public void Can_round_trip()
    {
        var before = new Dictionary<string, string>
        {
            {"key1", "value1"},
            {"key2", "value2"},
            {"keyWithNull", null},
            {"keyWithEmpty", ""}
        };

        var serialized = DictionarySerializer.Serialize(before);
        Approver.Verify(serialized);

        var after = DictionarySerializer.DeSerialize(serialized);

        AssertDictionariesAreTheSame(before, after);
    }

    [Test]
    public void Can_round_trip_empty_dictionary()
    {
        var before = new Dictionary<string, string>();

        var serialized = DictionarySerializer.Serialize(before);
        Approver.Verify(serialized);

        var after = DictionarySerializer.DeSerialize(serialized);

        AssertDictionariesAreTheSame(before, after);
    }

    [Test]
    public void Can_round_trip_special_characters()
    {
        var before = new Dictionary<string, string>
        {
            {"key with spaces", "value with spaces"},
            {"key-with-dashes", "value-with-dashes"},
            {"key_with_underscores", "value_with_underscores"},
            {"key.with.dots", "value.with.dots"},
            {"key/with/slashes", "value/with/slashes"},
            {"key\\with\\backslashes", "value\\with\\backslashes"},
            {"key\"with\"quotes", "value\"with\"quotes"},
            {"key'with'apostrophes", "value'with'apostrophes"},
            {"key:with:colons", "value:with:colons"},
            {"key;with;semicolons", "value;with;semicolons"},
            {"key,with,commas", "value,with,commas"},
            {"key=with=equals", "value=with=equals"},
            {"key?with?question", "value?with?question"},
            {"key&with&ampersand", "value&with&ampersand"},
            {"key<with>angle", "value<with>angle"},
            {"key{with}braces", "value{with}braces"},
            {"key[with]brackets", "value[with]brackets"},
            {"key(with)parens", "value(with)parens"}
        };

        var serialized = DictionarySerializer.Serialize(before);
        Approver.Verify(serialized);

        var after = DictionarySerializer.DeSerialize(serialized);

        AssertDictionariesAreTheSame(before, after);
    }

    [Test]
    public void Can_round_trip_unicode()
    {
        var before = new Dictionary<string, string>
        {
            {"ümlaut", "äöü"},
            {"emoji", "🚀"},
            {"japanese", "こんにちは"},
            {"arabic", "مرحبا"},
            {"newline", "line1\r\nline2"},
            {"tab", "value\twith\ttabs"}
        };

        var serialized = DictionarySerializer.Serialize(before);
        Approver.Verify(serialized);

        var after = DictionarySerializer.DeSerialize(serialized);

        AssertDictionariesAreTheSame(before, after);
    }

    [Test]
    public void Can_deserialize_empty_json_object()
    {
        var after = DictionarySerializer.DeSerialize("{}");

        Assert.That(after, Is.Empty);
    }

    [Test]
    public void Can_deserialize_null_values()
    {
        var after = DictionarySerializer.DeSerialize("""{"keyWithNull":null}""");

        Assert.That(after, Has.Count.EqualTo(1));
        Assert.That(after["keyWithNull"], Is.Null);
    }

    [Test]
    public void Can_deserialize_empty_values()
    {
        var after = DictionarySerializer.DeSerialize("""{"keyWithEmpty":""}""");

        Assert.That(after, Has.Count.EqualTo(1));
        Assert.That(after["keyWithEmpty"], Is.Empty);
    }

    [Test]
    public void Deserializing_json_null_returns_null()
    {
        var after = DictionarySerializer.DeSerialize("null");

        Assert.That(after, Is.Null);
    }

    [Test]
    public void Serialized_format_is_compact()
    {
        var before = new Dictionary<string, string>
        {
            {"key1", "value1"},
            {"key2", "value2"}
        };

        var serialized = DictionarySerializer.Serialize(before);

        Assert.That(serialized, Does.Not.Contain("\r"));
        Assert.That(serialized, Does.Not.Contain("\n"));
        Assert.That(serialized, Does.Not.Contain("  "));
    }

    static void AssertDictionariesAreTheSame(Dictionary<string, string> before, Dictionary<string, string> after)
    {
        foreach (var beforeItem in before)
        {
            Assert.That(after[beforeItem.Key], Is.EqualTo(beforeItem.Value));
        }

        Assert.That(after, Has.Count.EqualTo(before.Count));
    }
}