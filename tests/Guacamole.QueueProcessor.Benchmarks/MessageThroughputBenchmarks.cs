using System.Text.Json;
using System.Threading.Channels;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using Guacamole.QueueProcessor.Abstract;
using Guacamole.QueueProcessor.Models;

namespace Guacamole.QueueProcessor.Benchmarks;

/// <summary>
/// Benchmarks measuring end-to-end message throughput through the processing pipeline.
/// Uses an in-memory channel as the message source to isolate CPU cost from I/O.
/// </summary>
[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
[HideColumns("Error", "StdDev", "Median", "RatioSD")]
public class MessageThroughputBenchmarks
{
    private byte[] _payloadBytes = [];

    [Params(1, 10, 50)]
    public int MessageCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var msg = new OrderMessage
        {
            OrderId = Guid.NewGuid().ToString(),
            CustomerId = "cust-bench",
            TotalAmount = 42.0m,
            Status = "Processing"
        };
        _payloadBytes = JsonSerializer.SerializeToUtf8Bytes(msg);
    }

    [Benchmark]
    public async Task Channel_SingleReader_Throughput()
    {
        var channel = Channel.CreateBounded<MessageEnvelope>(new BoundedChannelOptions(MessageCount + 1)
        {
            SingleReader = true,
            SingleWriter = true
        });

        // Write messages
        for (int i = 0; i < MessageCount; i++)
        {
            await channel.Writer.WriteAsync(CreateEnvelope(i));
        }
        channel.Writer.Complete();

        // Read + deserialize all
        await foreach (var envelope in channel.Reader.ReadAllAsync())
        {
            _ = InlineDeserializer.Deserialize<OrderMessage>(envelope.Payload);
        }
    }

    [Benchmark]
    public async Task Channel_Unbounded_Throughput()
    {
        var channel = Channel.CreateUnbounded<MessageEnvelope>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
            AllowSynchronousContinuations = false
        });

        for (int i = 0; i < MessageCount; i++)
        {
            channel.Writer.TryWrite(CreateEnvelope(i));
        }
        channel.Writer.Complete();

        await foreach (var envelope in channel.Reader.ReadAllAsync())
        {
            _ = InlineDeserializer.Deserialize<OrderMessage>(envelope.Payload);
        }
    }

    [Benchmark]
    public void Deserialize_Loop()
    {
        var payload = new ReadOnlyMemory<byte>(_payloadBytes);
        for (int i = 0; i < MessageCount; i++)
        {
            _ = InlineDeserializer.Deserialize<OrderMessage>(payload);
        }
    }

    private MessageEnvelope CreateEnvelope(int i) => new()
    {
        MessageId = $"msg-{i:0000}",
        PopReceipt = $"receipt-{i}",
        DequeueCount = 1,
        Payload = new ReadOnlyMemory<byte>(_payloadBytes)
    };
}

// Inlined deserialization helper (MessageDeserializer in source is internal)
file static class InlineDeserializer
{
    private static readonly System.Text.Json.JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
    };

    public static T? Deserialize<T>(ReadOnlyMemory<byte> payload) where T : class
        => payload.IsEmpty ? null : System.Text.Json.JsonSerializer.Deserialize<T>(payload.Span, Options);
}
