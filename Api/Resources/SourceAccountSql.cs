using System.Resources;

namespace Api.Resources;

public static class SourceAccountSql
{
    private static readonly ResourceManager Manager = new(typeof(SourceAccountSql).FullName!, typeof(SourceAccountSql).Assembly);
    private static string Read(string name) => Manager.GetString(name) ?? throw new InvalidOperationException($"Missing SourceAccountSql.{name}");
    public static string Schema { get; } = Read(nameof(Schema));
    public static string Drop { get; } = Read(nameof(Drop));
    public static string Lock { get; } = Read(nameof(Lock));
    public static string Identity { get; } = Read(nameof(Identity));
    public static string Pair { get; } = Read(nameof(Pair));
    public static string CanBind { get; } = Read(nameof(CanBind));
    public static string ViewerCompatible { get; } = Read(nameof(ViewerCompatible));
    public static string LockAuthLineage { get; } = Read(nameof(LockAuthLineage));
    public static string InsertIdentity { get; } = Read(nameof(InsertIdentity));
    public static string Revision { get; } = Read(nameof(Revision));
    public static string Latest { get; } = Read(nameof(Latest));
    public static string LatestByPair { get; } = Read(nameof(LatestByPair));
    public static string LastTime { get; } = Read(nameof(LastTime));
    public static string InsertRevision { get; } = Read(nameof(InsertRevision));
}
