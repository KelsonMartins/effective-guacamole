# Guacamole.QueueProcessor

A reusable, high-throughput queue processing framework for .NET 10 with strongly-typed processors, adaptive concurrency, poison handling, and provider-specific adapters.

## Features

- Strongly-typed handlers via `IQueueProcessor<TMessage>`
- Optional batch handlers via `IQueueBatchProcessor<TMessage>`
- Adaptive polling and worker auto-scaling
- Bounded channels for backpressure
- Poison routing and configurable retry behavior
- Hot-reload aware runtime configuration using `IOptionsMonitor`
- OpenTelemetry-friendly metrics via `System.Diagnostics.Metrics`
- Multiple providers:
    - Azure Storage Queues
    - Azure Service Bus queues
    - RabbitMQ

## Install

```bash
dotnet add package Guacamole.QueueProcessor
```

## Quick Start (Single Message Processor)

### 0. Configure Azure client (required for Azure provider)

`AddAzureQueueProcessing(...)` expects a registered `QueueServiceClient` in DI.

Using Aspire/Azure client wiring:

```csharp
builder.Services.AddAzureClients(clientBuilder =>
{
        clientBuilder.AddQueueServiceClient(builder.Configuration.GetConnectionString("Storage"));
});
```

Manual DI setup:

```csharp
using Azure.Storage.Queues;

builder.Services.AddSingleton(_ =>
        new QueueServiceClient(builder.Configuration.GetConnectionString("Storage")));
```

### 1. Define a message

```csharp
public sealed class OrderPlaced
{
        public required string OrderId { get; init; }
        public required decimal TotalAmount { get; init; }
}
```

### 2. Implement a processor

```csharp
using Guacamole.QueueProcessor;
using Guacamole.QueueProcessor.Abstract;

public sealed class OrderPlacedProcessor : IQueueProcessor<OrderPlaced>
{
        public Task<ProcessingResult> ProcessAsync(
                OrderPlaced message,
                ProcessingContext context,
                CancellationToken cancellationToken)
        {
                // Business logic
                return Task.FromResult(ProcessingResult.Successful());
        }
}
```

### 3. Register with DI

Choose one provider registration method:

```csharp
using Guacamole.QueueProcessor.Extensions;

// Azure Storage Queue
builder.Services.AddAzureQueueProcessing(builder.Configuration, qp =>
{
        qp.AddProcessor<OrderPlaced, OrderPlacedProcessor>("orders");
});

// Azure Service Bus Queue
builder.Services.AddServiceBusQueueProcessing(builder.Configuration, qp =>
{
        qp.AddProcessor<OrderPlaced, OrderPlacedProcessor>("orders");
});

// RabbitMQ
builder.Services.AddRabbitMqQueueProcessing(builder.Configuration, qp =>
{
        qp.AddProcessor<OrderPlaced, OrderPlacedProcessor>("orders");
});
```

## Batch Processing

When your workload is more efficient in bulk (for example, bulk writes), implement `IQueueBatchProcessor<TMessage>`:

```csharp
using Guacamole.QueueProcessor;
using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Models;

public sealed class InvoiceBatchProcessor : IQueueBatchProcessor<InvoiceMessage>
{
        public Task<BatchProcessingResult> ProcessBatchAsync(
                IReadOnlyList<BatchItem<InvoiceMessage>> batch,
                CancellationToken cancellationToken)
        {
                var results = batch.ToDictionary(
                        item => item.Context.MessageId,
                        _ => ProcessingResult.Successful());

                return Task.FromResult(new BatchProcessingResult { Results = results });
        }
}
```

Registration:

```csharp
builder.Services.AddAzureQueueProcessing(builder.Configuration, qp =>
{
        qp.AddBatchProcessor<InvoiceMessage, InvoiceBatchProcessor>("invoices");
});
```

## Configuration

All options are under `QueueProcessing`.

```json
{
    "QueueProcessing": {
        "ServiceBusConnectionString": "<service-bus-connection-string>",
        "RabbitMqUri": "amqp://guest:guest@localhost:5672",
        "Queues": [
            {
                "Name": "orders",
                "MinWorkers": 1,
                "MaxWorkers": 10,
                "BatchSize": 8,
                "BatchFlushTimeoutMs": 100,
                "ChannelCapacity": 1000,
                "VisibilityTimeoutSeconds": 60,
                "MaxDequeueCount": 5,
                "EnableAdaptiveScaling": true,
                "TargetLagSeconds": 300,
                "TargetCpuPercent": 70,
                "DeadLetterQueueName": "orders-poison",
                "RetryQueueName": "orders-retry",
                "RetryDelaySeconds": 60,
                "Retry": {
                    "MaxAttempts": 3,
                    "InitialDelay": "00:00:00.200",
                    "MaxDelay": "00:00:30",
                    "UseJitter": true,
                    "EnableDurableRetry": false,
                    "DurableRetryDelay": "00:01:00"
                }
            }
        ]
    }
}
```

### Provider-specific notes

- Azure Storage requires a registered `QueueServiceClient` in DI (Aspire or manual registration).
- Azure Service Bus uses `ServiceBusConnectionString`.
- RabbitMQ uses `RabbitMqUri`.
- Durable retry queue behavior is only active when a provider supplies a retry queue component.

## Processing Result Semantics

- Return `ProcessingResult.Successful()` to complete and delete/ack the message.
- Return `ProcessingResult.Failed(..., shouldRetry: true)` for retryable failures.
- Return `ProcessingResult.Failed(..., shouldRetry: false)` for non-retryable failures (dead-letter path).

## Runtime Behavior

- One runtime instance per configured queue.
- Receiver writes to a bounded channel for backpressure.
- Worker pool consumes concurrently.
- Auto-scaler evaluates queue depth every 30 seconds.
- Hosted service listens for config changes and restarts queue runtime when significant options change.

## Metrics

The runtime emits metrics from meter `QueueProcessor`:

- `queue.messages.processed`
- `queue.messages.failed`
- `queue.processing.duration`
- `queue.depth`
- `queue.workers.active`
- `queue.lag`

Each metric is tagged with `queue.name`.

## Common Registration Errors

- Queue configured in `QueueProcessing:Queues` but no processor registered for that queue.
- Missing `QueueServiceClient` registration when using `AddAzureQueueProcessing(...)`.
- Missing provider connection value (`ServiceBusConnectionString` or `RabbitMqUri`).

## Target Framework

- `net10.0`
