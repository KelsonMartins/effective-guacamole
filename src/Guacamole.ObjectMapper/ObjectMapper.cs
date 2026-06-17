using System.Collections.Concurrent;
using System.Reflection;
using Guacamole.ObjectMapper.Abstract;

namespace Guacamole.ObjectMapper;

/// <summary>
/// High-performance object mapper with compiled expression caching, convention-based mapping,
/// circular-reference detection, and support for paged/collection types.
/// </summary>
/// <param name="configuration">The compiled mapping configuration produced by <see cref="MappingConfigurationBuilder.Build"/>.</param>
public sealed class ObjectMapper(MappingConfiguration configuration) : IObjectMapper
{
    private readonly MappingConfiguration _configuration = configuration;
    private readonly ConcurrentDictionary<(Type, Type), Func<object, object>> _compiledMappers = new();

    /// <inheritdoc />
    public TDestination Map<TDestination>(object? source) where TDestination : class
    {
        if (source == null)
            return (TDestination)CreateDefault(typeof(TDestination));

        return (TDestination)MapInternal(source, typeof(TDestination), new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    /// <inheritdoc />
    public TDestination Map<TSource, TDestination>(TSource? source)
        where TSource : class
        where TDestination : class
    {
        if (source == null)
            return (TDestination)CreateDefault(typeof(TDestination));

        return (TDestination)MapInternal(source, typeof(TDestination), new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    /// <inheritdoc />
    public void Map<TSource, TDestination>(TSource? source, TDestination destination)
        where TSource : class
        where TDestination : class
    {
        if (source == null || destination == null)
            return;

        var typeMapping = _configuration.GetTypeMapping<TSource, TDestination>();
        if (typeMapping != null)
            typeMapping.MapToExisting(source, destination, _configuration);
        else
            MapByConvention(source, destination, new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    /// <inheritdoc />
    public IEnumerable<TDestination> Map<TSource, TDestination>(IEnumerable<TSource>? source)
        where TSource : class
        where TDestination : class
    {
        if (source == null)
            return [];

        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        return source.Select(item => (TDestination)MapInternal(item, typeof(TDestination), visited));
    }

    /// <inheritdoc />
    public TSource ReverseMap<TSource, TDestination>(TDestination? destination)
        where TSource : class
        where TDestination : class
    {
        if (destination == null)
            return (TSource)CreateDefault(typeof(TSource));

        return (TSource)MapInternal(destination, typeof(TSource), new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    private object MapInternal(object source, Type destinationType, HashSet<object> visited)
    {
        if (visited.Contains(source))
            return CreateDefault(destinationType);

        visited.Add(source);
        try
        {
            var sourceType = source.GetType();
            var key = (sourceType, destinationType);

            if (_compiledMappers.TryGetValue(key, out var cached))
                return cached(source);

            var compiled = _configuration.GetCompiledMapper(sourceType, destinationType);
            if (compiled != null)
            {
                _compiledMappers[key] = compiled;
                return compiled(source);
            }

            if (HandleSpecialMappings(source, sourceType, destinationType, visited, out var special))
                return special;

            return MapByConvention(source, destinationType, visited);
        }
        finally
        {
            visited.Remove(source);
        }
    }

    private bool HandleSpecialMappings(object source, Type sourceType, Type destinationType, HashSet<object> visited, out object result)
    {
        result = null!;

        if (IsPagedListType(sourceType) && IsPagedListType(destinationType))
        {
            result = MapPagedList(source, sourceType, destinationType, visited);
            return true;
        }

        if (IsCollectionType(sourceType) && IsCollectionType(destinationType))
        {
            var mapped = MapCollection(source, sourceType, destinationType, visited);
            if (mapped != null) { result = mapped; return true; }
        }

        return false;
    }

    private object MapPagedList(object source, Type sourceType, Type destinationType, HashSet<object> visited)
    {
        var sourceElementType = sourceType.GetGenericArguments()[0];
        var destElementType = destinationType.GetGenericArguments()[0];
        var sourceList = (System.Collections.IList)source;

        var mappedItems = sourceList.Cast<object>()
            .Where(item => item != null)
            .Select(item => MapInternal(item, destElementType, visited))
            .ToList();

        var dest = Activator.CreateInstance(destinationType)!;
        CopyPaginationProperties(source, dest, sourceType, destinationType);

        var destList = (System.Collections.IList)dest;
        foreach (var item in mappedItems)
            destList.Add(item);

        return dest;
    }

    private static void CopyPaginationProperties(object source, object dest, Type sourceType, Type destType)
    {
        foreach (var propName in new[] { "CurrentPage", "TotalPages", "PageSize", "TotalCount", "PageNumber" })
        {
            var src = sourceType.GetProperty(propName);
            var dst = destType.GetProperty(propName);
            if (src?.CanRead == true && dst?.CanWrite == true && dst.PropertyType.IsAssignableFrom(src.PropertyType))
                dst.SetValue(dest, src.GetValue(source));
        }
    }

    private object? MapCollection(object source, Type sourceType, Type destinationType, HashSet<object> visited)
    {
        if (source is not System.Collections.IEnumerable enumerable)
            return null;

        var destElementType = GetCollectionElementType(destinationType);
        if (destElementType == null)
            return null;

        var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(destElementType))!;

        foreach (var item in enumerable)
            if (item != null)
                list.Add(MapInternal(item, destElementType, visited));

        if (destinationType.IsAssignableFrom(list.GetType()))
            return list;

        try { return Activator.CreateInstance(destinationType, list); }
        catch { return list; }
    }

    private object MapByConvention(object source, Type destinationType, HashSet<object> visited)
    {
        if (destinationType == typeof(string) || destinationType.IsPrimitive || destinationType.IsValueType)
            throw new InvalidOperationException(
                $"Cannot map to value type or string '{destinationType.Name}' using convention mapping. Register an explicit mapping profile.");

        if (HasParameterlessCtor(destinationType))
        {
            var destination = Activator.CreateInstance(destinationType, nonPublic: true)!;
            MapByConvention(source, destination, visited);
            return destination;
        }

        return MapByConstructor(source, destinationType, visited);
    }

    private object MapByConstructor(object source, Type destinationType, HashSet<object> visited)
    {
        var ctor = destinationType.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"No public constructor found for type '{destinationType.Name}'.");

        var sourceProps = source.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        var args = ctor.GetParameters()
            .Select(param =>
            {
                if (sourceProps.TryGetValue(param.Name!, out var sourceProp))
                {
                    var value = sourceProp.GetValue(source);
                    if (value != null)
                    {
                        if (param.ParameterType.IsAssignableFrom(sourceProp.PropertyType))
                            return value;

                        try { return MapInternal(value, param.ParameterType, visited); }
                        catch (InvalidOperationException ex)
                        {
                            throw new InvalidOperationException(
                                $"Failed to map constructor parameter '{param.Name}' of type '{param.ParameterType.Name}' " +
                                $"on '{destinationType.Name}'.", ex);
                        }
                    }
                }

                return param.HasDefaultValue
                    ? param.DefaultValue
                    : GetDefaultValue(param.ParameterType);
            })
            .ToArray();

        var destination = ctor.Invoke(args);
        MapByConvention(source, destination, visited);

        return destination;
    }

    private void MapByConvention(object source, object destination, HashSet<object> visited)
    {
        var sourceType = source.GetType();
        var destType = destination.GetType();

        var sourceProps = sourceType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var destProp in destType.GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite))
        {
            if (!sourceProps.TryGetValue(destProp.Name, out var sourceProp))
                continue;

            var value = sourceProp.GetValue(source);
            if (value == null)
                continue;

            if (destProp.PropertyType.IsAssignableFrom(sourceProp.PropertyType))
            {
                destProp.SetValue(destination, value);
            }
            else if (destProp.PropertyType.IsEnum)
            {
                try
                {
                    destProp.SetValue(destination, Enum.ToObject(destProp.PropertyType, Convert.ToInt32(value)));
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to convert enum for property '{destProp.Name}'.", ex);
                }
            }
            else if (IsValueTypeConvertible(sourceProp.PropertyType, destProp.PropertyType))
            {
                try { destProp.SetValue(destination, Convert.ChangeType(value, destProp.PropertyType)); }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to convert '{sourceProp.PropertyType.Name}' to '{destProp.PropertyType.Name}' for property '{destProp.Name}'.", ex);
                }
            }
            else if (destProp.PropertyType == typeof(string))
            {
                var nameProperty = value.GetType().GetProperty("Name");
                var str = nameProperty?.CanRead == true && nameProperty.PropertyType == typeof(string)
                    ? nameProperty.GetValue(value) as string ?? string.Empty
                    : value.ToString() ?? string.Empty;
                destProp.SetValue(destination, str);
            }
            else
            {
                try
                {
                    destProp.SetValue(destination, MapInternal(value, destProp.PropertyType, visited));
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"Failed to map property '{destProp.Name}' from '{sourceType.Name}' to '{destType.Name}'.", ex);
                }
            }
        }
    }

    private static bool IsPagedListType(Type type)
        => type.IsGenericType &&
           (type.GetGenericTypeDefinition().Name.Contains("PagedList") ||
            type.GetGenericTypeDefinition().Name.Contains("PaginatedList"));

    private static bool IsCollectionType(Type type)
        => type != typeof(string) &&
           (type.IsArray ||
            (type.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(type)) ||
            typeof(System.Collections.IEnumerable).IsAssignableFrom(type));

    private static Type? GetCollectionElementType(Type type)
    {
        if (type.IsArray) return type.GetElementType();
        if (type.IsGenericType)
        {
            var args = type.GetGenericArguments();
            if (args.Length > 0) return args[0];
        }
        return type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            ?.GetGenericArguments()[0];
    }

    private static bool IsValueTypeConvertible(Type src, Type dst)
    {
        static Type Unwrap(Type t) => Nullable.GetUnderlyingType(t) ?? t;
        var s = Unwrap(src); var d = Unwrap(dst);
        var primitives = new[] { typeof(string), typeof(decimal), typeof(DateTime), typeof(DateTimeOffset), typeof(Guid) };
        return (s.IsPrimitive || primitives.Contains(s)) && (d.IsPrimitive || primitives.Contains(d));
    }

    private static bool HasParameterlessCtor(Type type)
        => type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
               .Any(c => c.GetParameters().Length == 0);

    private static object CreateDefault(Type type)
    {
        if (HasParameterlessCtor(type))
            return Activator.CreateInstance(type, nonPublic: true)!;

        var ctor = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault()
            ?? throw new InvalidOperationException($"No public constructor found for type '{type.Name}'.");

        var args = ctor.GetParameters()
            .Select(p => p.HasDefaultValue ? p.DefaultValue : GetDefaultValue(p.ParameterType))
            .ToArray();

        return ctor.Invoke(args);
    }

    private static object? GetDefaultValue(Type type)
        => type.IsValueType ? Activator.CreateInstance(type) : null;
}
