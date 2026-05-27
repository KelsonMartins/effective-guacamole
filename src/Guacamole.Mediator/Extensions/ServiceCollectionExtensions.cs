using System.Reflection;
using Guacamole.Mediator.Abstract;
using Microsoft.Extensions.DependencyInjection;

namespace Guacamole.Mediator.Extensions;

/// <summary>
/// Extension methods for registering the mediator with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>
        /// Registers <see cref="IMediator"/> and scans <paramref name="assembly"/> for all
        /// <see cref="IRequestHandler{TRequest,TResponse}"/> and <see cref="IRequestHandler{TRequest}"/>
        /// implementations, registering them as scoped services.
        /// </summary>
        public IServiceCollection AddMediator(Assembly assembly)
        {
            services.AddScoped<IMediator, Sender>();

            var handlerInterface = typeof(IRequestHandler<,>);
            var handlers = assembly.GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .SelectMany(t => t.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface)
                    .Select(i => new { Interface = i, Implementation = t }));

            foreach (var h in handlers)
                services.AddScoped(h.Interface, h.Implementation);

            var singleHandlerInterface = typeof(IRequestHandler<>);
            var singleHandlers = assembly.GetTypes()
                .Where(t => !t.IsAbstract && !t.IsInterface)
                .SelectMany(t => t.GetInterfaces()
                    .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == singleHandlerInterface)
                    .Select(i => new { Interface = i, Implementation = t }));

            foreach (var h in singleHandlers)
                services.AddScoped(h.Interface, h.Implementation);

            return services;
        }

        /// <summary>
        /// Registers <see cref="IMediator"/> and scans multiple assemblies for all handler implementations.
        /// </summary>
        public IServiceCollection AddMediator(params Assembly[] assemblies)
        {
            services.AddScoped<IMediator, Sender>();

            foreach (var assembly in assemblies)
            {
                var handlerInterface = typeof(IRequestHandler<,>);
                var handlers = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface)
                    .SelectMany(t => t.GetInterfaces()
                        .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface)
                        .Select(i => new { Interface = i, Implementation = t }));

                foreach (var h in handlers)
                    services.AddScoped(h.Interface, h.Implementation);

                var singleHandlerInterface = typeof(IRequestHandler<>);
                var singleHandlers = assembly.GetTypes()
                    .Where(t => !t.IsAbstract && !t.IsInterface)
                    .SelectMany(t => t.GetInterfaces()
                        .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == singleHandlerInterface)
                        .Select(i => new { Interface = i, Implementation = t }));

                foreach (var h in singleHandlers)
                    services.AddScoped(h.Interface, h.Implementation);
            }

            return services;
        }
    }
}
