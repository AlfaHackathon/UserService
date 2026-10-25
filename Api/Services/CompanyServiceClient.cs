using AlfaHackaton.HttpClient.Common.Interfaces;
using AlfaHackaton.Result;
using AlfaHackaton.Result.Errors;
using Api.Contracts;
using Api.Services.Interfaces;
using Api.Settings;

namespace Api.Services;

/// <summary>
/// Клиент company-service: ходит в другой сервис через AlfaHackaton.HttpClient.
/// BaseAddress и internal-секрет — из настроек (Settings/CompanyServiceClientSettings.cs).
/// Result-pattern: Content=null — просто «не состоит»; Error — company-service недоступен.
/// </summary>
public class CompanyServiceClient(IHttpClient<CompanyServiceClientSettings> http) : ICompanyServiceClient
{
    public async Task<Result<CompanyInfoResponse?>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var response = await http.Get<CompanyInfoResponse>($"company-service/internal/companies/by-user/{userId}", cancellationToken: cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return new Result<CompanyInfoResponse?>(response.Result ?? new CompanyInfoResponse());
        }

        // 404 — валидный ответ «пользователь не состоит в компании», это не ошибка
        if (response.Status == System.Net.HttpStatusCode.NotFound)
        {
            return new Result<CompanyInfoResponse?>(null);
        }

        return new Result<CompanyInfoResponse?>(new Error(
            "company-service недоступен", ErrorSource.External, ErrorType.Undefined)
        {
            HttpStatusCode = System.Net.HttpStatusCode.BadGateway
        });
    }

    public async Task<List<Guid>?> GetMemberIdsAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        var response = await http.Get<List<Guid>>($"company-service/internal/companies/{companyId}/member-ids", cancellationToken: cancellationToken);
        return response.IsSuccessStatusCode ? response.Result : null;
    }
}
