namespace PatternsLab.Patterns.Creational;

// ---------------------------------------------------------------------------
// SINGLETON — handbook §1.4
//
// The classic implementation combines two UNRELATED responsibilities: being
// unique, and being reachable from anywhere. The second one is what makes it
// a code smell — it hides a dependency and destroys testability.
//
// In .NET, let the container own uniqueness and keep the dependency explicit.
// ---------------------------------------------------------------------------

/// <summary>The textbook version, thread-safe via Lazy — and still the wrong answer.</summary>
public sealed class HandRolledConfigCache
{
    private static readonly Lazy<HandRolledConfigCache> Instance_ = new(() => new HandRolledConfigCache());

    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    private HandRolledConfigCache() { }

    public static HandRolledConfigCache Instance => Instance_.Value;

    public void Set(string key, string value) => _values[key] = value;

    public string? Get(string key) => _values.GetValueOrDefault(key);
}

/// <summary>The version you should write: an interface, one instance, injected.</summary>
public interface IConfigCache
{
    void Set(string key, string value);
    string? Get(string key);
}

public sealed class ConfigCache : IConfigCache
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public void Set(string key, string value) => _values[key] = value;

    public string? Get(string key) => _values.GetValueOrDefault(key);
}

/// <summary>Takes its dependency, so a test can hand it a fake.</summary>
public sealed class PriceLookup(IConfigCache cache)
{
    public string Describe(string key) => cache.Get(key) ?? "(unset)";
}

public sealed class SingletonDemo : PatternDemo
{
    public override string Key => "singleton";
    public override string Name => "Singleton";
    public override PatternCategory Category => PatternCategory.Creational;
    public override string Intent => "Guarantee one instance and a global access point to it.";
    public override string HandbookSection => "1.4";
    public override string SeenInDotNet => "services.AddSingleton<T>() — the safe version";

    protected override void Execute(Action<string> log)
    {
        HandRolledConfigCache.Instance.Set("default-currency", "USD");
        log($"Hand-rolled: HandRolledConfigCache.Instance is the same object every time " +
            $"({ReferenceEquals(HandRolledConfigCache.Instance, HandRolledConfigCache.Instance)}).");
        log("  …but every caller is now welded to that concrete type. No test can substitute it.");

        IConfigCache injected = new ConfigCache();
        injected.Set("default-currency", "ILS");
        log($"Container-managed: PriceLookup(cache).Describe(\"default-currency\") = " +
            $"{new PriceLookup(injected).Describe("default-currency")}");
        log("  Uniqueness is a REGISTRATION detail (AddSingleton), not a property of the class.");

        log("WARNING (handbook §1.4): a singleton that injects a scoped DbContext, repository or " +
            "unit of work captures it for the lifetime of the app. Inject IServiceScopeFactory instead.");
    }
}
