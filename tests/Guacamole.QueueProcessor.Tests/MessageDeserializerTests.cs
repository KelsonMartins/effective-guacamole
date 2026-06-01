using System.Text;
using System.Text.Json;
using Guacamole.QueueProcessor.Services.Core;

namespace Guacamole.QueueProcessor.Tests;

public class MessageDeserializerTests
{
    private sealed record SampleMessage(string Name, int Value);

    [Test]
    public async Task Deserialize_Generic_ReturnsTypedObject()
    {
        var payload = Serialize(new SampleMessage("hello", 42));

        var result = MessageDeserializer.Deserialize<SampleMessage>(payload);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Name).IsEqualTo("hello");
        await Assert.That(result.Value).IsEqualTo(42);
    }

    [Test]
    public async Task Deserialize_NonGeneric_ReturnsObject()
    {
        var deserializer = new MessageDeserializer();
        var payload = Serialize(new SampleMessage("world", 7));

        var result = deserializer.Deserialize(payload, typeof(SampleMessage));

        await Assert.That(result).IsNotNull();
        await Assert.That(result).IsAssignableTo<SampleMessage>();
        var typed = (SampleMessage)result!;
        await Assert.That(typed.Name).IsEqualTo("world");
    }

    [Test]
    public async Task Deserialize_EmptyPayload_ReturnsNull()
    {
        var result = MessageDeserializer.Deserialize<SampleMessage>(ReadOnlyMemory<byte>.Empty);

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Deserialize_NonGeneric_EmptyPayload_ReturnsNull()
    {
        var deserializer = new MessageDeserializer();

        var result = deserializer.Deserialize(ReadOnlyMemory<byte>.Empty, typeof(SampleMessage));

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task Deserialize_CamelCaseJson_MapsCorrectly()
    {
        // camelCase property names should match PascalCase .NET properties
        var json = """{"name":"camel","value":99}""";
        var payload = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(json));

        var result = MessageDeserializer.Deserialize<SampleMessage>(payload);

        await Assert.That(result!.Name).IsEqualTo("camel");
        await Assert.That(result.Value).IsEqualTo(99);
    }

    [Test]
    public async Task Deserialize_Base64EncodedJson_MapsCorrectly()
    {
        var json = """{"name":"base64","value":123}""";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
        var payload = new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(encoded));

        var result = MessageDeserializer.Deserialize<SampleMessage>(payload);

        await Assert.That(result).IsNotNull();
        await Assert.That(result!.Name).IsEqualTo("base64");
        await Assert.That(result.Value).IsEqualTo(123);
    }

    private static ReadOnlyMemory<byte> Serialize<T>(T value)
        => new(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value)));
}

