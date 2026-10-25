using Api.Models;

namespace Api.Repositories.Interfaces;

public interface IRoleRepository
{
    /// <summary>Роли, видимые компании: все дефолтные + кастомные этой компании (companyId=null — только дефолтные).</summary>
    Task<List<Role>> GetVisibleAsync(Guid? companyId, CancellationToken cancellationToken = default);

    /// <summary>Роль по id, если она видна компании (дефолтная или кастомная этой компании).</summary>
    Task<Role?> GetByIdInCompanyAsync(Guid roleId, Guid companyId, CancellationToken cancellationToken = default);

    Task<Role> CreateCustomAsync(Role role, CancellationToken cancellationToken = default);

    /// <summary>Удаляет кастомную роль компании (дефолтные размером сидов не удаляются).</summary>
    Task DeleteCustomAsync(Guid roleId, Guid companyId, CancellationToken cancellationToken = default);

    Task AssignToUserAsync(Guid userId, Guid? roleId, CancellationToken cancellationToken = default);

    Task<long> CountUsersWithRoleAsync(Guid roleId, CancellationToken cancellationToken = default);

    /// <summary>Имя занято кастомной ролью компании или совпадает с дефолтной ролью.</summary>
    Task<bool> NameExistsAsync(Guid companyId, string name, CancellationToken cancellationToken = default);
}
