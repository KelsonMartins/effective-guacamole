using Guacamole.QueueProcessor.Services.Core;

namespace Guacamole.QueueProcessor.Tests;

public class ProcessorRegistryTests
{
    [Test]
    public async Task Register_AddsEntry_GetRegistration_ReturnsIt()
    {
        var registry = new ProcessorRegistry();
        registry.Register("orders", typeof(string), typeof(object));

        var reg = registry.GetRegistration("orders");

        await Assert.That(reg).IsNotNull();
        await Assert.That(reg!.QueueName).IsEqualTo("orders");
        await Assert.That(reg.MessageType).IsEqualTo(typeof(string));
        await Assert.That(reg.ProcessorType).IsEqualTo(typeof(object));
        await Assert.That(reg.IsBatchProcessor).IsFalse();
    }

    [Test]
    public async Task Register_BatchProcessor_SetsFlagCorrectly()
    {
        var registry = new ProcessorRegistry();
        registry.Register("orders", typeof(string), typeof(object), isBatchProcessor: true);

        var reg = registry.GetRegistration("orders");

        await Assert.That(reg!.IsBatchProcessor).IsTrue();
    }

    [Test]
    public async Task Register_DuplicateQueue_ThrowsInvalidOperationException()
    {
        var registry = new ProcessorRegistry();
        registry.Register("orders", typeof(string), typeof(object));

        await Assert.That(() => registry.Register("orders", typeof(int), typeof(object)))
            .ThrowsException()
            .WithMessageContaining("orders");
    }

    [Test]
    public async Task GetRegistration_UnknownQueue_ReturnsNull()
    {
        var registry = new ProcessorRegistry();

        var result = registry.GetRegistration("unknown");

        await Assert.That(result).IsNull();
    }

    [Test]
    public async Task HasRegistration_KnownQueue_ReturnsTrue()
    {
        var registry = new ProcessorRegistry();
        registry.Register("my-queue", typeof(string), typeof(object));

        await Assert.That(registry.HasRegistration("my-queue")).IsTrue();
    }

    [Test]
    public async Task HasRegistration_UnknownQueue_ReturnsFalse()
    {
        var registry = new ProcessorRegistry();

        await Assert.That(registry.HasRegistration("unknown")).IsFalse();
    }

    [Test]
    public async Task GetRegistration_IsCaseInsensitive()
    {
        var registry = new ProcessorRegistry();
        registry.Register("Orders", typeof(string), typeof(object));

        await Assert.That(registry.GetRegistration("orders")).IsNotNull();
        await Assert.That(registry.GetRegistration("ORDERS")).IsNotNull();
    }
}

