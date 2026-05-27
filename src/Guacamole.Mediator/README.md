## Guacamole.Mediator

A minimal mediator for .NET that dispatches requests to scoped handlers resolved from the DI container.

### Install

```bash
dotnet add package Guacamole.Mediator
```

### Register

```csharp
// Scan a single assembly
builder.Services.AddMediator(typeof(GetUserHandler).Assembly);

// Scan multiple assemblies
builder.Services.AddMediator(
    typeof(GetUserHandler).Assembly,
    typeof(OrderHandler).Assembly);
```

All `IRequestHandler<TRequest, TResponse>` and `IRequestHandler<TRequest>` implementations found in the scanned assemblies are registered as scoped services automatically. `IMediator` is also registered as scoped.

### Define a request and handler

```csharp
// Request with response
public record GetUserQuery(Guid Id) : IRequest<UserDto>;

public class GetUserHandler(IUserRepository repo) : IRequestHandler<GetUserQuery, UserDto>
{
    public async Task<UserDto> Handle(GetUserQuery request, CancellationToken ct)
        => await repo.GetByIdAsync(request.Id, ct);
}

// Fire-and-forget
public record DeleteUserCommand(Guid Id) : IRequest;

public class DeleteUserHandler(IUserRepository repo) : IRequestHandler<DeleteUserCommand>
{
    public async Task Handle(DeleteUserCommand request, CancellationToken ct)
        => await repo.DeleteAsync(request.Id, ct);
}
```

### Send

```csharp
// Inject IMediator
var user = await mediator.Send(new GetUserQuery(id), ct);  // returns TResponse
await mediator.Send(new DeleteUserCommand(id), ct);        // fire-and-forget
```

### Error handling

If no handler is registered for a request type, `Send` throws `InvalidOperationException` with a diagnostic message naming the missing handler interface:

```
No handler registered for request type 'GetUserQuery'.
Ensure you called AddMediator() and the handler implements IRequestHandler<GetUserQuery, UserDto>.
```
