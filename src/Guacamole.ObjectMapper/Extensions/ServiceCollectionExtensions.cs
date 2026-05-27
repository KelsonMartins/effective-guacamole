using System.Reflection;
using Guacamole.ObjectMapper.Abstract;
using Microsoft.Extensions.DependencyInjection;

namespace Guacamole.ObjectMapper.Extensions;

/// <summary>
/// Extension methods for registering <see cref="IObjectMapper"/> with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <see cref="IObjectMapper"/> with no profiles (convention mapping only).
        /// </summary>
        public IServiceCollection AddObjectMapper()
            => services.AddObjectMapper(Array.Empty<Assembly>());

        /// <summary>
        /// Scans <paramref name="assemblies"/> for <see cref="IMappingProfile"/> implementations
        /// and registers <see cref="IObjectMapper"/>.
        /// When <paramref name="assemblies"/> is empty, all loaded assemblies are scanned.
        /// </summary>
        public IServiceCollection AddObjectMapper(params Assembly[] assemblies)
        {
            services.AddSingleton(sp =>
            {
                var builder = new MappingConfigurationBuilder();
                ConfigureFromAssemblies(builder, assemblies, sp);
                return builder.Build();
            });
            services.AddScoped<IObjectMapper, ObjectMapper>();
            return services;
        }

        /// <summary>Registers <see cref="IObjectMapper"/> with the provided profiles.</summary>
        public IServiceCollection AddObjectMapper(params IMappingProfile[] profiles)
        {
            services.AddSingleton(_ =>
            {
                var builder = new MappingConfigurationBuilder();
                foreach (var profile in profiles)
                    profile.Configure(builder);
                return builder.Build();
            });
            services.AddScoped<IObjectMapper, ObjectMapper>();
            return services;
        }

        /// <summary>Registers <see cref="IObjectMapper"/> with an inline configuration action.</summary>
        public IServiceCollection AddObjectMapper(Action<IMappingConfigurationBuilder> configure)
        {
            services.AddSingleton(_ =>
            {
                var builder = new MappingConfigurationBuilder();
                configure(builder);
                return builder.Build();
            });
            services.AddScoped<IObjectMapper, ObjectMapper>();
            return services;
        }

        private static void ConfigureFromAssemblies(MappingConfigurationBuilder builder, Assembly[] assemblies, IServiceProvider serviceProvider)
        {
            var toScan = assemblies.Length > 0 ? assemblies : AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in toScan)
            {
                var profileTypes = assembly.GetTypes()
                    .Where(t => t.IsClass && !t.IsAbstract && typeof(IMappingProfile).IsAssignableFrom(t));

                foreach (var profileType in profileTypes)
                {
                    try
                    {
                        // Try to resolve the profile from DI first (in case it has dependencies), then fall back to Activator if not registered.
                        var profile = serviceProvider.GetService(profileType) as IMappingProfile
                            ?? (IMappingProfile)ActivatorUtilities.CreateInstance(serviceProvider, profileType);
                        profile.Configure(builder);
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Failed to instantiate mapping profile '{profileType.FullName}' from assembly '{assembly.FullName}'. See inner exception for details.", ex);
                    }
                }
            }
        }
    }
}
