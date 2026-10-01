namespace PatternsLab.Patterns.Creational;

// ---------------------------------------------------------------------------
// ABSTRACT FACTORY — handbook §1.3
//
// Factory Method makes ONE object. Abstract Factory makes a FAMILY of objects
// that must be used together. The guarantee it buys you: you can never end up
// with a SQL Server connection paired with a PostgreSQL command builder.
// ---------------------------------------------------------------------------

public interface IDbConnectionHandle { string Describe(); }
public interface IDbCommandBuilder { string Build(string table, string column); }
public interface IPagingDialect { string Page(int skip, int take); }

/// <summary>The abstract factory: one method per member of the family.</summary>
public interface IStorageProviderFactory
{
    string ProviderName { get; }
    IDbConnectionHandle CreateConnection();
    IDbCommandBuilder CreateCommandBuilder();
    IPagingDialect CreatePagingDialect();
}

public sealed class SqlServerFactory : IStorageProviderFactory
{
    public string ProviderName => "SqlServer";
    public IDbConnectionHandle CreateConnection() => new SqlServerConnectionHandle();
    public IDbCommandBuilder CreateCommandBuilder() => new SqlServerCommandBuilder();
    public IPagingDialect CreatePagingDialect() => new SqlServerPaging();

    private sealed class SqlServerConnectionHandle : IDbConnectionHandle
    {
        public string Describe() => "Server=.;Database=PatternsLab;TrustServerCertificate=true";
    }

    private sealed class SqlServerCommandBuilder : IDbCommandBuilder
    {
        public string Build(string table, string column) => $"SELECT * FROM [{table}] WHERE [{column}] = @p0";
    }

    private sealed class SqlServerPaging : IPagingDialect
    {
        public string Page(int skip, int take) => $"OFFSET {skip} ROWS FETCH NEXT {take} ROWS ONLY";
    }
}

public sealed class SqliteFactory : IStorageProviderFactory
{
    public string ProviderName => "Sqlite";
    public IDbConnectionHandle CreateConnection() => new SqliteConnectionHandle();
    public IDbCommandBuilder CreateCommandBuilder() => new SqliteCommandBuilder();
    public IPagingDialect CreatePagingDialect() => new SqlitePaging();

    private sealed class SqliteConnectionHandle : IDbConnectionHandle
    {
        public string Describe() => "Data Source=patternslab.db";
    }

    private sealed class SqliteCommandBuilder : IDbCommandBuilder
    {
        public string Build(string table, string column) => $"SELECT * FROM \"{table}\" WHERE \"{column}\" = $p0";
    }

    private sealed class SqlitePaging : IPagingDialect
    {
        public string Page(int skip, int take) => $"LIMIT {take} OFFSET {skip}";
    }
}

public sealed class AbstractFactoryDemo : PatternDemo
{
    public override string Key => "abstract-factory";
    public override string Name => "Abstract Factory";
    public override PatternCategory Category => PatternCategory.Creational;
    public override string Intent => "Create families of related objects without naming their concrete classes.";
    public override string HandbookSection => "1.3";
    public override string SeenInDotNet => "DbProviderFactory; EF Core's provider model";

    protected override void Execute(Action<string> log)
    {
        IStorageProviderFactory[] factories = [new SqlServerFactory(), new SqliteFactory()];

        foreach (var factory in factories)
        {
            log($"{factory.ProviderName}:");
            log($"  connection : {factory.CreateConnection().Describe()}");
            log($"  command    : {factory.CreateCommandBuilder().Build("Claims", "PayerCode")}");
            log($"  paging     : {factory.CreatePagingDialect().Page(50, 25)}");
        }

        log("The family always matches. A SQL Server connection can never be paired with SQLite paging.");
    }
}
