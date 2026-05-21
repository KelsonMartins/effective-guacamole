namespace Guacamole.ObjectMapper.Abstract;

/// <summary>
/// Core interface for object-to-object mapping operations.
/// </summary>
public interface IObjectMapper
{
    /// <summary>Maps <paramref name="source"/> to a new instance of <typeparamref name="TDestination"/>.</summary>
    TDestination Map<TDestination>(object? source) where TDestination : class;

    /// <summary>Maps <paramref name="source"/> to a new instance of <typeparamref name="TDestination"/>.</summary>
    TDestination Map<TSource, TDestination>(TSource? source)
        where TSource : class
        where TDestination : class;

    /// <summary>Maps <paramref name="source"/> onto an existing <paramref name="destination"/> instance.</summary>
    void Map<TSource, TDestination>(TSource? source, TDestination destination)
        where TSource : class
        where TDestination : class;

    /// <summary>Maps each element of <paramref name="source"/> to a new instance of <typeparamref name="TDestination"/>.</summary>
    IEnumerable<TDestination> Map<TSource, TDestination>(IEnumerable<TSource>? source)
        where TSource : class
        where TDestination : class;

    /// <summary>Reverse-maps <paramref name="destination"/> back to <typeparamref name="TSource"/>.</summary>
    TSource ReverseMap<TSource, TDestination>(TDestination? destination)
        where TSource : class
        where TDestination : class;
}
