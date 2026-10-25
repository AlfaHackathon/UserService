using AlfaHackaton.Result;
using AlfaHackaton.Result.Errors;
using Api.Contracts;
using Api.Models;
using Api.Repositories.Interfaces;
using Api.Services.Interfaces;

namespace Api.Services;

public class RoleService : IRoleService
{
    private readonly IRoleRepository _repository;
    private readonly ICompanyServiceClient _companyClient;

    public RoleService(IRoleRepository repository, ICompanyServiceClient companyClient)
    {
        _repository = repository;
        _companyClient = companyClient;
    }

    public async Task<Result<List<RoleResponse>>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var company = await _companyClient.GetByUserAsync(userId, cancellationToken);
        if (company.Error is not null)
        {
            return new Result<List<RoleResponse>>(company.Error);
        }

        // без компании юзер видит только дефолтные роли, кастомные чужих компаний в выборку не попадают
        var roles = await _repository.GetVisibleAsync(company.Content?.Id, cancellationToken);
        return new Result<List<RoleResponse>>(roles.Select(ToResponse).ToList());
    }

    public async Task<Result<RoleResponse>> CreateAsync(Guid requesterId, string name, CancellationToken cancellationToken = default)
    {
        var company = await RequireCreatorCompanyAsync(requesterId, cancellationToken, "Создавать роли может только создатель компании");
        if (company.Error is not null)
        {
            return new Result<RoleResponse>(company.Error);
        }

        // имя кастомной роли не пересекается ни с ролями компании, ни с дефолтными (типа «Ген директор»)
        if (await _repository.NameExistsAsync(company.Content!.Id, name, cancellationToken))
        {
            return new Result<RoleResponse>(Fail("Роль с таким именем уже существует", ErrorType.CorruptedInfo));
        }

        var role = await _repository.CreateCustomAsync(new Role
        {
            Id = Guid.NewGuid(),
            Name = name,
            IsDefault = false, // дефолтные роли создаёт только сид миграции
            CompanyId = company.Content.Id,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        return new Result<RoleResponse>(ToResponse(role));
    }

    public async Task<Result<object>> DeleteAsync(Guid requesterId, Guid roleId, CancellationToken cancellationToken = default)
    {
        var company = await RequireCreatorCompanyAsync(requesterId, cancellationToken, "Удалять роли может только создатель компании");
        if (company.Error is not null)
        {
            return new Result<object>(company.Error);
        }

        var role = await _repository.GetByIdInCompanyAsync(roleId, company.Content!.Id, cancellationToken);
        // видима компании, но это не кастомная роль нашей компании (дефолтная/чужая) → удаления нет
        if (role is null || role.IsDefault || role.CompanyId != company.Content.Id)
        {
            return new Result<object>(Fail("Роль не найдена", ErrorType.NotFound));
        }

        if (await _repository.CountUsersWithRoleAsync(roleId, cancellationToken) > 0)
        {
            return new Result<object>(Fail("Роль занята пользователем", ErrorType.CorruptedInfo));
        }

        await _repository.DeleteCustomAsync(roleId, company.Content.Id, cancellationToken);
        return new Result<object>();
    }

    public async Task<Result<object>> SetUserRoleAsync(Guid requesterId, Guid targetUserId, Guid? roleId, CancellationToken cancellationToken = default)
    {
        var company = await RequireCreatorCompanyAsync(requesterId, cancellationToken, "Менять роли может только создатель компании");
        if (company.Error is not null)
        {
            return new Result<object>(company.Error);
        }

        // целевой пользователь — участник той же компании (иначе роль не трогаем)
        var targetCompany = await _companyClient.GetByUserAsync(targetUserId, cancellationToken);
        if (targetCompany.Error is not null)
        {
            return new Result<object>(targetCompany.Error);
        }
        if (targetCompany.Content is null || targetCompany.Content.Id != company.Content!.Id)
        {
            return new Result<object>(Fail("Пользователь не состоит в вашей компании", ErrorType.NotFound));
        }

        if (roleId is not null)
        {
            // FK не отличает чужие кастомные роли от дефолтных — видимость проверяем тут
            var role = await _repository.GetByIdInCompanyAsync(roleId.Value, company.Content.Id, cancellationToken);
            if (role is null)
            {
                return new Result<object>(Fail("Роль не найдена", ErrorType.NotFound));
            }
        }

        await _repository.AssignToUserAsync(targetUserId, roleId, cancellationToken);
        return new Result<object>();
    }

    /// <summary>Компания запрашивающего; ошибка, если company-service недоступен, юзер не в компании или не её создатель.</summary>
    private async Task<Result<CompanyInfoResponse>> RequireCreatorCompanyAsync(
        Guid userId, CancellationToken cancellationToken, string creatorErrorMessage)
    {
        var company = await _companyClient.GetByUserAsync(userId, cancellationToken);
        if (company.Error is not null)
        {
            return new Result<CompanyInfoResponse>(company.Error);
        }

        if (company.Content is null)
        {
            return new Result<CompanyInfoResponse>(Fail("Пользователь не состоит в компании", ErrorType.NotFound));
        }

        if (company.Content.CreatorId != userId)
        {
            return new Result<CompanyInfoResponse>(Fail(creatorErrorMessage, ErrorType.NoRights));
        }

        return new Result<CompanyInfoResponse>(company.Content);
    }

    /// <summary>Ошибки клиентских сценариев как ErrorSource.Client; HTTP-код BaseController берёт из ErrorType.</summary>
    private static Error Fail(string title, ErrorType type) =>
        new(title, ErrorSource.Client, type);

    private static RoleResponse ToResponse(Role role) => new()
    {
        Id = role.Id,
        Name = role.Name,
        IsDefault = role.IsDefault,
        CompanyId = role.CompanyId
    };
}
