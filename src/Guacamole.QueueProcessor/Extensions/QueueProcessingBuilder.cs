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
    /// Registers a single-message processor for a specific queue.
    /// </summary>
    /// <typeparam name="TMessage">The message type</typeparam>
    /// <typeparam name="TProcessor">The processor implementation</typeparam>
    /// <param name="queueName">The queue name</param>
    public QueueProcessingBuilder AddProcessor<TMessage, TProcessor>(string queueName)
        where TMessage : class
        where TProcessor : class, IQueueProcessor<TMessage>
    {
        _services.AddScoped<TProcessor>();
        _registry.Register(queueName, typeof(TMessage), typeof(TProcessor), isBatchProcessor: false);
        return this;
    }

    /// <summary>
    /// Registers a single-message processor with a factory function.
    /// </summary>
    public QueueProcessingBuilder AddProcessor<TMessage, TProcessor>(string queueName, Func<IServiceProvider, TProcessor> implementationFactory)
        where TMessage : class
        where TProcessor : class, IQueueProcessor<TMessage>
    {
        _services.AddScoped<TProcessor>(implementationFactory);
        _registry.Register(queueName, typeof(TMessage), typeof(TProcessor), isBatchProcessor: false);
        return this;
    }

    /// <summary>
    /// Registers a batch processor for a specific queue.
    /// The processor receives a collection of messages and returns per-message results,
    /// enabling efficient bulk operations (e.g. bulk database inserts).
    /// </summary>
    /// <typeparam name="TMessage">The message type</typeparam>
    /// <typeparam name="TBatchProcessor">The batch processor implementation</typeparam>
    /// <param name="queueName">The queue name</param>
    public QueueProcessingBuilder AddBatchProcessor<TMessage, TBatchProcessor>(string queueName)
        where TMessage : class
        where TBatchProcessor : class, IQueueBatchProcessor<TMessage>
    {
        _services.AddScoped<TBatchProcessor>();
        _registry.Register(queueName, typeof(TMessage), typeof(TBatchProcessor), isBatchProcessor: true);
        return this;
    }

    /// <summary>
    /// Registers a batch processor with a factory function.
    /// </summary>
    public QueueProcessingBuilder AddBatchProcessor<TMessage, TBatchProcessor>(string queueName, Func<IServiceProvider, TBatchProcessor> implementationFactory)
        where TMessage : class
        where TBatchProcessor : class, IQueueBatchProcessor<TMessage>
    {
        _services.AddScoped<TBatchProcessor>(implementationFactory);
        _registry.Register(queueName, typeof(TMessage), typeof(TBatchProcessor), isBatchProcessor: true);
        return this;
    }
}
