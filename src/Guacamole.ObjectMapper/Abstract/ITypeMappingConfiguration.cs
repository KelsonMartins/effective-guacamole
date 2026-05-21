using System.Linq.Expressions;

namespace Guacamole.ObjectMapper.Abstract;

/// <summary>
/// Fluent per-pair mapping configuration.
/// </summary>
public interface ITypeMappingConfiguration<TSource, TDestination>
    where TSource : class
    where TDestination : class
{
    /// <summary>Maps a destination property from a source property of the same type.</summary>
    ITypeMappingConfiguration<TSource, TDestination> ForMember<TMember>(
        Expression<Func<TDestination, TMember>> destinationMember,
        Expression<Func<TSource, TMember>> sourceMember);

    /// <summary>Maps a destination property using a custom conversion function.</summary>
    ITypeMappingConfiguration<TSource, TDestination> ForMember<TDestMember, TSrcMember>(
        Expression<Func<TDestination, TDestMember>> destinationMember,
        Func<TSource, TSrcMember> sourceConverter);

    /// <summary>Excludes a destination property from mapping.</summary>
    ITypeMappingConfiguration<TSource, TDestination> Ignore<TMember>(
        Expression<Func<TDestination, TMember>> destinationMember);

    /// <summary>Creates a reverse mapping from <typeparamref name="TDestination"/> back to <typeparamref name="TSource"/>.</summary>
    ITypeMappingConfiguration<TDestination, TSource> ReverseMap();
}
