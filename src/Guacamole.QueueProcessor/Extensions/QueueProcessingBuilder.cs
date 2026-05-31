using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Services.Core;
using Microsoft.Extensions.DependencyInjection;

namespace Guacamole.QueueProcessor.Extensions;

/// <summary>
/// Builder for configuring queue processing.
/// </summary>
public sealed class QueueProcessingBuilder(IServiceCollection services, ProcessorRegistry registry)
{
    private readonly IServiceCollection _services = services;
    private readonly ProcessorRegistry _registry = registry;

    /// <summary>
    /// Registers a message processor for a specific queue.
    /// </summary>
    /// <typeparam name="TMessage">The message type</typeparam>
    /// <typeparam name="TProcessor">The processor implementation</typeparam>
    /// <param name="queueName">The queue name</param>
    public QueueProcessingBuilder AddProcessor<TMessage, TProcessor>(string queueName)
        where TMessage : class
        where TProcessor : class, IQueueProcessor<TMessage>
    {
        // Register processor in DI
        _services.AddScoped<TProcessor>();

        // Register in processor registry
        _registry.Register(queueName, typeof(TMessage), typeof(TProcessor));

        return this;
    }

    /// <summary>
    /// Registers a message processor with a factory function.
    /// </summary>
    public QueueProcessingBuilder AddProcessor<TMessage, TProcessor>(string queueName, Func<IServiceProvider, TProcessor> implementationFactory)
        where TMessage : class
        where TProcessor : class, IQueueProcessor<TMessage>
    {
        // Register processor in DI with factory
        _services.AddScoped<TProcessor>(implementationFactory);

        // Register in processor registry
        _registry.Register(queueName, typeof(TMessage), typeof(TProcessor));

        return this;
    }
}