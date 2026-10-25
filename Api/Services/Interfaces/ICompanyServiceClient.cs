using AlfaHackaton.Result;
using Api.Contracts;

namespace Api.Services.Interfaces;

/// <summary>Клиент company-service (internal-ручки, ходим по HTTP как сервис кластера).</summary>
public interface ICompanyServiceClient
{
    /// <summary>Компания пользователя. Content=null — не состоит; Error — company-service недоступен.</summary>
    Task<Result<CompanyInfoResponse?>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>id всех участников компании, null — компания не найдена.</summary>
    Task<List<Guid>?> GetMemberIdsAsync(Guid companyId, CancellationToken cancellationToken = default);
}
