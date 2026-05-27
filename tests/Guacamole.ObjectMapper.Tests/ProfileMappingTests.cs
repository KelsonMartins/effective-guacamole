using Guacamole.ObjectMapper.Abstract;
using Guacamole.ObjectMapper.Abstract.Base;

namespace Guacamole.ObjectMapper.Tests;

// ---------- profile ----------

/// <summary>
/// Mapping profile for <see cref="Product"/> to <see cref="ProductDto"/>.
/// </summary>
public class ProductProfile : MappingProfile
{
    /// <inheritdoc />
    public override void Configure(IMappingConfigurationBuilder builder)
    {
        builder.CreateMap<Product, ProductDto>()
             .ForMember<string>(dst => dst.Label, src => src.Code)
               .Ignore(dst => dst.InternalNotes);
    }
}

// ---------- tests ----------

/// <summary>
/// Unit tests for mapping profiles.
/// </summary>
public class ProfileMappingTests
{
    private readonly ObjectMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProfileMappingTests"/> class.
    /// </summary>
    public ProfileMappingTests()
    {
        var builder = new MappingConfigurationBuilder();
        new ProductProfile().Configure(builder);
        _mapper = new ObjectMapper(builder.Build());
    }

    /// <summary>
    /// Tests ForMember maps from the specified source property.
    /// </summary>
    [Test]
    public async Task ForMember_MapsFromSpecifiedSourceProperty()
    {
        var product = new Product { Id = 1, Code = "SKU-001", Price = 19.99m };

        var dto = _mapper.Map<ProductDto>(product);

        await Assert.That(dto.Label).IsEqualTo("SKU-001");
    }

    /// <summary>
    /// Tests Ignore excludes a property from mapping.
    /// </summary>
    [Test]
    public async Task Ignore_ExcludesPropertyFromMapping()
    {
        var product = new Product { Id = 1, Code = "X", InternalNotes = "secret" };

        var dto = _mapper.Map<ProductDto>(product);

        await Assert.That(dto.InternalNotes).IsEqualTo(string.Empty);
    }

    /// <summary>
    /// Tests convention fallback maps unspecified properties.
    /// </summary>
    [Test]
    public async Task Profile_ConventionFallback_MapsUnspecifiedProperties()
    {
        var product = new Product { Id = 42, Price = 5.00m };

        var dto = _mapper.Map<ProductDto>(product);

        await Assert.That(dto.Id).IsEqualTo(42);
        await Assert.That(dto.Price).IsEqualTo(5.00m);
    }

    /// <summary>
    /// Tests ForMember with converter applies the conversion.
    /// </summary>
    [Test]
    public async Task ForMember_WithConverter_AppliesConversion()
    {
        var builder = new MappingConfigurationBuilder();
        builder.CreateMap<Product, ProductDto>()
               .ForMember<string, string>(dst => dst.Label, src => $"${src.Price:F2}");
        var mapper = new ObjectMapper(builder.Build());

        var dto = mapper.Map<ProductDto>(new Product { Price = 12.5m });

        await Assert.That(dto.Label).IsEqualTo("$12.50");
    }

    /// <summary>
    /// Tests ReverseMap creates symmetric mapping.
    /// </summary>
    [Test]
    public async Task ReverseMap_CreatesSymmetricMapping()
    {
        var builder = new MappingConfigurationBuilder();
        builder.CreateMap<Product, ProductDto>()
             .ForMember<string>(dst => dst.Label, src => src.Code)
               .ReverseMap()
             .ForMember<string>(dst => dst.Code, src => src.Label);
        var mapper = new ObjectMapper(builder.Build());

        var dto = mapper.Map<ProductDto>(new Product { Id = 7, Code = "ABC" });
        var product = mapper.ReverseMap<Product, ProductDto>(dto);

        await Assert.That(product.Code).IsEqualTo("ABC");
        await Assert.That(product.Id).IsEqualTo(7);
    }
}
