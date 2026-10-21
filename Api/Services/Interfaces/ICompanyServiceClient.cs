using Api.Contracts;

namespace Api.Services.Interfaces;

/// <summary>Клиент company-service (internal-ручки, ходим по HTTP как сервис кластера).</summary>
public interface ICompanyServiceClient
{
    /// <summary>Компания пользователя, null — не состоит ни в одной.</summary>
    Task<CompanyInfoResponse?> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>id всех участников компании, null — компания не найдена.</summary>
    Task<List<Guid>?> GetMemberIdsAsync(Guid companyId, CancellationToken cancellationToken = default);
}
