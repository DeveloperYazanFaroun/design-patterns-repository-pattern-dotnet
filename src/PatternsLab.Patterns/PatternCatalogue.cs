namespace PatternsLab.Patterns;

public sealed record PatternInfo(
    string Key,
    string Name,
    string Category,
    string Intent,
    string HandbookSection,
    string SeenInDotNet);

/// <summary>
/// The whole Gang of Four catalogue, in one place — the runnable version of Table 1.1.
///
/// Every demo is registered against <see cref="IPatternDemo"/>, so this class receives
/// them as <c>IEnumerable&lt;IPatternDemo&gt;</c>. Adding a 24th pattern means adding a
/// class and one registration; this file never changes. That is the Strategy pattern
/// being used to host a catalogue of patterns.
/// </summary>
public sealed class PatternCatalogue(IEnumerable<IPatternDemo> demos)
{
    private readonly IReadOnlyDictionary<string, IPatternDemo> _byKey =
        demos.ToDictionary(d => d.Key, StringComparer.OrdinalIgnoreCase);

    public int Count => _byKey.Count;

    public IReadOnlyList<PatternInfo> All() =>
        _byKey.Values
              .OrderBy(d => d.Category)
              .ThenBy(d => d.Name, StringComparer.Ordinal)
              .Select(Describe)
              .ToList();

    public IReadOnlyList<PatternInfo> ByCategory(PatternCategory category) =>
        _byKey.Values
              .Where(d => d.Category == category)
              .OrderBy(d => d.Name, StringComparer.Ordinal)
              .Select(Describe)
              .ToList();

    public IPatternDemo? Find(string key) => _byKey.GetValueOrDefault(key);

    public IReadOnlyList<string> Run(string key) =>
        Find(key) is { } demo
            ? demo.Run()
            : throw new KeyNotFoundException(
                $"No pattern demo is registered under '{key}'. Known keys: {string.Join(", ", _byKey.Keys.Order())}.");

    private static PatternInfo Describe(IPatternDemo d) =>
        new(d.Key, d.Name, d.Category.ToString(), d.Intent, d.HandbookSection, d.SeenInDotNet);
}
