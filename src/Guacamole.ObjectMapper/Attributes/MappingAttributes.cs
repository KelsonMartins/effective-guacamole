namespace Guacamole.ObjectMapper.Attributes;

/// <summary>Marks a class for automatic mapping to another type.</summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public sealed class MapToAttribute : Attribute
{
    /// <summary>The target type to map to.</summary>
    public Type TargetType { get; }

    /// <summary>When <c>true</c>, generates a reverse mapping as well.</summary>
    public bool ReverseMap { get; set; } = false;

    /// <param name="targetType">The type to map this class to.</param>
    public MapToAttribute(Type targetType)
    {
        TargetType = targetType;
    }
}

/// <summary>Overrides the source property used when mapping this property by convention.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class MapFromAttribute : Attribute
{
    /// <summary>The source property name to read from.</summary>
    public string SourceProperty { get; }

    /// <param name="sourceProperty">The source property name.</param>
    public MapFromAttribute(string sourceProperty)
    {
        SourceProperty = sourceProperty;
    }
}

/// <summary>Excludes this property from convention-based mapping.</summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class IgnoreMapAttribute : Attribute { }
