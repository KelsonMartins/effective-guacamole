using Guacamole.ObjectMapper.Abstract;
using Guacamole.ObjectMapper.Abstract.Base;
using Guacamole.ObjectMapper.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Guacamole.ObjectMapper.Tests;

// ---------- test profile ----------

public class SimpleProfile : MappingProfile
{
    public override void Configure(IMappingConfigurationBuilder builder)
    {
        builder.CreateMap<Source, Destination>();
    }
}

// ---------- tests ----------

public class ServiceCollectionExtensionsTests : IDisposable
{
    private ServiceProvider? _provider;

    public void Dispose() => _provider?.Dispose();

    [Test]
    public async Task AddObjectMapper_NoArgs_RegistersMapper()
    {
        var services = new ServiceCollection();
        services.AddObjectMapper();
        _provider = services.BuildServiceProvider();

        var mapper = _provider.GetService<IObjectMapper>();

        await Assert.That(mapper).IsNotNull();
    }

    [Test]
    public async Task AddObjectMapper_WithExplicitProfile_MapsCorrectly()
    {
        var services = new ServiceCollection();
        services.AddObjectMapper(new SimpleProfile());
        _provider = services.BuildServiceProvider();

        var mapper = _provider.GetRequiredService<IObjectMapper>();
        var dst = mapper.Map<Destination>(new Source { Name = "DI", Age = 5 });

        await Assert.That(dst.Name).IsEqualTo("DI");
        await Assert.That(dst.Age).IsEqualTo(5);
    }

    [Test]
    public async Task AddObjectMapper_WithAssemblyScan_DiscoverProfile()
    {
        var services = new ServiceCollection();
        services.AddObjectMapper(typeof(SimpleProfile).Assembly);
        _provider = services.BuildServiceProvider();

        var mapper = _provider.GetRequiredService<IObjectMapper>();
        var dst = mapper.Map<Destination>(new Source { Name = "Scanned" });

        await Assert.That(dst.Name).IsEqualTo("Scanned");
    }

    [Test]
    public async Task AddObjectMapper_WithInlineConfig_MapsCorrectly()
    {
        var services = new ServiceCollection();
        services.AddObjectMapper(cfg => cfg.CreateMap<Source, Destination>());
        _provider = services.BuildServiceProvider();

        var mapper = _provider.GetRequiredService<IObjectMapper>();
        var dst = mapper.Map<Destination>(new Source { Name = "Inline", Age = 7 });

        await Assert.That(dst.Name).IsEqualTo("Inline");
        await Assert.That(dst.Age).IsEqualTo(7);
    }

    [Test]
    public async Task IObjectMapper_IsRegisteredAsScoped()
    {
        var services = new ServiceCollection();
        services.AddObjectMapper();
        _provider = services.BuildServiceProvider();

        using var scope1 = _provider.CreateScope();
        using var scope2 = _provider.CreateScope();

        var m1 = scope1.ServiceProvider.GetRequiredService<IObjectMapper>();
        var m2 = scope2.ServiceProvider.GetRequiredService<IObjectMapper>();

        await Assert.That(m1).IsNotNull();
        await Assert.That(m2).IsNotNull();
        // Different scopes = different instances
        await Assert.That(ReferenceEquals(m1, m2)).IsEqualTo(false);
    }
}
