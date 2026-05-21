using Guacamole.ObjectMapper.Abstract;
using Guacamole.ObjectMapper.Abstract.Base;

namespace Guacamole.ObjectMapper.Tests;

public class CollectionMappingTests
{
    private readonly ObjectMapper _mapper;

    public CollectionMappingTests()
    {
        var builder = new MappingConfigurationBuilder();
        builder.CreateMap<Source, Destination>();
        builder.CreateMap<Tag, TagDto>();
        _mapper = new ObjectMapper(builder.Build());
    }

    [Test]
    public async Task Map_ListOfSources_ReturnsListOfDestinations()
    {
        var sources = new List<Source>
        {
            new() { Name = "X", Age = 1 },
            new() { Name = "Y", Age = 2 },
            new() { Name = "Z", Age = 3 }
        };

        var dests = _mapper.Map<Source, Destination>(sources).ToList();

        await Assert.That(dests.Count).IsEqualTo(3);
        await Assert.That(dests[2].Name).IsEqualTo("Z");
    }

    [Test]
    public async Task Map_NullCollection_ReturnsEmpty()
    {
        var result = _mapper.Map<Source, Destination>((IEnumerable<Source>?)null).ToList();

        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Map_EmptyCollection_ReturnsEmpty()
    {
        var result = _mapper.Map<Source, Destination>(new List<Source>()).ToList();

        await Assert.That(result.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Map_NestedCollectionProperty_MapsEachElement()
    {
        var src = new Source
        {
            Tags = [new Tag { Value = "alpha" }, new Tag { Value = "beta" }]
        };

        var dst = _mapper.Map<Destination>(src);

        await Assert.That(dst.Tags.Count).IsEqualTo(2);
        await Assert.That(dst.Tags[0].Value).IsEqualTo("alpha");
        await Assert.That(dst.Tags[1].Value).IsEqualTo("beta");
    }

    [Test]
    public async Task Map_ArraySource_MapsToListDestination()
    {
        var sources = new Source[] { new() { Name = "arr" } };

        var dests = _mapper.Map<Source, Destination>(sources).ToList();

        await Assert.That(dests.Count).IsEqualTo(1);
        await Assert.That(dests[0].Name).IsEqualTo("arr");
    }
}
