using System.Resources;

namespace Api.Resources;

public static class ViewerContextSql
{
    private static readonly ResourceManager Manager = new(typeof(ViewerContextSql).FullName!, typeof(ViewerContextSql).Assembly);
    private static string Read(string name) => Manager.GetString(name) ?? throw new InvalidOperationException($"Missing ViewerContextSql.{name}");
    public static string Find { get; } = Read(nameof(Find));
    public static string Latest { get; } = Read(nameof(Latest));
    public static string LockUser { get; } = Read(nameof(LockUser));
    public static string LastTime { get; } = Read(nameof(LastTime));
    public static string Insert { get; } = Read(nameof(Insert));
    public static string UpdateUser { get; } = Read(nameof(UpdateUser));
}
