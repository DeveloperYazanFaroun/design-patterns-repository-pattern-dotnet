using Microsoft.Extensions.DependencyInjection;

namespace PatternsLab.Infrastructure.DependencyInjection;

/// <summary>
/// A 30-line decorator registration, written out rather than taken from Scrutor so you
/// can see there is no magic in it (handbook §1.5).
///
/// It finds the existing registration for <typeparamref name="TService"/>, replaces it
/// with a factory that builds the decorator, and hands the decorator the original
/// implementation as its inner instance. Registrations read outside-in: the LAST
/// Decorate call is the outermost wrapper.
/// </summary>
public static class ServiceCollectionDecoratorExtensions
{
    public static IServiceCollection Decorate<TService, TDecorator>(this IServiceCollection services)
        where TService : class
        where TDecorator : class, TService
    {
        var descriptor = services.LastOrDefault(d => d.ServiceType == typeof(TService))
            ?? throw new InvalidOperationException(
                $"Cannot decorate {typeof(TService).Name}: it has not been registered yet. " +
                "Register the concrete implementation before calling Decorate.");

        var index = services.IndexOf(descriptor);

        services[index] = ServiceDescriptor.Describe(
            typeof(TService),
            provider =>
            {
                var inner = (TService)CreateInstance(provider, descriptor);
                return ActivatorUtilities.CreateInstance<TDecorator>(provider, inner);
            },
            descriptor.Lifetime);

        return services;
    }

    private static object CreateInstance(IServiceProvider provider, ServiceDescriptor descriptor)
    {
        if (descriptor.ImplementationInstance is not null)
            return descriptor.ImplementationInstance;

        if (descriptor.ImplementationFactory is not null)
            return descriptor.ImplementationFactory(provider);

        return ActivatorUtilities.GetServiceOrCreateInstance(provider, descriptor.ImplementationType!);
    }
}
