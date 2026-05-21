namespace Guacamole.ObjectMapper.Abstract;

/// <summary>
/// Fluent builder for creating type-to-type mapping configurations.
/// </summary>
public interface IMappingConfigurationBuilder
{
    /// <summary>
    /// Creates a mapping configuration between <typeparamref name="TSource"/> and <typeparamref name="TDestination"/>.
    /// </summary>
    ITypeMappingConfiguration<TSource, TDestination> CreateMap<TSource, TDestination>()
        where TSource : class
        where TDestination : class;
}
