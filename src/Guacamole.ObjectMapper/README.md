## Guacamole.ObjectMapper

A lightweight object-to-object mapper for .NET with fluent profile configuration, convention-based fallback, and attribute hints.

### Install

```bash
dotnet add package Guacamole.ObjectMapper
```

### Register

```csharp
// Scan an assembly for IMappingProfile implementations
builder.Services.AddObjectMapper(typeof(UserProfile).Assembly);

// Pass profiles directly
builder.Services.AddObjectMapper(new UserProfile(), new OrderProfile());

// Inline configuration
builder.Services.AddObjectMapper(cfg =>
{
    cfg.CreateMap<User, UserDto>();
});

// No profiles — convention mapping only
builder.Services.AddObjectMapper();
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
var dto  = mapper.Map<UserDto>(user);                 // infer source type
var dto2 = mapper.Map<User, UserDto>(user);           // explicit source type
var list = mapper.Map<User, UserDto>(users);          // IEnumerable<T>
var user = mapper.ReverseMap<User, UserDto>(dto);     // reverse direction

// Map onto an existing instance
mapper.Map(command, existingEntity);
```

### Convention-based mapping

When no profile is registered for a pair, properties are matched by name (case-insensitive). Supported conversions:

- Same type or assignable
- Enum ↔ Enum (by underlying `int` value)
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

| Attribute | Target | Effect |
|---|---|---|
| `[MapTo(typeof(T))]` | Class | Registers a convention map to `T`; `ReverseMap = true` adds the inverse |
| `[MapFrom("Prop")]` | Property | Reads from the named source property instead of matching by name |
| `[IgnoreMap]` | Property | Excludes the property from convention mapping |

### Advanced profile with custom conversions

Use the converter overload of `ForMember` when the source and destination property types differ or when the value requires computation:

```csharp
public class OrderProfile : MappingProfile
{
    public override void Configure(IMappingConfigurationBuilder builder)
    {
        builder.CreateMap<Order, OrderDto>()
               .ForMember<string, string>(
                   dst => dst.CustomerName,
                   src => $"{src.Customer.FirstName} {src.Customer.LastName}")
               .ForMember<decimal, decimal>(
                   dst => dst.TotalAmount,
                   src => src.Items.Sum(i => i.Price * i.Quantity))
               .ForMember<string, int>(
                   dst => dst.Status,
                   src => src.StatusId switch
                   {
                       1 => "Pending",
                       2 => "Processing",
                       3 => "Shipped",
                       4 => "Delivered",
                       _ => "Unknown"
                   });
    }
}
```

### Performance

- **Compiled expression caching** — the first mapping for a type pair compiles and caches a `Func<object, object>` in a `ConcurrentDictionary`; subsequent calls use the cached delegate directly.
- **Circular-reference detection** — a per-call `HashSet<object>` (reference equality) tracks visited instances; a cycle returns a default instance instead of recursing infinitely.
- **Scoped registration** — `IObjectMapper` is registered as `Scoped`, so the singleton `MappingConfiguration` is shared and the mapper itself is lightweight per request.

### Best practices

- **Inject `IObjectMapper`** — never instantiate `ObjectMapper` directly; resolve it through DI.
- **Group mappings by feature** — one `MappingProfile` per domain area keeps configurations focused and easy to test.
- **Prefer `.ReverseMap()`** for symmetric pairs — it registers the inverse automatically so both directions stay in sync.
- **Use `.Ignore()`** for destination properties that must not be overwritten (e.g. audit timestamps, computed columns).
- **Use the converter overload for cross-type members** — `ForMember<TDest, TSrc>(dst => ..., src => ...)` handles type coercion and computed values without requiring a separate profile step.

### Error handling

- **Null source** — all `Map` and `ReverseMap` overloads return a default-constructed destination instance when the source is `null`; no exception is thrown.
- **Null source on `Map(source, destination)`** — the void overload is a no-op when either argument is `null`.
- **Missing properties** — unmatched destination properties are silently skipped during convention mapping.
- **Cycles** — circular object graphs are broken by returning an empty destination instance at the point of re-entry.
