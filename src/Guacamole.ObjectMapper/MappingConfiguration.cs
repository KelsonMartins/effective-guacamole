using System.Collections.Concurrent;
using Guacamole.ObjectMapper.Abstract.Base;

namespace Guacamole.ObjectMapper;

/// <summary>
/// Holds all compiled type mappings. Built by <see cref="MappingConfigurationBuilder"/>.
/// </summary>
public sealed class MappingConfiguration
{
    private readonly ConcurrentDictionary<(Type source, Type destination), TypeMappingConfiguration> _typeMappings = new();
    private readonly ConcurrentDictionary<(Type source, Type destination), Func<object, object>> _compiledMappers = new();

    internal void AddTypeMapping<TSource, TDestination>(TypeMappingConfiguration<TSource, TDestination> mapping)
        where TSource : class
        where TDestination : class
        => _typeMappings[(typeof(TSource), typeof(TDestination))] = mapping;

    /// <summary>Returns the registered typed mapping, or <c>null</c> if none.</summary>
    public TypeMappingConfiguration<TSource, TDestination>? GetTypeMapping<TSource, TDestination>()
        where TSource : class
        where TDestination : class
        => _typeMappings.TryGetValue((typeof(TSource), typeof(TDestination)), out var m)
            ? m as TypeMappingConfiguration<TSource, TDestination>
            : null;

    /// <summary>Returns a compiled mapper function for the given type pair, or <c>null</c> if none.</summary>
    public Func<object, object>? GetCompiledMapper(Type sourceType, Type destinationType)
        => _compiledMappers.TryGetValue((sourceType, destinationType), out var mapper) ? mapper : null;

    internal void SetCompiledMapper(Type sourceType, Type destinationType, Func<object, object> mapper)
        => _compiledMappers[(sourceType, destinationType)] = mapper;

    /// <summary>Returns <c>true</c> if a type mapping is registered for the given pair.</summary>
    public bool HasMapping(Type sourceType, Type destinationType)
        => _typeMappings.ContainsKey((sourceType, destinationType));

    internal IEnumerable<KeyValuePair<(Type source, Type destination), TypeMappingConfiguration>> GetAllTypeMappings()
        => _typeMappings;
}
