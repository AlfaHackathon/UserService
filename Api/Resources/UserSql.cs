using System.Resources;

namespace Api.Resources;

/// <summary>
/// Доступ к SQL-запросам из Resources/UserSql.resx.
/// Весь SQL живёт в ресурсах, репозитории не содержат сырых строк.
/// </summary>
public static class UserSql
{
    private static readonly ResourceManager Manager =
        new(typeof(UserSql).FullName!, typeof(UserSql).Assembly);

    public static string GetAll { get; } = Manager.GetString(nameof(GetAll))
        ?? throw new InvalidOperationException("Ресурс UserSql.GetAll не найден");

    public static string GetById { get; } = Manager.GetString(nameof(GetById))
        ?? throw new InvalidOperationException("Ресурс UserSql.GetById не найден");

    public static string GetByEmail { get; } = Manager.GetString(nameof(GetByEmail))
        ?? throw new InvalidOperationException("Ресурс UserSql.GetByEmail не найден");

    public static string ExistsByEmail { get; } = Manager.GetString(nameof(ExistsByEmail))
        ?? throw new InvalidOperationException("Ресурс UserSql.ExistsByEmail не найден");

    public static string Add { get; } = Manager.GetString(nameof(Add))
        ?? throw new InvalidOperationException("Ресурс UserSql.Add не найден");
}
