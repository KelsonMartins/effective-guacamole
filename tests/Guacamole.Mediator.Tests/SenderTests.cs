using Guacamole.Mediator.Abstract;
using Microsoft.Extensions.DependencyInjection;

namespace Guacamole.Mediator.Tests;

// ---------- test doubles ----------

public record PingQuery(string Message) : IRequest<string>;
public record FireCommand(string Payload) : IRequest;

public class PingHandler : IRequestHandler<PingQuery, string>
{
    public Task<string> Handle(PingQuery request, CancellationToken ct)
        => Task.FromResult($"pong:{request.Message}");
}

public class FireHandler : IRequestHandler<FireCommand>
{
    public static string? LastPayload { get; private set; }

    public Task Handle(FireCommand request, CancellationToken ct)
    {
        LastPayload = request.Payload;
        return Task.CompletedTask;
    }
}

// ---------- tests ----------

public class SenderTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    public SenderTests()
    {
        var services = new ServiceCollection();
        services.AddScoped<IMediator, Sender>();
        services.AddScoped<IRequestHandler<PingQuery, string>, PingHandler>();
        services.AddScoped<IRequestHandler<FireCommand>, FireHandler>();
        _provider = services.BuildServiceProvider();
        _mediator = _provider.GetRequiredService<IMediator>();
    }

    public void Dispose() => _provider.Dispose();

    [Test]
    public async Task Send_WithResponse_ReturnsHandlerResult()
    {
        var result = await _mediator.Send(new PingQuery("hello"));

        await Assert.That(result).IsEqualTo("pong:hello");
    }

    [Test]
    public async Task Send_FireAndForget_InvokesHandler()
    {
        await _mediator.Send(new FireCommand("boom"));

        await Assert.That(FireHandler.LastPayload).IsEqualTo("boom");
    }

    [Test]
    public async Task Send_WithResponse_PassesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var result = await _mediator.Send(new PingQuery("ct"), cts.Token);

        await Assert.That(result).IsNotNull();
    }

    [Test]
    public async Task Send_MissingResponseHandler_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        services.AddScoped<IMediator, Sender>();
        await using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        await Assert.That(async () => await mediator.Send(new PingQuery("x")))
            .ThrowsAsync<InvalidOperationException>();
    }

    [Test]
    public async Task Send_MissingVoidHandler_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        services.AddScoped<IMediator, Sender>();
        await using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        await Assert.That(async () => await mediator.Send(new FireCommand("x")))
            .ThrowsAsync<InvalidOperationException>();
    }
}
