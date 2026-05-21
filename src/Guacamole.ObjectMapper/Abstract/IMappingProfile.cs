namespace Guacamole.ObjectMapper.Abstract;

/// <summary>
/// Groups related mapping configurations. Implement this interface and call
/// <see cref="IMappingConfigurationBuilder.CreateMap{TSource,TDestination}"/> inside
/// <see cref="Configure"/> to register mappings.
/// </summary>
public interface IMappingProfile
{
    /// <summary>Configures mappings on the provided <paramref name="builder"/>.</summary>
    void Configure(IMappingConfigurationBuilder builder);
}
