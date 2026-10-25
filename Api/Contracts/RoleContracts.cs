namespace Api.Contracts;

/// <summary>Роль, видимая компании.</summary>
public class RoleResponse
{
    public required Guid Id { get; set; }
    public required string Name { get; set; }
    public required bool IsDefault { get; set; }
    /// <summary>null у дефолтных ролей; у кастомных — компания-владелец.</summary>
    public Guid? CompanyId { get; set; }
}

/// <summary>Создание кастомной роли (company_id подставится по компании создателя).</summary>
public class CreateRoleRequest
{
    public required string Name { get; set; }
}

/// <summary>Назначить/сменить/снять роль пользователю: {"roleId": "..."} или {"roleId": null}.</summary>
public class UpdateUserRoleRequest
{
    public Guid? RoleId { get; set; }
}
