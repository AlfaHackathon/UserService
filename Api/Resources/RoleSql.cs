using System.Resources;

namespace Api.Resources;

/// <summary>
/// Доступ к SQL-запросам из Resources/RoleSql.resx.
/// Весь SQL живёт в ресурсах, репозитории не содержат сырых строк.
/// </summary>
public static class RoleSql
{
    private static readonly ResourceManager Manager =
        new("Api.Resources.RoleSql", typeof(RoleSql).Assembly);

    public static string GetDefaults { get; } = Manager.GetString(nameof(GetDefaults))
        ?? throw new InvalidOperationException("Ресурс RoleSql.GetDefaults не найден");

    public static string GetDefaultsPlusCompany { get; } = Manager.GetString(nameof(GetDefaultsPlusCompany))
        ?? throw new InvalidOperationException("Ресурс RoleSql.GetDefaultsPlusCompany не найден");

    public static string GetByIdInCompany { get; } = Manager.GetString(nameof(GetByIdInCompany))
        ?? throw new InvalidOperationException("Ресурс RoleSql.GetByIdInCompany не найден");

    public static string CreateCustom { get; } = Manager.GetString(nameof(CreateCustom))
        ?? throw new InvalidOperationException("Ресурс RoleSql.CreateCustom не найден");

    public static string DeleteCustom { get; } = Manager.GetString(nameof(DeleteCustom))
        ?? throw new InvalidOperationException("Ресурс RoleSql.DeleteCustom не найден");

    public static string AssignToUser { get; } = Manager.GetString(nameof(AssignToUser))
        ?? throw new InvalidOperationException("Ресурс RoleSql.AssignToUser не найден");

    public static string CountUsersWithRole { get; } = Manager.GetString(nameof(CountUsersWithRole))
        ?? throw new InvalidOperationException("Ресурс RoleSql.CountUsersWithRole не найден");

    public static string NameExists { get; } = Manager.GetString(nameof(NameExists))
        ?? throw new InvalidOperationException("Ресурс RoleSql.NameExists не найден");
}
