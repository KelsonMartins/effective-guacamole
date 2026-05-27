namespace Guacamole.ObjectMapper.Abstract.Base;

/// <summary>
/// Non-generic base for a compiled type mapping configuration.
/// </summary>
public abstract class TypeMappingConfiguration
{
    /// <summary>The source type for this mapping.</summary>
    public abstract Type SourceType { get; }

    /// <summary>The destination type for this mapping.</summary>
    public abstract Type DestinationType { get; }

    /// <summary>Maps <paramref name="source"/> to a new destination instance.</summary>
    public abstract object MapObject(object source, MappingConfiguration config);
}
