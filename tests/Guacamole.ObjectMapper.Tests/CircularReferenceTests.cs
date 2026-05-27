using Guacamole.ObjectMapper.Abstract;
using Guacamole.ObjectMapper.Abstract.Base;

namespace Guacamole.ObjectMapper.Tests;

// ---------- profiles ----------

/// <summary>
/// Mapping profile for parent/child circular reference tests.
/// </summary>
public class ParentChildProfile : MappingProfile
{
    /// <inheritdoc />
    public override void Configure(IMappingConfigurationBuilder builder)
    {
        builder.CreateMap<Parent, ParentDto>();
        builder.CreateMap<Child, ChildDto>();
    }
}

// ---------- tests ----------

/// <summary>
/// Unit tests for circular reference mapping.
/// </summary>
public class CircularReferenceTests
{
    private readonly ObjectMapper _mapper;

    /// <summary>
    /// Initializes a new instance of the <see cref="CircularReferenceTests"/> class.
    /// </summary>
    public CircularReferenceTests()
    {
        var builder = new MappingConfigurationBuilder();
        new ParentChildProfile().Configure(builder);
        _mapper = new ObjectMapper(builder.Build());
    }

    /// <summary>
    /// Tests mapping a parent with children does not throw.
    /// </summary>
    [Test]
    public async Task Map_ParentWithChildren_DoesNotThrow()
    {
        var parent = new Parent { Name = "Root" };
        var child1 = new Child { Name = "Child1", Parent = parent };
        var child2 = new Child { Name = "Child2", Parent = parent };
        parent.Children.Add(child1);
        parent.Children.Add(child2);

        var dto = _mapper.Map<ParentDto>(parent);

        await Assert.That(dto).IsNotNull();
        await Assert.That(dto.Name).IsEqualTo("Root");
    }

    /// <summary>
    /// Tests mapping a parent with children maps child names.
    /// </summary>
    [Test]
    public async Task Map_ParentWithChildren_MapsChildNames()
    {
        var parent = new Parent { Name = "P" };
        parent.Children.Add(new Child { Name = "C1", Parent = parent });
        parent.Children.Add(new Child { Name = "C2", Parent = parent });

        var dto = _mapper.Map<ParentDto>(parent);

        await Assert.That(dto.Children.Count).IsEqualTo(2);
        await Assert.That(dto.Children[0].Name).IsEqualTo("C1");
        await Assert.That(dto.Children[1].Name).IsEqualTo("C2");
    }

    /// <summary>
    /// Tests mapping a deeply nested circular reference does not stack overflow.
    /// </summary>
    [Test]
    public async Task Map_DeeplyNestedCircularRef_DoesNotStackOverflow()
    {
        var parent = new Parent { Name = "Grandparent" };
        var child = new Child { Name = "Child", Parent = parent };
        parent.Children.Add(child);

        // Should complete without StackOverflowException
        var dto = _mapper.Map<ParentDto>(parent);

        await Assert.That(dto).IsNotNull();
    }

    /// <summary>
    /// Tests convention-based mapping of circular references does not throw.
    /// </summary>
    [Test]
    public async Task Map_ConventionBased_CircularRef_DoesNotThrow()
    {
        // No profile – purely convention-based
        var plain = new MappingConfigurationBuilder();
        var mapper = new ObjectMapper(plain.Build());

        var parent = new Parent { Name = "Conv" };
        parent.Children.Add(new Child { Name = "CConv", Parent = parent });

        // Convention mapper detects circular reference and breaks it
        var dto = mapper.Map<ParentDto>(parent);

        await Assert.That(dto).IsNotNull();
        await Assert.That(dto.Name).IsEqualTo("Conv");
    }
}
