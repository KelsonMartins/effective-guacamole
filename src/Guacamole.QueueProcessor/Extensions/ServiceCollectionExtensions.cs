using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Configuration;
using Guacamole.QueueProcessor.Runtime;
using Guacamole.QueueProcessor.Services.Azure;
using Guacamole.QueueProcessor.Services.Core;
using Azure.Storage.Queues;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Guacamole.QueueProcessor.Extensions;

/// <summary>
/// Extension methods for adding queue processing to the service collection.
/// </summary>
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds Azure Storage Queue processing to the application.
        /// Uses <see cref="IOptionsMonitor{QueueProcessingOptions}"/> for hot-reload support.
        /// </summary>
        /// <param name="configuration">Configuration containing QueueProcessing section</param>
        /// <param name="configure">Optional queue processor registrations</param>
        public IServiceCollection AddAzureQueueProcessing(IConfiguration configuration, Action<QueueProcessingBuilder>? configure = null)
        {
            EnsureQueueServiceClientRegistered(services);

            // Bind via Configure so IOptionsMonitor picks up hot-reload changes
            services.Configure<QueueProcessingOptions>(configuration.GetSection(QueueProcessingOptions.SectionName));

            // Register processor registry
            var registry = new ProcessorRegistry();
            services.AddSingleton(registry);

            // Validate initial configuration and register queues
            var snapshot = configuration
                .GetSection(QueueProcessingOptions.SectionName)
                .Get<QueueProcessingOptions>() ?? new QueueProcessingOptions();

            // Allow callers to register processors
            var builder = new QueueProcessingBuilder(services, registry);
            configure?.Invoke(builder);

            // Register Azure-specific factory
            services.AddSingleton<IQueueRuntimeFactory, AzureQueueRuntimeFactory>();

            // Use registry queue names (registered via builder) merged with any from config
            var configQueues = snapshot.Queues.Select(q => q.Name);
            var allQueues = registry.GetQueueNames().Union(configQueues, StringComparer.OrdinalIgnoreCase);
            RegisterQueueRuntimes(services, registry, allQueues);

            return services;
        }

        /// <summary>
        /// Adds Azure Storage Queue processing with inline configuration.
        /// </summary>
        public IServiceCollection AddAzureQueueProcessing(Action<QueueProcessingOptions> configureOptions, Action<QueueProcessingBuilder> configureBuilder)
        {
            EnsureQueueServiceClientRegistered(services);

            services.Configure(configureOptions);

            var snapshot = new QueueProcessingOptions();
            configureOptions(snapshot);

            var registry = new ProcessorRegistry();
            services.AddSingleton(registry);

            var builder = new QueueProcessingBuilder(services, registry);
            configureBuilder(builder);

            services.AddSingleton<IQueueRuntimeFactory, AzureQueueRuntimeFactory>();

            RegisterQueueRuntimes(services, registry, registry.GetQueueNames());

            return services;
        }

        /// <summary>
        /// Adds Azure Service Bus queue processing to the application.
        /// </summary>
        public IServiceCollection AddServiceBusQueueProcessing(IConfiguration configuration, Action<QueueProcessingBuilder>? configure = null)
        {
            services.Configure<QueueProcessingOptions>(configuration.GetSection(QueueProcessingOptions.SectionName));

            var registry = new ProcessorRegistry();
            services.AddSingleton(registry);

            var snapshot = configuration
                .GetSection(QueueProcessingOptions.SectionName)
                .Get<QueueProcessingOptions>() ?? new QueueProcessingOptions();

            var builder = new QueueProcessingBuilder(services, registry);
            configure?.Invoke(builder);

            services.AddSingleton<IQueueRuntimeFactory, Services.ServiceBus.ServiceBusQueueRuntimeFactory>();

            var sbConfigQueues = snapshot.Queues.Select(q => q.Name);
            var sbAllQueues = registry.GetQueueNames().Union(sbConfigQueues, StringComparer.OrdinalIgnoreCase);
            RegisterQueueRuntimes(services, registry, sbAllQueues);

            return services;
        }

        /// <summary>
        /// Adds RabbitMQ queue processing to the application.
        /// </summary>
        public IServiceCollection AddRabbitMqQueueProcessing(IConfiguration configuration, Action<QueueProcessingBuilder>? configure = null)
        {
            services.Configure<QueueProcessingOptions>(configuration.GetSection(QueueProcessingOptions.SectionName));

            var registry = new ProcessorRegistry();
            services.AddSingleton(registry);

            var snapshot = configuration
                .GetSection(QueueProcessingOptions.SectionName)
                .Get<QueueProcessingOptions>() ?? new QueueProcessingOptions();

            var builder = new QueueProcessingBuilder(services, registry);
            configure?.Invoke(builder);

            services.AddSingleton<IQueueRuntimeFactory, Services.RabbitMq.RabbitMqQueueRuntimeFactory>();

            var rmqConfigQueues = snapshot.Queues.Select(q => q.Name);
            var rmqAllQueues = registry.GetQueueNames().Union(rmqConfigQueues, StringComparer.OrdinalIgnoreCase);
            RegisterQueueRuntimes(services, registry, rmqAllQueues);

            return services;
        }

        private static void RegisterQueueRuntimes(IServiceCollection sc, ProcessorRegistry registry, IEnumerable<string> queueNames)
        {
            foreach (var queueName in queueNames)
            {
                if (!registry.HasRegistration(queueName))
                    throw new InvalidOperationException($"Queue '{queueName}' is configured but has no registered processor.");

                var name = queueName; // capture for closure

                sc.AddSingleton(sp => new QueueRuntime(
                    name,
                    sp.GetRequiredService<IOptionsMonitor<QueueProcessingOptions>>(),
                    sp,
                    sp.GetRequiredService<ProcessorRegistry>(),
                    sp.GetRequiredService<ILogger<QueueRuntime>>()));

                sc.AddSingleton<IHostedService>(sp =>
                {
                    // Find the runtime for this queue
                    var runtime = sp.GetServices<QueueRuntime>().First(r => r.QueueName == name);
                    return new QueueRuntimeHostedService(
                        runtime,
                        sp.GetRequiredService<IQueueRuntimeFactory>(),
                        sp.GetRequiredService<IOptionsMonitor<QueueProcessingOptions>>(),
                        sp.GetRequiredService<ILogger<QueueRuntimeHostedService>>());
                });
            }
        }

        private static void EnsureQueueServiceClientRegistered(IServiceCollection serviceCollection)
        {
            var registered = serviceCollection.Any(sd => sd.ServiceType == typeof(QueueServiceClient));

            if (!registered)
            {
                throw new InvalidOperationException(
                    "QueueServiceClient is not registered. Register it before calling AddAzureQueueProcessing (for example, via Aspire service defaults/Azure client wiring or manual DI registration).");
            }
        }
    }
}

