namespace Guacamole.Mediator.Abstract;

/// <summary>
/// Dispatches requests to their registered handlers.
/// </summary>
public interface IMediator
{
    /// <summary>Dispatches a fire-and-forget request.</summary>
    Task Send(IRequest request, CancellationToken cancellationToken = default);

    /// <summary>Dispatches a request and returns its response.</summary>
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);
}
