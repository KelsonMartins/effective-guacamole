using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Guacamole.QueueProcessor.Benchmarks;

/// <summary>
/// Benchmarks comparing generic vs. reflective JSON deserialization.
/// MessageDeserializer is internal — logic is inlined here to measure the same code path.
/// </summary>
[SimpleJob(RuntimeMoniker.Net80)]
[MemoryDiagnoser]
[HideColumns("Error", "StdDev", "Median", "RatioSD")]
public class MessageDeserializerBenchmarks
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private ReadOnlyMemory<byte> _smallPayload;
    private ReadOnlyMemory<byte> _largePayload;

    [GlobalSetup]
    public void Setup()
    {
        var small = new OrderMessage
        {
            OrderId = Guid.NewGuid().ToString(),
            CustomerId = "cust-001",
            TotalAmount = 99.99m,
            Status = "Pending"
        };

        var large = new OrderBatchMessage
        {
            BatchId = Guid.NewGuid().ToString(),
            Orders = Enumerable.Range(0, 50).Select(i => new OrderMessage
            {
                OrderId = Guid.NewGuid().ToString(),
                CustomerId = $"cust-{i:000}",
                TotalAmount = i * 10.5m,
                Status = "Pending"
            }).ToList()
        };

        _smallPayload = new ReadOnlyMemory<byte>(JsonSerializer.SerializeToUtf8Bytes(small));
        _largePayload = new ReadOnlyMemory<byte>(JsonSerializer.SerializeToUtf8Bytes(large));
    }

    [Benchmark(Baseline = true)]
    public OrderMessage? Deserialize_Small_Generic()
        => JsonSerializer.Deserialize<OrderMessage>(_smallPayload.Span, Options);

    [Benchmark]
    public object? Deserialize_Small_Reflective()
        => JsonSerializer.Deserialize(_smallPayload.Span, typeof(OrderMessage), Options);

    [Benchmark]
    public OrderBatchMessage? Deserialize_Large_Generic()
        => JsonSerializer.Deserialize<OrderBatchMessage>(_largePayload.Span, Options);

    [Benchmark]
    public object? Deserialize_Large_Reflective()
        => JsonSerializer.Deserialize(_largePayload.Span, typeof(OrderBatchMessage), Options);
}

public sealed class OrderMessage
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class OrderBatchMessage
{
    public string BatchId { get; set; } = string.Empty;
    public List<OrderMessage> Orders { get; set; } = [];
}
