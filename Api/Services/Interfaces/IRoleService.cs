using AlfaHackaton.Result;
using Api.Contracts;
using Api.Services.Interfaces;

namespace Api.Services.Interfaces;

/// <summary>Бизнес-операции над ролями в user-service (Result-pattern, без исключений).</summary>
public interface IRoleService
{
    /// <summary>Роли, видимые пользователю: все дефолтные + кастомные его компании. Без компании — только дефолтные.</summary>
    Task<Result<List<RoleResponse>>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Создать кастомную роль своей компании. Только создатель компании.</summary>
    Task<Result<RoleResponse>> CreateAsync(Guid requesterId, string name, CancellationToken cancellationToken = default);

    /// <summary>Удалить кастомную роль своей компании (дефолтные и занятые — нельзя). Только создатель компании.</summary>
    Task<Result<object>> DeleteAsync(Guid requesterId, Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>Назначить/сменить/снять роль (roleId=null) пользователю своей компании. Только создатель компании.</summary>
    Task<Result<object>> SetUserRoleAsync(Guid requesterId, Guid targetUserId, Guid? roleId, CancellationToken cancellationToken = default);
}
