namespace PatternsLab.Patterns.Structural;

// ---------------------------------------------------------------------------
// DECORATOR — handbook §1.5, Figure 1.3
//
// Add behaviour by WRAPPING, never by editing. Each wrapper implements the same
// interface and holds an inner instance of it, so the caller cannot tell how deep
// the stack goes — and order becomes configuration rather than code.
//
// The production version of this is in
// PatternsLab.Infrastructure/Repositories/Decorators.
// ---------------------------------------------------------------------------

public interface IClaimReader
{
    string Read(Guid id);
}

/// <summary>The real one. It is never edited again after this file is written.</summary>
public sealed class DatabaseClaimReader : IClaimReader
{
    public int Reads { get; private set; }

    public string Read(Guid id)
    {
        Reads++;
        Thread.Sleep(1);                     // stand-in for a round trip
        return $"claim:{id.ToString()[..8]}";
    }
}

public sealed class CachingClaimReader(IClaimReader inner) : IClaimReader
{
    private readonly Dictionary<Guid, string> _cache = [];

    public List<string> Trace { get; } = [];

    public string Read(Guid id)
    {
        if (_cache.TryGetValue(id, out var hit))
        {
            Trace.Add($"cache HIT  {id.ToString()[..8]}");
            return hit;
        }

        Trace.Add($"cache MISS {id.ToString()[..8]}");
        return _cache[id] = inner.Read(id);
    }
}

public sealed class RetryingClaimReader(IClaimReader inner, int attempts = 3) : IClaimReader
{
    public string Read(Guid id)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return inner.Read(id);
            }
            catch (Exception) when (attempt < attempts)
            {
                // swallow and retry — a transient fault
            }
        }
    }
}

public sealed class LoggingClaimReader(IClaimReader inner) : IClaimReader
{
    public List<string> Trace { get; } = [];

    public string Read(Guid id)
    {
        Trace.Add($"-> Read({id.ToString()[..8]})");
        var result = inner.Read(id);
        Trace.Add($"<- {result}");
        return result;
    }
}

public sealed class DecoratorDemo : PatternDemo
{
    public override string Key => "decorator";
    public override string Name => "Decorator";
    public override PatternCategory Category => PatternCategory.Structural;
    public override string Intent => "Add behaviour by wrapping, without touching the wrapped class.";
    public override string HandbookSection => "1.5";
    public override string SeenInDotNet => "DelegatingHandler, ASP.NET Core middleware, Stream wrappers";

    protected override void Execute(Action<string> log)
    {
        var real = new DatabaseClaimReader();
        var caching = new CachingClaimReader(new RetryingClaimReader(real));
        var logging = new LoggingClaimReader(caching);

        IClaimReader reader = logging;      // the caller only ever sees IClaimReader

        var id = Guid.CreateVersion7();
        reader.Read(id);
        reader.Read(id);
        reader.Read(Guid.CreateVersion7());

        log("Chain: Logging -> Caching -> Retrying -> DatabaseClaimReader");
        foreach (var line in logging.Trace) log("  " + line);
        foreach (var line in caching.Trace) log("  " + line);

        log($"The database was hit {real.Reads} times for 3 reads — the caching layer absorbed one.");
        log("Order is configuration: registering retry OUTSIDE caching would retry cache hits too.");
    }
}
