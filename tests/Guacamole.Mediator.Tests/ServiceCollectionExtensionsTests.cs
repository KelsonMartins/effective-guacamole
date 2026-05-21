using Guacamole.Mediator.Abstract;
using Guacamole.Mediator.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Guacamole.Mediator.Tests;

// Handlers defined in SenderTests.cs are re-used for assembly scanning.

public class ServiceCollectionExtensionsTests : IDisposable
{
    private ServiceProvider? _provider;

    public void Dispose() => _provider?.Dispose();

    [Test]
    public async Task AddMediator_ScansAssembly_RegistersResponseHandler()
    {
        var services = new ServiceCollection();
        services.AddMediator(typeof(PingHandler).Assembly);
        _provider = services.BuildServiceProvider();

        var handler = _provider.GetService<IRequestHandler<PingQuery, string>>();

        await Assert.That(handler).IsNotNull();
        await Assert.That(handler).IsTypeOf<PingHandler>();
    }

    [Test]
    public async Task AddMediator_ScansAssembly_RegistersVoidHandler()
    {
        var services = new ServiceCollection();
        services.AddMediator(typeof(FireHandler).Assembly);
        _provider = services.BuildServiceProvider();

        var handler = _provider.GetService<IRequestHandler<FireCommand>>();

        await Assert.That(handler).IsNotNull();
        await Assert.That(handler).IsTypeOf<FireHandler>();
    }

    [Test]
    public async Task AddMediator_RegistersIMediator()
    {
        var services = new ServiceCollection();
        services.AddMediator(typeof(PingHandler).Assembly);
        _provider = services.BuildServiceProvider();

        var mediator = _provider.GetService<IMediator>();

        await Assert.That(mediator).IsNotNull();
        await Assert.That(mediator).IsTypeOf<Sender>();
    }

    [Test]
    public async Task AddMediator_MultipleAssemblies_RegistersHandlersFromAll()
    {
        var services = new ServiceCollection();
        services.AddMediator(typeof(PingHandler).Assembly, typeof(FireHandler).Assembly);
        _provider = services.BuildServiceProvider();

        var responseHandler = _provider.GetService<IRequestHandler<PingQuery, string>>();
        var voidHandler = _provider.GetService<IRequestHandler<FireCommand>>();

        await Assert.That(responseHandler).IsNotNull();
        await Assert.That(voidHandler).IsNotNull();
    }
}
