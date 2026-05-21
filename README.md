# effective-guacamole

Two lightweight, zero-external-dependency .NET libraries extracted from production use.

| Package | NuGet | Description |
|---|---|---|
| `Guacamole.Mediator` | [![NuGet](https://img.shields.io/nuget/v/Guacamole.Mediator)](https://www.nuget.org/packages/Guacamole.Mediator) | Mediator pattern – request/response and fire-and-forget |
| `Guacamole.ObjectMapper` | [![NuGet](https://img.shields.io/nuget/v/Guacamole.ObjectMapper)](https://www.nuget.org/packages/Guacamole.ObjectMapper) | Convention-based and profile-driven object mapper |

---

## Guacamole.Mediator

### Install

```bash
dotnet add package Guacamole.Mediator
```

### Concepts

| Type | Purpose |
|---|---|
| `IRequest<TResponse>` | Marker for a request that returns `TResponse` |
| `IRequest` | Marker for a fire-and-forget request |
| `IRequestHandler<TRequest, TResponse>` | Handles `IRequest<TResponse>` |
| `IRequestHandler<TRequest>` | Handles fire-and-forget `IRequest` |
| `IMediator` | Dispatches requests to their handlers |

### Register

```csharp
builder.Services.AddMediator(typeof(MyHandler).Assembly);
```

All `IRequestHandler<,>` and `IRequestHandler<>` implementations in the assembly are registered as scoped services automatically.

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
var user = await mediator.Send(new GetUserQuery(id), ct);
await mediator.Send(new DeleteUserCommand(id), ct);
```

---

## Guacamole.ObjectMapper

### Install

```bash
dotnet add package Guacamole.ObjectMapper
```

### Concepts

| Type | Purpose |
|---|---|
| `IObjectMapper` | Core mapping interface |
| `IMappingProfile` | Groups related mapping configurations |
| `MappingProfile` | Base class for profiles |
| `IMappingConfigurationBuilder` | Fluent builder for type maps |
| `ITypeMappingConfiguration<S,D>` | Per-pair fluent configuration |

### Register

```csharp
// Scan assemblies for IMappingProfile implementations
builder.Services.AddObjectMapper(typeof(UserProfile).Assembly);

// Or pass profiles explicitly
builder.Services.AddObjectMapper(new UserProfile(), new OrderProfile());

// Or configure inline
builder.Services.AddObjectMapper(cfg =>
{
    cfg.CreateMap<User, UserDto>();
});
```

### Define a profile

```csharp
public class UserProfile : MappingProfile
{
    public override void Configure(IMappingConfigurationBuilder builder)
    {
        builder.CreateMap<User, UserDto>()
               .ForMember(dst => dst.FullName, src => src.Name)
               .ForMember<string, int>(dst => dst.AgeLabel, src => $"{src.Age} years old")
               .Ignore(dst => dst.InternalNotes)
               .ReverseMap();
    }
}
```

### Map

```csharp
// Inject IObjectMapper
var dto  = mapper.Map<UserDto>(user);               // single object
var dto2 = mapper.Map<User, UserDto>(user);          // explicit types
var list = mapper.Map<User, UserDto>(users);         // IEnumerable<T>
var user = mapper.ReverseMap<User, UserDto>(dto);    // reverse

// Map onto an existing instance
mapper.Map(command, existingEntity);
```

### Convention-based mapping

When no profile is registered for a pair, properties are matched by name (case-insensitive). Supported conversions:

- Same type or assignable
- Enum ↔ Enum (by underlying int value)
- Primitives, `string`, `decimal`, `DateTime`, `DateTimeOffset`, `Guid`
- Nested complex objects (recursive)
- Collections (`IEnumerable<T>`, `List<T>`, arrays)
- Circular references (detected and broken automatically)

### Attributes

```csharp
[MapTo(typeof(UserDto), ReverseMap = true)]
public class User
{
    [MapFrom("UserName")]
    public string Name { get; set; }

    [IgnoreMap]
    public string Password { get; set; }
}
```

> Attributes serve as metadata hints. The mapper respects `[IgnoreMap]` during convention mapping.

---

## CI / CD

| Branch | Action |
|---|---|
| `develop` | Build → Test → Pack with `-alpha.{run}` suffix → push to NuGet |
| `main` | Build → Test → Pack stable version → push to NuGet |

### Required secret

Add `NUGET_API_KEY` to the repository secrets (Settings → Secrets → Actions) with a valid NuGet.org API key.

---

## Contributing

1. Fork the repo
2. Create a feature branch from `develop`
3. Add tests
4. Open a PR targeting `develop`
