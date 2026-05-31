using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Configuration;
using Guacamole.QueueProcessor.Runtime;
using Guacamole.QueueProcessor.Services.Azure;
using Guacamole.QueueProcessor.Services.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Guacamole.QueueProcessor.Extensions;

/// <summary>
/// Extension methods for adding Azure Queue Processing to the service collection.
/// </summary>
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Adds Azure Storage Queue processing to the application.
        /// </summary>
        /// <param name="configuration">Configuration containing QueueProcessing section</param>
        /// <param name="configure">Optional configuration builder action</param>
        public IServiceCollection AddAzureQueueProcessing(IConfiguration configuration, Action<QueueProcessingBuilder>? configure = null)
        {
            // Bind configuration
            var options = new QueueProcessingOptions();
            configuration.GetSection(QueueProcessingOptions.SectionName).Bind(options);
            services.AddSingleton(options);

            // Register processor registry
            var registry = new ProcessorRegistry();
            services.AddSingleton(registry);

            // Configure processors
            var builder = new QueueProcessingBuilder(services, registry);
            configure?.Invoke(builder);

            // Register Azure-specific factory
            services.AddSingleton<IQueueRuntimeFactory, AzureQueueRuntimeFactory>();

            // Create and register queue runtimes
            foreach (var queueConfig in options.Queues)
            {
                // Validate registration
                if (!registry.HasRegistration(queueConfig.Name))
                    throw new InvalidOperationException($"Queue '{queueConfig.Name}' is configured but has no registered processor");

                // Register runtime as singleton
                services.AddSingleton(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<QueueRuntime>>();
                    return new QueueRuntime(queueConfig, sp, registry, logger);
                });

                // Register hosted service for this queue
                services.AddSingleton<IHostedService>(sp =>
                {
                    var runtimes = sp.GetServices<QueueRuntime>();
                    var runtime = runtimes.First(r => r.QueueName == queueConfig.Name);
                    var factory = sp.GetRequiredService<IQueueRuntimeFactory>();
                    var logger = sp.GetRequiredService<ILogger<QueueRuntimeHostedService>>();

                    return new QueueRuntimeHostedService(runtime, factory, logger);
                });
            }

            return services;
        }

        /// <summary>
        /// Adds Azure Storage Queue processing with inline configuration.
        /// </summary>
        public IServiceCollection AddAzureQueueProcessing(Action<QueueProcessingOptions> configureOptions, Action<QueueProcessingBuilder> configureBuilder)
        {
            // Configure options
            var options = new QueueProcessingOptions();
            configureOptions(options);
            services.AddSingleton(options);

            // Register processor registry
            var registry = new ProcessorRegistry();
            services.AddSingleton(registry);

            // Configure processors
            var builder = new QueueProcessingBuilder(services, registry);
            configureBuilder(builder);

            // Register Azure-specific factory
            services.AddSingleton<IQueueRuntimeFactory, AzureQueueRuntimeFactory>();

            // Create and register queue runtimes
            foreach (var queueConfig in options.Queues)
            {
                // Validate registration
                if (!registry.HasRegistration(queueConfig.Name))
                    throw new InvalidOperationException($"Queue '{queueConfig.Name}' is configured but has no registered processor");

                // Register runtime as singleton (need unique key per queue)
                var queueName = queueConfig.Name;
                services.AddKeyedSingleton<QueueRuntime>(queueName, (sp, key) =>
                {
                    var logger = sp.GetRequiredService<ILogger<QueueRuntime>>();
                    return new QueueRuntime(queueConfig, sp, registry, logger);
                });

                // Register hosted service for this queue
                services.AddSingleton<IHostedService>(sp =>
                {
                    var runtime = sp.GetRequiredKeyedService<QueueRuntime>(queueName);
                    var factory = sp.GetRequiredService<IQueueRuntimeFactory>();
                    var logger = sp.GetRequiredService<ILogger<QueueRuntimeHostedService>>();

                    return new QueueRuntimeHostedService(runtime, factory, logger);
                });
            }

            return services;
        }
    }
}