namespace Guacamole.Mediator.Abstract;

/// <summary>
/// Handles a request that returns a response of type <typeparamref name="TResponse"/>.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IRequestHandler<TRequest, TResponse> where TRequest : IRequest<TResponse>
{
    /// <summary>Handles the request and returns the response.</summary>
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Handles a fire-and-forget request that returns no response.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
public interface IRequestHandler<TRequest> where TRequest : IRequest
{
    /// <summary>Handles the request.</summary>
    Task Handle(TRequest request, CancellationToken cancellationToken);
}
