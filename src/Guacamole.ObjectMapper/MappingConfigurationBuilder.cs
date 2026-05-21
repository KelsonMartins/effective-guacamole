using Guacamole.ObjectMapper.Abstract;

namespace Guacamole.ObjectMapper;

/// <summary>
/// Default <see cref="IMappingConfigurationBuilder"/> implementation.
/// Use <see cref="Build"/> after all <see cref="CreateMap{TSource,TDestination}"/> calls
/// to obtain a compiled <see cref="MappingConfiguration"/>.
/// </summary>
public sealed class MappingConfigurationBuilder : IMappingConfigurationBuilder
{
    private readonly MappingConfiguration _configuration = new();

    /// <inheritdoc />
    public ITypeMappingConfiguration<TSource, TDestination> CreateMap<TSource, TDestination>()
        where TSource : class
        where TDestination : class
    {
        var typeMapping = new TypeMappingConfiguration<TSource, TDestination>(this);
        _configuration.AddTypeMapping(typeMapping);
        return typeMapping;
    }

    /// <summary>
    /// Compiles all registered type maps into fast delegate-based mappers and returns
    /// the immutable <see cref="MappingConfiguration"/>.
    /// </summary>
    public MappingConfiguration Build()
    {
        foreach (var kvp in _configuration.GetAllTypeMappings())
        {
            var (sourceType, destinationType) = kvp.Key;
            var typeMapping = kvp.Value;
            Func<object, object> compiled = source => typeMapping.MapObject(source, _configuration);
            _configuration.SetCompiledMapper(sourceType, destinationType, compiled);
        }

        return _configuration;
    }
}
