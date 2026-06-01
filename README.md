# effective-guacamole

Three .NET 10 libraries extracted from production use.

All packages target `net10.0` and are MIT-licensed.

| Package | Version | Description |
|---|---|---|
| [`Guacamole.Mediator`](src/Guacamole.Mediator/README.md) | [![NuGet](https://img.shields.io/nuget/v/Guacamole.Mediator)](https://www.nuget.org/packages/Guacamole.Mediator) | Mediator pattern — request/response and fire-and-forget dispatch via DI |
| [`Guacamole.ObjectMapper`](src/Guacamole.ObjectMapper/README.md) | [![NuGet](https://img.shields.io/nuget/v/Guacamole.ObjectMapper)](https://www.nuget.org/packages/Guacamole.ObjectMapper) | Convention-based and profile-driven object-to-object mapper |
| [`Guacamole.QueueProcessor`](src/Guacamole.QueueProcessor/README.md) | [![NuGet](https://img.shields.io/nuget/v/Guacamole.QueueProcessor)](https://www.nuget.org/packages/Guacamole.QueueProcessor) | Adaptive queue processing runtime with Azure Storage Queue, Service Bus, and RabbitMQ providers |

---

## Guacamole.Mediator

```bash
dotnet add package Guacamole.Mediator
```

Resolves `IRequestHandler<TRequest, TResponse>` and `IRequestHandler<TRequest>` from the DI container at call time. No pipeline, no behaviours — just dispatch.

```csharp
// Register — scans assembly for all IRequestHandler implementations
builder.Services.AddMediator(typeof(GetUserHandler).Assembly);

// Define
public record GetUserQuery(Guid Id) : IRequest<UserDto>;

public class GetUserHandler(IUserRepository repo) : IRequestHandler<GetUserQuery, UserDto>
{
    public async Task<UserDto> Handle(GetUserQuery request, CancellationToken ct)
        => await repo.GetByIdAsync(request.Id, ct);
}

// Dispatch
var user = await mediator.Send(new GetUserQuery(id), ct);
await mediator.Send(new DeleteUserCommand(id), ct);  // fire-and-forget
```

> See [src/Guacamole.Mediator/README.md](src/Guacamole.Mediator/README.md) for full API reference.

---

## Guacamole.ObjectMapper

```bash
dotnet add package Guacamole.ObjectMapper
```

Maps objects by name-matching convention or explicit `MappingProfile`. Supports collections, nested types, circular references, enum conversion, and attribute-based hints.

```csharp
// Register
builder.Services.AddObjectMapper(typeof(UserProfile).Assembly);

// Define a profile
public class UserProfile : MappingProfile
{
    public override void Configure(IMappingConfigurationBuilder builder)
    {
        builder.CreateMap<User, UserDto>()
               .ForMember(dst => dst.FullName, src => src.Name)
               .Ignore(dst => dst.InternalNotes)
               .ReverseMap();
    }
}

// Map
var dto  = mapper.Map<UserDto>(user);
var list = mapper.Map<User, UserDto>(users);
var user = mapper.ReverseMap<User, UserDto>(dto);
```

> See [src/Guacamole.ObjectMapper/README.md](src/Guacamole.ObjectMapper/README.md) for profiles, attributes, advanced conversions, and performance notes.

---

## Guacamole.QueueProcessor

```bash
dotnet add package Guacamole.QueueProcessor
```

High-throughput queue processing with strongly-typed single-message and batch processors, adaptive worker scaling, retry/dead-letter behavior, and provider-specific adapters.

```csharp
// Register Azure Storage Queue processing
builder.Services.AddAzureQueueProcessing(builder.Configuration, qp =>
{
    qp.AddProcessor<OrderPlaced, OrderPlacedProcessor>("orders");
});
```

Also supports:

- `AddServiceBusQueueProcessing(...)`
- `AddRabbitMqQueueProcessing(...)`

> See [src/Guacamole.QueueProcessor/README.md](src/Guacamole.QueueProcessor/README.md) for setup, configuration, retries, batch processors, and runtime behavior.

---

## Repository layout

```
src/
  Guacamole.Mediator/        # Mediator library
  Guacamole.ObjectMapper/    # ObjectMapper library
  Guacamole.QueueProcessor/  # Queue processing framework
tests/
  Guacamole.Mediator.Tests/
  Guacamole.ObjectMapper.Tests/
  Guacamole.QueueProcessor.Tests/
  Guacamole.QueueProcessor.Benchmarks/
```

Shared build properties (version, authors, pack settings) live in [`Directory.Build.props`](Directory.Build.props).

---

## CI / CD

| Branch | Workflow | Publishes |
|---|---|---|
| `develop` | Alpha Release | `1.0.0-alpha.{run}` pre-release to NuGet + GitHub pre-release |
| `main` | Release | `1.0.0` stable to NuGet + GitHub release |

PRs targeting either branch run the full build and test pipeline and post results as a PR comment.

---

## Contributing

1. Fork the repo
2. Branch from `develop`
3. Add or update tests
4. Open a PR targeting `develop`
