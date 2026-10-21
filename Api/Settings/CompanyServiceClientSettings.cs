using AlfaHackaton.HttpClient.Common.Settings;

namespace Api.Settings;

/// <summary>
/// Настройки клиента company-service: BaseAddress — из appsettings (Services:CompanyService),
/// секрет кластера — общий X-Internal-Secret.
/// </summary>
public class CompanyServiceClientSettings : HttpClientSettings
{
    public CompanyServiceClientSettings(IConfiguration configuration)
    {
        BaseAddress = configuration["Services:CompanyService"]
            ?? throw new InvalidOperationException("Не задан адрес company-service (Services:CompanyService)");

        DefaultHeaders = headers => headers.Add("X-Internal-Secret",
            configuration["Internal:Secret"] ?? string.Empty);
    }
}
