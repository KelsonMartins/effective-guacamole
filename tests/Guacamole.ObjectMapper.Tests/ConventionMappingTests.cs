namespace Guacamole.ObjectMapper.Tests;

/// <summary>
/// Unit tests for convention-based object mapping.
/// </summary>
public class ConventionMappingTests
{
    private readonly ObjectMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConventionMappingTests"/> class.
    /// </summary>
    public ConventionMappingTests()
    {
        var builder = new MappingConfigurationBuilder();
        // No profiles – pure convention mapping
        _mapper = new ObjectMapper(builder.Build());
    }

    /// <summary>
    /// Tests that scalar properties are mapped by name.
    /// </summary>
    [Test]
    public async Task Map_ScalarProperties_MappedByName()
    {
        var id = Guid.NewGuid();
        var src = new Source { Id = id, Name = "Alice", Age = 30, Balance = 9.99m };

        var dst = _mapper.Map<Destination>(src);

        await Assert.That(dst.Id).IsEqualTo(id);
        await Assert.That(dst.Name).IsEqualTo("Alice");
        await Assert.That(dst.Age).IsEqualTo(30);
        await Assert.That(dst.Balance).IsEqualTo(9.99m);
    }

    /// <summary>
    /// Tests that enum properties are mapped by their underlying value.
    /// </summary>
    [Test]
    public async Task Map_EnumProperty_MappedByUnderlyingValue()
    {
        var src = new Source { Status = Status.Pending };

        var dst = _mapper.Map<Destination>(src);

        await Assert.That(dst.Status).IsEqualTo(Status.Pending);
    }

    /// <summary>
    /// Tests that mapping a null source returns a default instance.
    /// </summary>
    [Test]
    public async Task Map_NullSource_ReturnsDefaultInstance()
    {
        var dst = _mapper.Map<Destination>(null);

        await Assert.That(dst).IsNotNull();
    }

    /// <summary>
    /// Tests that mapping with an explicit type pair works.
    /// </summary>
    [Test]
    public async Task Map_ExplicitTypePair_Works()
    {
        var src = new Source { Name = "Bob" };

        var dst = _mapper.Map<Source, Destination>(src);

        await Assert.That(dst.Name).IsEqualTo("Bob");
    }

    /// <summary>
    /// Tests that mapping to an existing instance overwrites its properties.
    /// </summary>
    [Test]
    public async Task Map_ToExistingInstance_OverwritesProperties()
    {
        var src = new Source { Name = "Updated", Age = 99 };
        var dst = new Destination { Name = "Old", Age = 0 };

        _mapper.Map(src, dst);

        await Assert.That(dst.Name).IsEqualTo("Updated");
        await Assert.That(dst.Age).IsEqualTo(99);
    }

    /// <summary>
    /// Tests that mapping an IEnumerable maps all elements.
    /// </summary>
    [Test]
    public async Task Map_IEnumerable_MapsAllElements()
    {
        var sources = new List<Source>
        {
            new() { Name = "A", Age = 1 },
            new() { Name = "B", Age = 2 }
        };

        var results = _mapper.Map<Source, Destination>(sources).ToList();

        await Assert.That(results.Count).IsEqualTo(2);
        await Assert.That(results[0].Name).IsEqualTo("A");
        await Assert.That(results[1].Name).IsEqualTo("B");
    }

    /// <summary>
    /// Tests that nested complex objects are mapped by convention.
    /// </summary>
    [Test]
    public async Task Map_NestedComplexObject_MappedByConvention()
    {
        var src = new Source
        {
            Address = new Address { Street = "123 Main St", City = "Springfield" }
        };

        var dst = _mapper.Map<Destination>(src);

        await Assert.That(dst.Address).IsNotNull();
        await Assert.That(dst.Address!.Street).IsEqualTo("123 Main St");
        await Assert.That(dst.Address.City).IsEqualTo("Springfield");
    }

    /// <summary>
    /// Tests that ReverseMap maps back to the source type.
    /// </summary>
    [Test]
    public async Task ReverseMap_MapsBackToSource()
    {
        var dst = new Destination { Name = "Charlie", Age = 42 };

        var src = _mapper.ReverseMap<Source, Destination>(dst);

        await Assert.That(src.Name).IsEqualTo("Charlie");
        await Assert.That(src.Age).IsEqualTo(42);
    }
}
