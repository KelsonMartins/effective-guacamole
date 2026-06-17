using System.Linq.Expressions;
using System.Reflection;
using Guacamole.ObjectMapper.Abstract;
using Guacamole.ObjectMapper.Abstract.Base;

namespace Guacamole.ObjectMapper;

/// <summary>
/// Strongly-typed mapping configuration for the <typeparamref name="TSource"/> → <typeparamref name="TDestination"/> pair.
/// </summary>
public sealed class TypeMappingConfiguration<TSource, TDestination>
    : TypeMappingConfiguration, ITypeMappingConfiguration<TSource, TDestination>, IDisposable
    where TSource : class
    where TDestination : class
{
    private readonly List<MemberMapping> _memberMappings = [];
    private readonly HashSet<string> _ignoredMembers = [];
    private readonly MappingConfigurationBuilder _builder;
    private readonly ThreadLocal<HashSet<string>> _recursionTracker = new(() => new HashSet<string>());
    private bool _disposed;

    /// <inheritdoc />
    public override Type SourceType => typeof(TSource);

    /// <inheritdoc />
    public override Type DestinationType => typeof(TDestination);

    internal TypeMappingConfiguration(MappingConfigurationBuilder builder)
    {
        _builder = builder;
    }

    /// <inheritdoc />
    public ITypeMappingConfiguration<TSource, TDestination> ForMember<TMember>(
        Expression<Func<TDestination, TMember>> destinationMember,
        Expression<Func<TSource, TMember>> sourceMember)
    {
        _memberMappings.Add(new MemberMapping
        {
            DestinationMember = GetMemberName(destinationMember),
            SourceMemberName = GetMemberName(sourceMember),
            SourceExpression = sourceMember
        });
        return this;
    }

    /// <inheritdoc />
    public ITypeMappingConfiguration<TSource, TDestination> ForMember<TDestMember, TSrcMember>(
        Expression<Func<TDestination, TDestMember>> destinationMember,
        Func<TSource, TSrcMember> sourceConverter)
    {
        _memberMappings.Add(new MemberMapping
        {
            DestinationMember = GetMemberName(destinationMember),
            SourceConverter = source => sourceConverter((TSource)source)!
        });
        return this;
    }

    /// <inheritdoc />
    public ITypeMappingConfiguration<TSource, TDestination> Ignore<TMember>(
        Expression<Func<TDestination, TMember>> destinationMember)
    {
        _ignoredMembers.Add(GetMemberName(destinationMember));
        return this;
    }

    /// <inheritdoc />
    public ITypeMappingConfiguration<TDestination, TSource> ReverseMap()
        => _builder.CreateMap<TDestination, TSource>();

    /// <inheritdoc />
    public override object MapObject(object source, MappingConfiguration config)
    {
        if (source is not TSource typedSource)
            throw new ArgumentException($"Source must be of type '{typeof(TSource).Name}'.", nameof(source));

        if (HasParameterlessCtor(typeof(TDestination)))
        {
            var destination = (TDestination)Activator.CreateInstance(typeof(TDestination), nonPublic: true)!;
            MapToExisting(typedSource, destination, config);
            return destination;
        }

        return MapViaConstructor(typedSource, config);
    }

    /// <summary>Maps <paramref name="source"/> onto an existing <paramref name="destination"/> instance.</summary>
    public void MapToExisting(TSource source, TDestination destination, MappingConfiguration config)
    {
        foreach (var memberMapping in _memberMappings)
        {
            var destProperty = typeof(TDestination).GetProperty(memberMapping.DestinationMember);
            if (destProperty?.CanWrite != true)
                continue;

            object? value = null;

            if (memberMapping.SourceConverter != null)
            {
                value = memberMapping.SourceConverter(source);
            }
            else if (memberMapping.SourceExpression != null)
            {
                var compiled = memberMapping.CompiledExpression ??= memberMapping.SourceExpression.Compile();
                value = compiled.DynamicInvoke(source);
            }

            if (value == null)
                continue;

            if (destProperty.PropertyType.IsAssignableFrom(value.GetType()))
                destProperty.SetValue(destination, value);
            else
            {
                var mapped = MapComplexObject(value, destProperty.PropertyType, config);
                if (mapped != null)
                    destProperty.SetValue(destination, mapped);
            }
        }

        ApplyConventionMapping(source, destination, config);
    }

    private TDestination MapViaConstructor(TSource source, MappingConfiguration config)
    {
        var ctor = typeof(TDestination).GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"No public constructor found for type '{typeof(TDestination).Name}'.");

        var memberMappingsByDest = _memberMappings
            .ToDictionary(m => m.DestinationMember, StringComparer.OrdinalIgnoreCase);

        var sourceProps = typeof(TSource).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

        var args = ctor.GetParameters()
            .Select(param =>
            {
                var paramName = param.Name!;

                if (memberMappingsByDest.TryGetValue(paramName, out var memberMapping))
                {
                    if (memberMapping.SourceConverter != null)
                        return memberMapping.SourceConverter(source);

                    if (memberMapping.SourceExpression != null)
                    {
                        var compiled = memberMapping.CompiledExpression ??= memberMapping.SourceExpression.Compile();
                        return compiled.DynamicInvoke(source)
                            ?? (param.HasDefaultValue ? param.DefaultValue : GetDefaultValue(param.ParameterType));
                    }
                }

                if (!_ignoredMembers.Contains(paramName) && sourceProps.TryGetValue(paramName, out var sourceProp))
                {
                    var value = sourceProp.GetValue(source);
                    if (value != null)
                    {
                        if (param.ParameterType.IsAssignableFrom(sourceProp.PropertyType))
                            return value;

                        var mapped = MapComplexObject(value, param.ParameterType, config);
                        if (mapped != null)
                            return mapped;
                    }
                }

                return param.HasDefaultValue ? param.DefaultValue : GetDefaultValue(param.ParameterType);
            })
            .ToArray();

        var destination = (TDestination)ctor.Invoke(args);
        MapToExisting(source, destination, config);
        return destination;
    }

    private static bool HasParameterlessCtor(Type type)
        => type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
               .Any(c => c.GetParameters().Length == 0);

    private static object? GetDefaultValue(Type type)
        => type.IsValueType ? Activator.CreateInstance(type) : null;

    private void ApplyConventionMapping(TSource source, TDestination destination, MappingConfiguration config)
    {
        var sourceProps = typeof(TSource).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .ToDictionary(p => p.Name);

        var mappedMembers = _memberMappings.Select(m => m.DestinationMember).ToHashSet();

        foreach (var destProp in typeof(TDestination).GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.CanWrite))
        {
            if (mappedMembers.Contains(destProp.Name) || _ignoredMembers.Contains(destProp.Name))
                continue;

            if (!sourceProps.TryGetValue(destProp.Name, out var sourceProp))
                continue;

            var sourceValue = sourceProp.GetValue(source);
            if (sourceValue == null)
                continue;

            if (destProp.PropertyType.IsAssignableFrom(sourceProp.PropertyType))
                destProp.SetValue(destination, sourceValue);
            else
            {
                var mapped = MapComplexObject(sourceValue, destProp.PropertyType, config);
                if (mapped != null)
                    destProp.SetValue(destination, mapped);
            }
        }
    }

    private object? MapComplexObject(object source, Type destinationType, MappingConfiguration config)
    {
        var sourceType = source.GetType();
        var recursionKey = $"{sourceType.FullName}->{destinationType.FullName}";

        if (_recursionTracker.Value!.Contains(recursionKey))
            return null;

        if (destinationType.IsAssignableFrom(sourceType))
            return source;

        try
        {
            _recursionTracker.Value!.Add(recursionKey);

            if (config.HasMapping(sourceType, destinationType))
                return config.GetCompiledMapper(sourceType, destinationType)?.Invoke(source);

            if (IsCollectionTypeSafe(sourceType) && IsCollectionTypeSafe(destinationType))
                return MapCollection(source, destinationType, config);

            return null;
        }
        finally
        {
            _recursionTracker.Value!.Remove(recursionKey);
        }
    }

    private object? MapCollection(object source, Type destinationType, MappingConfiguration config)
    {
        if (source is not System.Collections.IEnumerable enumerable)
            return null;

        var elementType = GetCollectionElementType(destinationType);
        if (elementType == null || elementType == destinationType || elementType.IsGenericTypeDefinition)
            return null;

        try
        {
            var list = (System.Collections.IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;

            foreach (var item in enumerable)
            {
                if (item == null) continue;
                var itemType = item.GetType();

                if (elementType.IsAssignableFrom(itemType))
                    list.Add(item);
                else if (!itemType.IsGenericTypeDefinition)
                {
                    var mapped = MapComplexObject(item, elementType, config);
                    if (mapped != null)
                        list.Add(mapped);
                }
            }

            if (destinationType.IsAssignableFrom(list.GetType()))
                return list;

            try { return Activator.CreateInstance(destinationType, list); }
            catch { return list; }
        }
        catch
        {
            return null;
        }
    }

    private static string GetMemberName<T, TMember>(Expression<Func<T, TMember>> expression)
        => expression.Body switch
        {
            MemberExpression m => m.Member.Name,
            UnaryExpression { Operand: MemberExpression m2 } => m2.Member.Name,
            _ => throw new ArgumentException("Expression must be a member access.", nameof(expression))
        };

    private static bool IsCollectionTypeSafe(Type type)
    {
        try
        {
            return type != typeof(string) &&
                   (type.IsArray ||
                    (type.IsGenericType && !type.IsGenericTypeDefinition && typeof(System.Collections.IEnumerable).IsAssignableFrom(type)) ||
                    typeof(System.Collections.IEnumerable).IsAssignableFrom(type));
        }
        catch { return false; }
    }

    private static Type? GetCollectionElementType(Type collectionType)
    {
        if (collectionType.IsArray)
            return collectionType.GetElementType();

        if (collectionType.IsGenericType && !collectionType.IsGenericTypeDefinition)
        {
            var args = collectionType.GetGenericArguments();
            if (args.Length > 0 && args[0] != collectionType && args[0] != typeof(object) && !args[0].IsGenericTypeDefinition)
                return args[0];
        }

        var iface = collectionType.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && !i.IsGenericTypeDefinition && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));

        if (iface != null)
        {
            var t = iface.GetGenericArguments()[0];
            if (t != collectionType && !t.IsGenericTypeDefinition)
                return t;
        }

        return null;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _recursionTracker.Dispose();
        _disposed = true;
    }

    private sealed class MemberMapping
    {
        public required string DestinationMember { get; init; }
        public string? SourceMemberName { get; init; }
        public LambdaExpression? SourceExpression { get; init; }
        public Func<object, object>? SourceConverter { get; init; }
        public Delegate? CompiledExpression { get; set; }
    }
}
