using Guacamole.QueueProcessor.Configuration;
using Guacamole.QueueProcessor.Extensions;
using Guacamole.QueueProcessor.Services.Core;
using Guacamole.QueueProcessor.Tests.Fakes;
using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Guacamole.QueueProcessor.Tests;

public class ServiceCollectionExtensionsTests
{
    private sealed record TestMessage(string Data);

    [Test]
    public async Task AddAzureQueueProcessing_RegistersHostedService()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["QueueProcessing:StorageConnectionString"] = "UseDevelopmentStorage=true"
        });

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<QueueServiceClient>(_ => throw new InvalidOperationException("Test placeholder"));
        services.AddAzureQueueProcessing(config, builder =>
        {
            builder.AddProcessor<TestMessage, FakeQueueProcessor<TestMessage>>("test-queue");
        });

        // Check at the descriptor level — resolving IHostedService would instantiate
        // AzureQueueRuntimeFactory which validates the connection string eagerly.
        var hostedServiceCount = services.Count(d =>
            d.ServiceType == typeof(IHostedService) &&
            d.ImplementationFactory != null);

        await Assert.That(hostedServiceCount).IsEqualTo(1);
    }

    [Test]
    public async Task AddAzureQueueProcessing_RegistersOptionsMonitor()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            [$"{QueueProcessingOptions.SectionName}:StorageConnectionString"] = "AccountName=test;AccountKey=dGVzdA==;DefaultEndpointsProtocol=https;"
        });

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<QueueServiceClient>(_ => throw new InvalidOperationException("Test placeholder"));
        services.AddAzureQueueProcessing(config, builder =>
        {
            builder.AddProcessor<TestMessage, FakeQueueProcessor<TestMessage>>("queue1");
        });

        var sp = services.BuildServiceProvider();
        var optionsMonitor = sp.GetService<Microsoft.Extensions.Options.IOptionsMonitor<QueueProcessingOptions>>();

        await Assert.That(optionsMonitor).IsNotNull();
    }

    [Test]
    public async Task AddAzureQueueProcessing_AddBatchProcessor_RegistersCorrectly()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            ["QueueProcessing:StorageConnectionString"] = "UseDevelopmentStorage=true"
        });

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<QueueServiceClient>(_ => throw new InvalidOperationException("Test placeholder"));
        services.AddAzureQueueProcessing(config, builder =>
        {
            builder.AddBatchProcessor<TestMessage, FakeQueueBatchProcessor<TestMessage>>("batch-queue");
        });

        // Should not throw during registration
        var sp = services.BuildServiceProvider();

        await Assert.That(sp).IsNotNull();
    }

    [Test]
    public async Task AddRabbitMqQueueProcessing_CanRegisterWithoutThrowing()
    {
        var config = BuildConfig(new Dictionary<string, string?>
        {
            [$"{QueueProcessingOptions.SectionName}:RabbitMqUri"] = "amqp://guest:guest@localhost:5672"
        });

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRabbitMqQueueProcessing(config, builder =>
        {
            builder.AddProcessor<TestMessage, FakeQueueProcessor<TestMessage>>("test-queue");
        });

        var sp = services.BuildServiceProvider();

        await Assert.That(sp).IsNotNull();
    }

    [Test]
    public async Task AddAzureQueueProcessing_Throws_WhenQueueServiceClientMissing()
    {
        var config = BuildConfig(new Dictionary<string, string?>());

        var services = new ServiceCollection();
        services.AddLogging();

        var action = () => services.AddAzureQueueProcessing(config, builder =>
        {
            builder.AddProcessor<TestMessage, FakeQueueProcessor<TestMessage>>("test-queue");
        });

        await Assert.That(action).Throws<InvalidOperationException>();
    }

    private static IConfiguration BuildConfig(Dictionary<string, string?> values)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
}

