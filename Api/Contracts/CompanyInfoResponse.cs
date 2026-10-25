namespace Api.Contracts;

/// <summary>
/// Компания из internal-ответа company-service ({ id, name, creatorId, bankProfileId }).
/// CreatorId нужен user-service для проверок «менять роли может только создатель компании».
/// </summary>
public class CompanyInfoResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid CreatorId { get; set; }
    public Guid? BankProfileId { get; set; }
}
