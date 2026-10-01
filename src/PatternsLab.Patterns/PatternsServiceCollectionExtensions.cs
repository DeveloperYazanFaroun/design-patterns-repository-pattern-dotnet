using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace PatternsLab.Patterns;

public static class PatternsServiceCollectionExtensions
{
    /// <summary>
    /// Registers every <see cref="IPatternDemo"/> in this assembly by reflection.
    ///
    /// Adding a 24th pattern means adding one class. Nothing here changes — which is
    /// itself the Open/Closed Principle the catalogue is teaching.
    /// </summary>
    public static IServiceCollection AddPatternCatalogue(this IServiceCollection services)
    {
        var demoTypes = typeof(PatternsServiceCollectionExtensions).Assembly
            .GetTypes()
            .Where(t => t is { IsAbstract: false, IsInterface: false, IsGenericTypeDefinition: false })
            .Where(t => typeof(IPatternDemo).IsAssignableFrom(t))
            .Where(t => t.GetConstructor(Type.EmptyTypes) is not null);

        foreach (var type in demoTypes)
            services.AddSingleton(typeof(IPatternDemo), type);

        services.AddSingleton<PatternCatalogue>();

        return services;
    }

    /// <summary>Used by the tests to assert that all 23 GoF patterns are present.</summary>
    public static IReadOnlyList<Type> DiscoverDemoTypes() =>
        Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => t is { IsAbstract: false, IsInterface: false } && typeof(IPatternDemo).IsAssignableFrom(t))
                .OrderBy(t => t.Name, StringComparer.Ordinal)
                .ToList();
}
