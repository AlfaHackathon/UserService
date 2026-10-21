using AlfaHackaton.HttpClient.Common.Interfaces;
using Api.Contracts;
using Api.Services.Interfaces;
using Api.Settings;

namespace Api.Services;

/// <summary>
/// Клиент company-service: ходит в другой сервис через AlfaHackaton.HttpClient.
/// BaseAddress и internal-секрет — из настроек (Settings/CompanyServiceClientSettings.cs).
/// </summary>
public class CompanyServiceClient(IHttpClient<CompanyServiceClientSettings> http) : ICompanyServiceClient
{
    public async Task<CompanyInfoResponse?> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var response = await http.Get<CompanyInfoResponse>($"company-service/internal/companies/by-user/{userId}", cancellationToken: cancellationToken);
        return response.IsSuccessStatusCode ? response.Result : null;
    }

    public async Task<List<Guid>?> GetMemberIdsAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var response = await http.Get<List<Guid>>($"company-service/internal/companies/{companyId}/member-ids", cancellationToken: cancellationToken);
        return response.IsSuccessStatusCode ? response.Result : null;
    }
}
