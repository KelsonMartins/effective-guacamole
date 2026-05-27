# Object Mapper Usage Examples

This document demonstrates how to use the high-performance object mapper in the Secure-Sense application.

## Setup

The object mapper is automatically registered with dependency injection when you call `AddCommonServices()`:

```csharp
services.AddCommonServices(configuration);
```

This registers the `IObjectMapper` interface with the container and scans for mapping profiles in the specified assemblies.

## Basic Usage

### Simple Mapping

```csharp
public class UserController : ControllerBase
{
    private readonly IObjectMapper _mapper;

    public UserController(IObjectMapper mapper)
    {
        _mapper = mapper;
    }

    public async Task<ActionResult<UserDto>> GetUser(int id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        var userDto = _mapper.Map<UserDto>(user);
        return Ok(userDto);
    }
}
```

### Collection Mapping

```csharp
public async Task<ActionResult<IEnumerable<ProductDto>>> GetProducts()
{
    var products = await _productRepository.GetAllAsync();
    var productDtos = _mapper.Map<Product, ProductDto>(products);
    return Ok(productDtos);
}
```

### PagedList Mapping

```csharp
public async Task<ActionResult<PagedList<UserDto>>> GetUsers(int pageNumber = 1, int pageSize = 10)
{
    var users = await _userRepository.GetPagedAsync(pageNumber, pageSize);
    var userDtos = _mapper.Map<PagedList<UserDto>>(users);
    return Ok(userDtos);
}
```

### Reverse Mapping

```csharp
[HttpPost]
public async Task<ActionResult<UserDto>> CreateUser([FromBody] CreateUserDto createUserDto)
{
    var user = _mapper.ReverseMap<User, CreateUserDto>(createUserDto);
    await _userRepository.AddAsync(user);

    var userDto = _mapper.Map<UserDto>(user);
    return CreatedAtAction(nameof(GetUser), new { id = user.Id }, userDto);
}
```

### Mapping to Existing Object

```csharp
[HttpPut("{id}")]
public async Task<ActionResult> UpdateUser(int id, [FromBody] UpdateUserDto updateUserDto)
{
    var user = await _userRepository.GetByIdAsync(id);
    if (user == null) return NotFound();

    _mapper.Map(updateUserDto, user);
    await _userRepository.UpdateAsync(user);

    return NoContent();
}
```

## Creating Custom Mapping Profiles

### Basic Profile

```csharp
public class UserMappingProfile : MappingProfile
{
    public override void Configure(IMappingConfigurationBuilder builder)
    {
        builder.CreateMap<User, UserDto>()
            .ForMember(dest => dest.FullName, src => $"{src.FirstName} {src.LastName}")
            .ForMember(dest => dest.Email, src => src.EmailAddress);

        builder.CreateMap<UserDto, User>()
            .ForMember(dest => dest.EmailAddress, src => src.Email)
            .Ignore(dest => dest.CreatedAt)
            .Ignore(dest => dest.UpdatedAt);
    }
}
```

### Advanced Profile with Custom Conversions

```csharp
public class OrderMappingProfile : MappingProfile
{
    public override void Configure(IMappingConfigurationBuilder builder)
    {
        builder.CreateMap<Order, OrderDto>()
            .ForMember(dest => dest.CustomerName,
                      order => $"{order.Customer.FirstName} {order.Customer.LastName}")
            .ForMember(dest => dest.TotalAmount,
                      order => order.Items.Sum(i => i.Price * i.Quantity))
            .ForMember(dest => dest.Status,
                      order => order.StatusId switch
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

## Performance Features

### Compiled Expression Caching
The mapper uses compiled expressions and caching for maximum performance:

- First mapping compiles the expression
- Subsequent mappings use the cached compiled version
- Thread-safe concurrent dictionary for cache storage

### Optimized Collection Handling
- Special handling for IEnumerable, List, Array types
- Efficient PagedList mapping with property preservation
- Minimal memory allocations during collection mapping

### Smart Type Detection
- Automatic detection of compatible types
- Value type conversion support
- Recursive mapping for complex nested objects

## Best Practices

### 1. Use Dependency Injection
Always inject `IObjectMapper` rather than creating instances manually.

### 2. Create Specific Profiles
Group related mappings into focused profiles:

```csharp
public class ProductMappingProfile : MappingProfile { ... }
public class OrderMappingProfile : MappingProfile { ... }
public class UserMappingProfile : MappingProfile { ... }
```

### 3. Configure Reverse Mappings Explicitly
When you need bidirectional mapping, configure both directions:

```csharp
builder.CreateMap<Entity, Dto>()
    .ForMember(dest => dest.DisplayName, src => src.Name);

builder.CreateMap<Dto, Entity>()
    .ForMember(dest => dest.Name, src => src.DisplayName)
    .Ignore(dest => dest.CreatedAt);
```

### 4. Use Ignore for Read-Only Properties
Explicitly ignore properties that should not be mapped:

```csharp
builder.CreateMap<UserDto, User>()
    .Ignore(dest => dest.Id)
    .Ignore(dest => dest.CreatedAt)
    .Ignore(dest => dest.LastModified);
```

### 5. Leverage Custom Converters for Complex Logic
For complex transformations, use custom converter functions:

```csharp
builder.CreateMap<User, UserDto>()
    .ForMember(dest => dest.Age, user => CalculateAge(user.BirthDate))
    .ForMember(dest => dest.Status, user => GetUserStatus(user));
```

## Error Handling

The mapper includes comprehensive error handling and logging:

- Failed mappings are logged with context
- Type conversion errors are handled gracefully
- Missing properties are ignored with optional warnings

## Integration with Domain Models

The mapper works seamlessly with your existing domain architecture:

```csharp
// Entity Framework entities
public class UserEntity : TEntity { ... }

// Domain models
public class User : TModel { ... }

// DTOs
public class UserDto { ... }

// All can be mapped between each other with proper profile configuration
```
