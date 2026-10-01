namespace PatternsLab.Patterns.Structural;

// ---------------------------------------------------------------------------
// PROXY — handbook §1.3
//
// A stand-in that CONTROLS ACCESS to the real object. Structurally it looks like a
// decorator; the difference is intent. A decorator ADDS behaviour, a proxy DECIDES
// WHETHER AND WHEN the real thing is reached — laziness, authorisation, remoteness.
// ---------------------------------------------------------------------------

public interface IClaimDocumentStore
{
    byte[] Download(Guid claimId);
}

public sealed class BlobDocumentStore : IClaimDocumentStore
{
    public int Downloads { get; private set; }

    public byte[] Download(Guid claimId)
    {
        Downloads++;
        return System.Text.Encoding.UTF8.GetBytes($"PDF for {claimId}");
    }
}

/// <summary>Virtual proxy: the expensive object is not touched until it is really needed.</summary>
public sealed class LazyDocumentProxy(Func<IClaimDocumentStore> factory) : IClaimDocumentStore
{
    private IClaimDocumentStore? _real;

    public bool RealObjectCreated => _real is not null;

    public byte[] Download(Guid claimId) => (_real ??= factory()).Download(claimId);
}

/// <summary>Protection proxy: the call never reaches the real object without the right role.</summary>
public sealed class AuthorisingDocumentProxy(IClaimDocumentStore inner, Func<string> currentRole)
    : IClaimDocumentStore
{
    public byte[] Download(Guid claimId) =>
        currentRole() is "billing" or "admin"
            ? inner.Download(claimId)
            : throw new UnauthorizedAccessException($"Role '{currentRole()}' may not download claim documents.");
}

public sealed class ProxyDemo : PatternDemo
{
    public override string Key => "proxy";
    public override string Name => "Proxy";
    public override PatternCategory Category => PatternCategory.Structural;
    public override string Intent => "Stand in for another object to control access to it.";
    public override string HandbookSection => "1.3";
    public override string SeenInDotNet => "EF Core lazy-loading proxies, Lazy<T>, gRPC/HTTP clients";

    protected override void Execute(Action<string> log)
    {
        var real = new BlobDocumentStore();
        var lazy = new LazyDocumentProxy(() => real);

        log($"After constructing the proxy, was the real store created? {lazy.RealObjectCreated}");
        lazy.Download(Guid.CreateVersion7());
        log($"After the first Download,                               {lazy.RealObjectCreated}");

        var role = "reporting";
        var guarded = new AuthorisingDocumentProxy(real, () => role);

        try
        {
            guarded.Download(Guid.CreateVersion7());
        }
        catch (UnauthorizedAccessException ex)
        {
            log($"Protection proxy refused: {ex.Message}");
        }

        role = "billing";
        guarded.Download(Guid.CreateVersion7());
        log($"With role 'billing' the call goes through. Real downloads so far: {real.Downloads}");
        log("Decorator vs Proxy: same shape, different intent — add behaviour vs. control access.");
    }
}
