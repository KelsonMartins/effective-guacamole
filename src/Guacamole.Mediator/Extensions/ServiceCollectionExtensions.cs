using System.Reflection;
using Guacamole.Mediator.Abstract;
using Microsoft.Extensions.DependencyInjection;

namespace Guacamole.Mediator.Extensions;

/// <summary>
/// Extension methods for registering the mediator with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IMediator"/> and scans <paramref name="assembly"/> for all
    /// <see cref="IRequestHandler{TRequest,TResponse}"/> and <see cref="IRequestHandler{TRequest}"/>
    /// implementations, registering them as scoped services.
    /// </summary>
    public static IServiceCollection AddMediator(this IServiceCollection services, Assembly assembly)
    {
        services.AddScoped<IMediator, Sender>();

        RegisterHandlers(services, assembly, typeof(IRequestHandler<,>));
        RegisterHandlers(services, assembly, typeof(IRequestHandler<>));

        return services;
    }

    /// <summary>
    /// Registers <see cref="IMediator"/> and scans multiple assemblies for all handler implementations.
    /// </summary>
    public static IServiceCollection AddMediator(this IServiceCollection services, params Assembly[] assemblies)
    {
        services.AddScoped<IMediator, Sender>();

        foreach (var assembly in assemblies)
        {
            RegisterHandlers(services, assembly, typeof(IRequestHandler<,>));
            RegisterHandlers(services, assembly, typeof(IRequestHandler<>));
        }

        return services;
    }

    private static void RegisterHandlers(IServiceCollection services, Assembly assembly, Type handlerInterface)
    {
        var handlers = assembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface)
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface)
                .Select(i => new { Interface = i, Implementation = t }));

        foreach (var h in handlers)
            services.AddScoped(h.Interface, h.Implementation);
    }
}
