using AlfaHackaton.DapperContext.Common.Interfaces.Dapper;
using AlfaHackaton.DapperContext.Dapper;
using Api.Models;
using Api.Repositories.Interfaces;
using Api.Resources;

namespace Api.Repositories;

public class RoleRepository : IRoleRepository
{
    private readonly IDapperContext _context;

    public RoleRepository(IDapperContext context)
    {
        _context = context;
    }

    /// <summary>Роли, видимые компании: все дефолтные + кастомные этой компании (companyId=null — только дефолтные).</summary>
    public async Task<List<Role>> GetVisibleAsync(Guid? companyId, CancellationToken cancellationToken = default)
    {
        var sql = companyId is null ? RoleSql.GetDefaults : RoleSql.GetDefaultsPlusCompany;
        return await _context.ListOrEmpty<Role>(new QueryObject(sql, new { companyId }));
    }

    /// <summary>Роль по id, если она видна компании (дефолтная или кастомная этой компании).</summary>
    public async Task<Role?> GetByIdInCompanyAsync(Guid roleId, Guid companyId, CancellationToken cancellationToken = default)
    {
        return await _context.FirstOrDefault<Role>(
            new QueryObject(RoleSql.GetByIdInCompany, new { roleId, companyId }));
    }

    public async Task<Role> CreateCustomAsync(Role role, CancellationToken cancellationToken = default)
    {
        return await _context.CommandWithResponse<Role>(new QueryObject(RoleSql.CreateCustom, new
        {
            id = role.Id,
            name = role.Name,
            companyId = role.CompanyId,
            createdAt = role.CreatedAt
        }));
    }

    public async Task DeleteCustomAsync(Guid roleId, Guid companyId, CancellationToken cancellationToken = default)
    {
        await _context.Command(new QueryObject(RoleSql.DeleteCustom, new { roleId, companyId }));
    }

    public async Task AssignToUserAsync(Guid userId, Guid? roleId, CancellationToken cancellationToken = default)
    {
        await _context.Command(new QueryObject(RoleSql.AssignToUser, new { userId, roleId }));
    }

    public async Task<long> CountUsersWithRoleAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        return await _context.CommandWithResponse<long>(new QueryObject(RoleSql.CountUsersWithRole, new { roleId }));
    }

    /// <summary>Имя занято кастомной ролью компании или совпадает с дефолтной ролью.</summary>
    public async Task<bool> NameExistsAsync(Guid companyId, string name, CancellationToken cancellationToken = default)
    {
        return await _context.CommandWithResponse<bool>(
            new QueryObject(RoleSql.NameExists, new { companyId, name }));
    }
}
