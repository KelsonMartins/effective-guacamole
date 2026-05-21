using Guacamole.Mediator.Abstract;
using Microsoft.Extensions.DependencyInjection;

namespace Guacamole.Mediator;

/// <summary>
/// Default <see cref="IMediator"/> implementation that resolves handlers from an <see cref="IServiceProvider"/>.
/// </summary>
public sealed class Sender(IServiceProvider provider) : IMediator
{
    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        var requestType = request.GetType();
        var handlerType = typeof(IRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));
        var handler = provider.GetService(handlerType)
            ?? throw new InvalidOperationException(
                $"No handler registered for request type '{requestType.Name}'. "
                + $"Ensure you called AddMediator() and the handler implements IRequestHandler<{requestType.Name}, {typeof(TResponse).Name}>.");

        return await ((dynamic)handler).Handle((dynamic)request, cancellationToken);
    }

    public async Task Send(IRequest request, CancellationToken cancellationToken = default)
    {
        var requestType = request.GetType();
        var handlerType = typeof(IRequestHandler<>).MakeGenericType(requestType);
        var handler = provider.GetService(handlerType)
            ?? throw new InvalidOperationException(
                $"No handler registered for request type '{requestType.Name}'. "
                + $"Ensure you called AddMediator() and the handler implements IRequestHandler<{requestType.Name}>.");

        await ((dynamic)handler).Handle((dynamic)request, cancellationToken);
    }
}
