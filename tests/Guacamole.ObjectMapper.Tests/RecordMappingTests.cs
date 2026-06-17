using Guacamole.ObjectMapper.Abstract;

namespace Guacamole.ObjectMapper.Tests;

/// <summary>
/// Unit tests for constructor-based mapping of C# record types.
/// </summary>
public class RecordMappingTests
{
    /// <summary>
    /// Tests that convention mapping creates a record by matching source properties to constructor parameters.
    /// </summary>
    [Test]
    public async Task Map_Convention_ToRecord_MapsScalarProperties()
    {
        var builder = new MappingConfigurationBuilder();
        var mapper = new ObjectMapper(builder.Build());

        var id = Guid.NewGuid();
        var source = new PersonSource { Id = id, FirstName = "Alice", LastName = "Smith", Age = 30 };

        var record = mapper.Map<PersonRecord>(source);

        await Assert.That(record.Id).IsEqualTo(id);
        await Assert.That(record.FirstName).IsEqualTo("Alice");
        await Assert.That(record.LastName).IsEqualTo("Smith");
        await Assert.That(record.Age).IsEqualTo(30);
    }

    /// <summary>
    /// Tests that convention mapping maps nested record types.
    /// </summary>
    [Test]
    public async Task Map_Convention_ToRecord_MapsNestedRecord()
    {
        var builder = new MappingConfigurationBuilder();
        var mapper = new ObjectMapper(builder.Build());

        var id = Guid.NewGuid();
        var source = new PersonSource
        {
            Id = id,
            FirstName = "Bob",
            Age = 25,
            HomeAddress = new AddressSource { Street = "42 Elm St", City = "Shelbyville" }
        };

        var record = mapper.Map<PersonRecordWithAddress>(source);

        await Assert.That(record.Id).IsEqualTo(id);
        await Assert.That(record.FirstName).IsEqualTo("Bob");
        await Assert.That(record.Age).IsEqualTo(25);
        await Assert.That(record.HomeAddress).IsNotNull();
        await Assert.That(record.HomeAddress!.Street).IsEqualTo("42 Elm St");
        await Assert.That(record.HomeAddress.City).IsEqualTo("Shelbyville");
    }

    /// <summary>
    /// Tests that a registered profile mapping creates a record using its constructor,
    /// honoring ForMember and Ignore configurations.
    /// </summary>
    [Test]
    public async Task Map_Profile_ToRecord_HonorsForMemberAndIgnore()
    {
        var builder = new MappingConfigurationBuilder();
        builder.CreateMap<PersonSource, PersonRecord>()
               .ForMember<string>(dst => dst.FirstName, src => src.Email)
               .Ignore(dst => dst.LastName);
        var mapper = new ObjectMapper(builder.Build());

        var id = Guid.NewGuid();
        var source = new PersonSource
        {
            Id = id,
            FirstName = "Alice",
            LastName = "Smith",
            Age = 42,
            Email = "alice@example.com"
        };

        var record = mapper.Map<PersonRecord>(source);

        await Assert.That(record.Id).IsEqualTo(id);
        await Assert.That(record.FirstName).IsEqualTo("alice@example.com");
        await Assert.That(record.LastName).IsNull();
        await Assert.That(record.Age).IsEqualTo(42);
    }

    /// <summary>
    /// Tests that mapping a collection of source objects to records works.
    /// </summary>
    [Test]
    public async Task Map_IEnumerable_ToRecords_MapsAllElements()
    {
        var builder = new MappingConfigurationBuilder();
        var mapper = new ObjectMapper(builder.Build());

        var sources = new List<PersonSource>
        {
            new() { Id = Guid.NewGuid(), FirstName = "A", LastName = "AA", Age = 1 },
            new() { Id = Guid.NewGuid(), FirstName = "B", LastName = "BB", Age = 2 }
        };

        var records = mapper.Map<PersonSource, PersonRecord>(sources).ToList();

        await Assert.That(records.Count).IsEqualTo(2);
        await Assert.That(records[0].FirstName).IsEqualTo("A");
        await Assert.That(records[1].FirstName).IsEqualTo("B");
    }

    /// <summary>
    /// Tests that mapping a null source to a record type returns a default (all-default-values) instance.
    /// </summary>
    [Test]
    public async Task Map_NullSource_ToRecord_ReturnsDefaultInstance()
    {
        var builder = new MappingConfigurationBuilder();
        var mapper = new ObjectMapper(builder.Build());

        var record = mapper.Map<PersonRecord>(null);

        await Assert.That(record).IsNotNull();
        await Assert.That(record.FirstName).IsNull();
        await Assert.That(record.Age).IsEqualTo(0);
    }

    /// <summary>
    /// When the source has no property matching an optional constructor parameter,
    /// the parameter's explicit default value must be used.
    /// </summary>
    [Test]
    public async Task MapByConstructor_OptionalParam_NoMatchingSourceProp_UsesExplicitDefault()
    {
        var builder = new MappingConfigurationBuilder();
        var mapper = new ObjectMapper(builder.Build());

        // Source has Name but not Score or Label.
        var source = new SourceWithFewProps { Name = "Alice" };

        var record = mapper.Map<RecordWithOptionalParam>(source);

        await Assert.That(record.Name).IsEqualTo("Alice");
        await Assert.That(record.Score).IsEqualTo(42);            // explicit default
        await Assert.That(record.Label).IsEqualTo("default-label"); // explicit default
    }

    /// <summary>
    /// When the source has no property matching a required (no-default) constructor parameter,
    /// the mapper must fall back to default(T): 0 for value types, null for reference types.
    /// </summary>
    [Test]
    public async Task MapByConstructor_RequiredParam_NoMatchingSourceProp_UsesDefaultOfT()
    {
        var builder = new MappingConfigurationBuilder();
        var mapper = new ObjectMapper(builder.Build());

        // Source has Name but not RequiredScore or RequiredLabel.
        var source = new SourceWithFewProps { Name = "Bob" };

        var record = mapper.Map<RecordWithRequiredParam>(source);

        await Assert.That(record.Name).IsEqualTo("Bob");
        await Assert.That(record.RequiredScore).IsEqualTo(0);   // default(int)
        await Assert.That(record.RequiredLabel).IsNull();        // default(string?)
    }
}
