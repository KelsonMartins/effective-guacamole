namespace Guacamole.Mediator.Abstract;

/// <summary>
/// Marker interface for a fire-and-forget request that returns no response.
/// </summary>
public interface IRequest { }

/// <summary>
/// Marker interface for a request that returns a response of type <typeparamref name="TResponse"/>.
/// </summary>
/// <typeparam name="TResponse">The type of the response.</typeparam>
public interface IRequest<TResponse> { }
