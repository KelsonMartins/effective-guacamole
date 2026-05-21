namespace Guacamole.ObjectMapper.Abstract.Base;

/// <summary>
/// Base class for mapping profiles. Override <see cref="Configure"/> to define type maps.
/// </summary>
public abstract class MappingProfile : IMappingProfile
{
    /// <inheritdoc />
    public abstract void Configure(IMappingConfigurationBuilder builder);
}
