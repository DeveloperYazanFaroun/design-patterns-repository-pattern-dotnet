namespace PatternsLab.Patterns;

public enum PatternCategory
{
    /// <summary>How objects get made. "new is a commitment." (Handbook §1.4)</summary>
    Creational = 0,

    /// <summary>How objects are composed. "make these two fit." (Handbook §1.5)</summary>
    Structural = 1,

    /// <summary>How objects talk and decide. "who does what, when." (Handbook §1.6)</summary>
    Behavioural = 2
}

/// <summary>
/// Every pattern in this project implements this so the API can list them and run one
/// on demand. <see cref="Run"/> returns a narrated transcript rather than writing to the
/// console, which keeps each demo testable.
/// </summary>
public interface IPatternDemo
{
    /// <summary>URL-friendly identifier, e.g. "factory-method".</summary>
    string Key { get; }

    string Name { get; }

    PatternCategory Category { get; }

    /// <summary>One line, taken from Table 1.1 of the handbook.</summary>
    string Intent { get; }

    /// <summary>Where the handbook discusses it.</summary>
    string HandbookSection { get; }

    /// <summary>Where you have already met it in .NET without noticing.</summary>
    string SeenInDotNet { get; }

    /// <summary>Runs the demo and returns what happened, line by line.</summary>
    IReadOnlyList<string> Run();
}

/// <summary>Small helper so each demo does not repeat the same boilerplate.</summary>
public abstract class PatternDemo : IPatternDemo
{
    public abstract string Key { get; }
    public abstract string Name { get; }
    public abstract PatternCategory Category { get; }
    public abstract string Intent { get; }
    public abstract string HandbookSection { get; }
    public abstract string SeenInDotNet { get; }

    public IReadOnlyList<string> Run()
    {
        var transcript = new List<string>();
        Execute(transcript.Add);
        return transcript;
    }

    /// <param name="log">Call this for each line of the transcript.</param>
    protected abstract void Execute(Action<string> log);
}
