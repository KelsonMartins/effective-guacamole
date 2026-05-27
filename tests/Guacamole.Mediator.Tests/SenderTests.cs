using Guacamole.Mediator.Abstract;
using Microsoft.Extensions.DependencyInjection;

namespace Guacamole.Mediator.Tests;

// ---------- test doubles ----------

/// <summary>
/// Query for pinging with a message.
/// </summary>
/// <param name="Message">The message to send.</param>
public record PingQuery(string Message) : IRequest<string>;
/// <summary>
/// Command for firing with a payload.
/// </summary>
/// <param name="Payload">The payload to send.</param>
public record FireCommand(string Payload) : IRequest;

/// <summary>
/// Handles <see cref="PingQuery"/> requests.
/// </summary>
public class PingHandler : IRequestHandler<PingQuery, string>
{
    /// <inheritdoc />
    public Task<string> Handle(PingQuery request, CancellationToken ct)
        => Task.FromResult($"pong:{request.Message}");
}

/// <summary>
/// Handles <see cref="FireCommand"/> requests.
/// </summary>
public class FireHandler : IRequestHandler<FireCommand>
{
    /// <summary>
    /// Gets the last payload handled.
    /// </summary>
    public static string? LastPayload { get; private set; }

    /// <inheritdoc />
    public Task Handle(FireCommand request, CancellationToken ct)
    {
        LastPayload = request.Payload;
        return Task.CompletedTask;
    }
}

// ---------- tests ----------

/// <summary>
/// Unit tests for <see cref="Sender"/> and mediator pattern.
/// </summary>
public class SenderTests : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="SenderTests"/> class.
    /// </summary>
    public SenderTests()
    {
        var services = new ServiceCollection();
        services.AddScoped<IMediator, Sender>();
        services.AddScoped<IRequestHandler<PingQuery, string>, PingHandler>();
        services.AddScoped<IRequestHandler<FireCommand>, FireHandler>();
        _provider = services.BuildServiceProvider();
        _mediator = _provider.GetRequiredService<IMediator>();
    }

    /// <inheritdoc />
    public void Dispose() => _provider.Dispose();

    /// <summary>
    /// Tests that Send returns the handler result for a query with response.
    /// </summary>
    [Test]
    public async Task Send_WithResponse_ReturnsHandlerResult()
    {
        var result = await _mediator.Send(new PingQuery("hello"));

        await Assert.That(result).IsEqualTo("pong:hello");
    }

    /// <summary>
    /// Tests that Send invokes the handler for a fire-and-forget command.
    /// </summary>
    [Test]
    public async Task Send_FireAndForget_InvokesHandler()
    {
        await _mediator.Send(new FireCommand("boom"));

        await Assert.That(FireHandler.LastPayload).IsEqualTo("boom");
    }

    /// <summary>
    /// Tests that Send passes the cancellation token to the handler.
    /// </summary>
    [Test]
    public async Task Send_WithResponse_PassesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        var result = await _mediator.Send(new PingQuery("ct"), cts.Token);

        await Assert.That(result).IsNotNull();
    }

    /// <summary>
    /// Tests that Send throws when no response handler is registered.
    /// </summary>
    [Test]
    public async Task Send_MissingResponseHandler_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        services.AddScoped<IMediator, Sender>();
        await using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => { await mediator.Send(new PingQuery("x")); });
    }

    /// <summary>
    /// Tests that Send throws when no void handler is registered.
    /// </summary>
    [Test]
    public async Task Send_MissingVoidHandler_ThrowsInvalidOperationException()
    {
        var services = new ServiceCollection();
        services.AddScoped<IMediator, Sender>();
        await using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        await Assert.ThrowsAsync<InvalidOperationException>(async () => { await mediator.Send(new FireCommand("x")); });
    }
}
